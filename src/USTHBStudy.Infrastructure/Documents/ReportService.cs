namespace USTHBStudy.Infrastructure.Documents;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Documents;
using USTHBStudy.Application.Notifications;
using USTHBStudy.Domain.Admin;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Persistence;

public sealed class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;

    public ReportService(AppDbContext db, INotificationService notifications, IAuditLogger audit, IDateTimeProvider clock)
    {
        _db = db;
        _notifications = notifications;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ReportDto> SubmitAsync(
        string documentSlug, Guid? reporterId, string reason, string? comment, CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Slug == documentSlug, ct)
            ?? throw new NotFoundException("Document", documentSlug);

        var report = new DocumentReport
        {
            DocumentId = document.Id,
            ReporterId = reporterId,
            Reason = Enum.Parse<ReportReason>(reason, ignoreCase: true),
            Comment = comment?.Trim(),
            Status = ReportStatus.Open,
        };

        _db.DocumentReports.Add(report);
        await _db.SaveChangesAsync(ct);

        return Map(report, document.Title, document.Slug);
    }

    public async Task<PagedResult<ReportDto>> ListAsync(ReportQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };

        var q = _db.DocumentReports.AsNoTracking().Include(r => r.Document).AsQueryable();
        if (query.Status is not null && Enum.TryParse<ReportStatus>(query.Status, true, out var status))
        {
            q = q.Where(r => r.Status == status);
        }

        var total = await q.LongCountAsync(ct);
        var rows = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip(paging.Skip).Take(paging.Take)
            .ToListAsync(ct);

        var items = rows
            .Select(r => Map(r, r.Document?.Title ?? "—", r.Document?.Slug ?? ""))
            .ToArray();

        return new PagedResult<ReportDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<ReportDto> ResolveAsync(
        Guid reportId, Guid resolvedById, string status, string? note, CancellationToken ct = default)
    {
        var report = await _db.DocumentReports.Include(r => r.Document)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new NotFoundException("DocumentReport", reportId);

        report.Status = Enum.Parse<ReportStatus>(status, ignoreCase: true);
        report.ResolvedById = resolvedById;
        report.ResolvedAt = _clock.UtcNow;
        report.ResolutionNote = note?.Trim();
        await _db.SaveChangesAsync(ct);

        if (report.ReporterId is { } reporterId)
        {
            await _notifications.NotifyAsync(
                reporterId, NotificationType.ReportResolved,
                "Signalement traité",
                $"Votre signalement sur « {report.Document?.Title} » a été traité ({report.Status}).",
                report.Document is null ? null : $"/documents/{report.Document.Slug}", ct);
        }

        await _audit.WriteAsync($"report.{report.Status.ToString().ToLowerInvariant()}", "DocumentReport",
            report.Id.ToString(), new { report.DocumentId, report.Reason }, ct);

        return Map(report, report.Document?.Title ?? "—", report.Document?.Slug ?? "");
    }

    private static ReportDto Map(DocumentReport r, string title, string slug) => new(
        r.Id, r.DocumentId, title, slug, r.ReporterId, r.Reason.ToString(), r.Comment,
        r.Status.ToString(), r.ResolutionNote, r.CreatedAt);
}
