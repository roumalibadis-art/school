namespace USTHBStudy.Infrastructure.Contributions;

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Contributions;
using USTHBStudy.Application.Documents;
using USTHBStudy.Application.Notifications;
using USTHBStudy.Domain.Admin;
using USTHBStudy.Domain.Contributions;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Documents;
using USTHBStudy.Infrastructure.Persistence;

public sealed class ContributionService : IContributionService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IDocumentService _documents;
    private readonly INotificationService _notifications;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly DocumentOptions _options;

    public ContributionService(
        AppDbContext db,
        IFileStorageService storage,
        IDocumentService documents,
        INotificationService notifications,
        IAuditLogger audit,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        IOptions<DocumentOptions> options)
    {
        _db = db;
        _storage = storage;
        _documents = documents;
        _notifications = notifications;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<ContributionDto> SubmitAsync(
        Guid userId, ContributionRequest request, DocumentFile file, CancellationToken ct = default)
    {
        var type = Enum.Parse<DocumentType>(request.Type, ignoreCase: true);

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw new BadRequestException($"The file exceeds the {_options.MaxFileSizeBytes / (1024 * 1024)} MB limit.");
        }

        await using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        var inspection = FileValidation.Inspect(file.FileName, bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        if (!inspection.IsValid)
        {
            throw new BadRequestException(inspection.Error!);
        }

        if (!await _db.Modules.AnyAsync(m => m.Id == request.ModuleId, ct))
        {
            throw new NotFoundException("Module", request.ModuleId);
        }

        if (request.AcademicYearId is { } yearId && !await _db.AcademicYears.AnyAsync(y => y.Id == yearId, ct))
        {
            throw new NotFoundException("AcademicYear", yearId);
        }

        var stored = await _storage.SaveAsync(
            new MemoryStream(bytes, writable: false), "contributions", file.FileName, inspection.MimeType, ct);

        var contribution = new Contribution
        {
            SubmittedById = userId,
            Title = request.Title.Trim(),
            Type = type,
            ModuleId = request.ModuleId,
            AcademicYearId = request.AcademicYearId,
            Description = request.Description?.Trim(),
            FileStorageKey = stored.StorageKey,
            FileName = stored.FileName,
            FileSize = stored.SizeBytes,
            MimeType = inspection.MimeType,
            FileHashSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            Status = ContributionStatus.Pending,
        };

        _db.Contributions.Add(contribution);
        await _db.SaveChangesAsync(ct);

        return Map(contribution);
    }

    public async Task<IReadOnlyList<ContributionDto>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _db.Contributions.AsNoTracking()
            .Where(c => c.SubmittedById == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return list.Select(Map).ToArray();
    }

    public async Task<PagedResult<ContributionDto>> ListForModerationAsync(ContributionQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };
        var q = _db.Contributions.AsNoTracking();

        if (query.Status is not null && Enum.TryParse<ContributionStatus>(query.Status, true, out var status))
        {
            q = q.Where(c => c.Status == status);
        }

        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderBy(c => c.CreatedAt)
            .Skip(paging.Skip).Take(paging.Take)
            .ToListAsync(ct);

        return new PagedResult<ContributionDto>(items.Select(Map).ToArray(), paging.Page, paging.PageSize, total);
    }

    public async Task<ContributionDto> ApproveAsync(
        Guid contributionId, string? note, bool publishNow, CancellationToken ct = default)
    {
        var contribution = await _db.Contributions.FirstOrDefaultAsync(c => c.Id == contributionId, ct)
                           ?? throw new NotFoundException("Contribution", contributionId);

        if (contribution.Status != ContributionStatus.Pending)
        {
            throw new ConflictException("This contribution has already been reviewed.");
        }

        byte[] fileBytes;
        await using (var content = await _storage.OpenReadAsync(contribution.FileStorageKey, ct))
        {
            await using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        var upload = await _documents.UploadAsync(
            new DocumentUploadRequest(
                contribution.Title, contribution.Type.ToString(), contribution.ModuleId,
                contribution.AcademicYearId, null, contribution.Description, IsPremium: false,
                Source: "Contribution étudiante", RightsStatus: "UserProvided", PermissionNotes: null),
            new DocumentFile(new MemoryStream(fileBytes, writable: false), contribution.FileName, contribution.MimeType, fileBytes.LongLength),
            ct);

        if (publishNow)
        {
            await _documents.ChangeStatusAsync(upload.Document.Id, new DocumentStatusChange("Published", note), ct);
        }

        contribution.Status = ContributionStatus.Approved;
        contribution.ReviewNote = note?.Trim();
        contribution.ReviewedById = _currentUser.UserId;
        contribution.ReviewedAt = _clock.UtcNow;
        contribution.CreatedDocumentId = upload.Document.Id;
        await _db.SaveChangesAsync(ct);

        await _storage.DeleteAsync(contribution.FileStorageKey, ct);
        await _notifications.NotifyAsync(
            contribution.SubmittedById, NotificationType.ContributionApproved,
            "Contribution acceptée", $"« {contribution.Title} » a été acceptée.",
            $"/documents/{upload.Document.Slug}", ct);
        await _audit.WriteAsync("contribution.approved", "Contribution", contribution.Id.ToString(),
            new { contribution.CreatedDocumentId, publishNow }, ct);

        return Map(contribution);
    }

    public async Task<ContributionDto> RejectAsync(Guid contributionId, string? note, CancellationToken ct = default)
    {
        var contribution = await _db.Contributions.FirstOrDefaultAsync(c => c.Id == contributionId, ct)
                           ?? throw new NotFoundException("Contribution", contributionId);

        if (contribution.Status != ContributionStatus.Pending)
        {
            throw new ConflictException("This contribution has already been reviewed.");
        }

        contribution.Status = ContributionStatus.Rejected;
        contribution.ReviewNote = note?.Trim();
        contribution.ReviewedById = _currentUser.UserId;
        contribution.ReviewedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _storage.DeleteAsync(contribution.FileStorageKey, ct);
        await _notifications.NotifyAsync(
            contribution.SubmittedById, NotificationType.ContributionRejected,
            "Contribution refusée",
            $"« {contribution.Title} » n'a pas été retenue." + (string.IsNullOrWhiteSpace(note) ? "" : $" Motif : {note}"),
            "/contribute", ct);
        await _audit.WriteAsync("contribution.rejected", "Contribution", contribution.Id.ToString(), null, ct);

        return Map(contribution);
    }

    private static ContributionDto Map(Contribution c) => new(
        c.Id, c.Title, c.Type.ToString(), c.ModuleId, c.AcademicYearId, c.Description,
        c.FileName, c.FileSize, c.Status.ToString(), c.ReviewNote, c.CreatedDocumentId, c.SubmittedById, c.CreatedAt);
}
