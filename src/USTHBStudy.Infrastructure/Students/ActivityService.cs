namespace USTHBStudy.Infrastructure.Students;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Students;
using USTHBStudy.Domain.Students;
using USTHBStudy.Infrastructure.Persistence;

public sealed class ActivityService : IActivityService
{
    private const int RecentLimit = 20;

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ActivityService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task RecordDocumentViewAsync(Guid userId, Guid documentId, CancellationToken ct = default) =>
        UpsertAsync(userId, UserActivityKind.DocumentView, documentId: documentId, ct: ct);

    public Task RecordModuleViewAsync(Guid userId, Guid moduleId, CancellationToken ct = default) =>
        UpsertAsync(userId, UserActivityKind.ModuleView, moduleId: moduleId, ct: ct);

    public async Task RecordDocumentDownloadAsync(Guid userId, Guid documentId, CancellationToken ct = default)
    {
        _db.UserActivities.Add(new UserActivity
        {
            UserId = userId,
            Kind = UserActivityKind.DocumentDownload,
            DocumentId = documentId,
            OccurredAt = _clock.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HistoryDto> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var recentDocuments = await _db.UserActivities.AsNoTracking()
            .Where(a => a.UserId == userId && a.Kind == UserActivityKind.DocumentView && a.Document != null)
            .OrderByDescending(a => a.OccurredAt)
            .Take(RecentLimit)
            .Select(a => new HistoryEntryDto(
                "Document", a.DocumentId!.Value, a.Document!.Title, a.Document.Slug, a.Document.Type.ToString(), a.OccurredAt))
            .ToListAsync(ct);

        var recentModules = await _db.UserActivities.AsNoTracking()
            .Where(a => a.UserId == userId && a.Kind == UserActivityKind.ModuleView && a.Module != null)
            .OrderByDescending(a => a.OccurredAt)
            .Take(RecentLimit)
            .Select(a => new HistoryEntryDto(
                "Module", a.ModuleId!.Value, a.Module!.Name, a.Module.Slug, null, a.OccurredAt))
            .ToListAsync(ct);

        var downloads = await _db.UserActivities.AsNoTracking()
            .Where(a => a.UserId == userId && a.Kind == UserActivityKind.DocumentDownload && a.Document != null)
            .OrderByDescending(a => a.OccurredAt)
            .Take(RecentLimit)
            .Select(a => new HistoryEntryDto(
                "Document", a.DocumentId!.Value, a.Document!.Title, a.Document.Slug, a.Document.Type.ToString(), a.OccurredAt))
            .ToListAsync(ct);

        return new HistoryDto(recentDocuments, recentModules, downloads);
    }

    private async Task UpsertAsync(
        Guid userId,
        UserActivityKind kind,
        Guid? documentId = null,
        Guid? moduleId = null,
        CancellationToken ct = default)
    {
        var existing = await _db.UserActivities.FirstOrDefaultAsync(
            a => a.UserId == userId && a.Kind == kind && a.DocumentId == documentId && a.ModuleId == moduleId, ct);

        if (existing is null)
        {
            _db.UserActivities.Add(new UserActivity
            {
                UserId = userId,
                Kind = kind,
                DocumentId = documentId,
                ModuleId = moduleId,
                OccurredAt = _clock.UtcNow,
            });
        }
        else
        {
            existing.OccurredAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
