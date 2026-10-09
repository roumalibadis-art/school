namespace USTHBStudy.Infrastructure.Classification;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Classification;
using USTHBStudy.Application.Common;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Infrastructure.Persistence;

public sealed class DownloadQuotaService : IDownloadQuotaService, IContributionTracker
{
    public const string ContributionRequiredCode = "contribution_required";

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public DownloadQuotaService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task RecordLoginAsync(Guid userId, CancellationToken ct = default)
    {
        await StatsOps.EnsureAsync(_db, _clock, userId, ct);
        var now = _clock.UtcNow;
        await _db.ContributionStats.Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.LastLoginAt, now), ct);
    }

    public async Task<QuotaStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        await StatsOps.EnsureAsync(_db, _clock, userId, ct);
        if (settings.QuotaEnabled)
        {
            await StatsOps.ResetWindowIfDueAsync(_db, userId, _clock.UtcNow, settings.QuotaWindowDays, ct);
        }

        var stats = await _db.ContributionStats.AsNoTracking().FirstAsync(s => s.UserId == userId, ct);
        return BuildStatus(settings, stats, exempt: false);
    }

    public async Task ConsumeDownloadAsync(Guid userId, bool exempt, CancellationToken ct = default)
    {
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        await StatsOps.EnsureAsync(_db, _clock, userId, ct);

        if (!settings.QuotaEnabled || exempt)
        {
            await _db.ContributionStats.Where(s => s.UserId == userId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.TotalDownloads, s => s.TotalDownloads + 1), ct);
            return;
        }

        await StatsOps.ResetWindowIfDueAsync(_db, userId, _clock.UtcNow, settings.QuotaWindowDays, ct);

        var free = settings.FreeDownloadsPerWindow;
        // One statement: increments only while allowance remains, so concurrent requests cannot overspend.
        var updated = await _db.ContributionStats
            .Where(s => s.UserId == userId && s.QuotaDownloadsUsed < free + s.QuotaBonusEarned)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.QuotaDownloadsUsed, s => s.QuotaDownloadsUsed + 1)
                .SetProperty(s => s.TotalDownloads, s => s.TotalDownloads + 1), ct);

        if (updated == 0)
        {
            throw new ForbiddenAppException(
                "You have used all your free downloads for this period. Help classify a few documents to earn more, or upgrade to Premium.")
                .WithCode(ContributionRequiredCode);
        }
    }

    internal static QuotaStatusDto BuildStatus(ClassificationSettings s, UserContributionStats stats, bool exempt)
    {
        var allowed = s.FreeDownloadsPerWindow + stats.QuotaBonusEarned;
        return new QuotaStatusDto(
            s.QuotaEnabled,
            exempt,
            allowed,
            stats.QuotaDownloadsUsed,
            Math.Max(0, allowed - stats.QuotaDownloadsUsed),
            stats.QuotaBonusEarned,
            stats.QuotaWindowStart?.AddDays(s.QuotaWindowDays),
            s.BonusDownloadsPerContribution,
            stats.TotalDownloads,
            stats.ValidContributions,
            s.DocumentsPerTask);
    }
}

internal static class ForbiddenExceptionExtensions
{
    /// <summary>Adds a machine-readable code to the error list so the UI can react without parsing prose.</summary>
    public static ForbiddenAppException WithCode(this ForbiddenAppException ex, string code) =>
        new(ex.Message, new[] { code });
}
