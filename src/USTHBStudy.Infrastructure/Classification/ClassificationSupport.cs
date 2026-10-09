namespace USTHBStudy.Infrastructure.Classification;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Classification;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>Get-or-create access to the single settings row.</summary>
internal static class ClassificationSettingsStore
{
    public static async Task<ClassificationSettings> LoadAsync(
        AppDbContext db, IDateTimeProvider clock, CancellationToken ct, bool track = false)
    {
        var query = track ? db.ClassificationSettings : db.ClassificationSettings.AsNoTracking();
        var existing = await query.FirstOrDefaultAsync(s => s.Id == ClassificationSettings.SingletonId, ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = new ClassificationSettings { Id = ClassificationSettings.SingletonId, UpdatedAt = clock.UtcNow };
        db.ClassificationSettings.Add(created);
        try
        {
            await db.SaveChangesAsync(ct);
            return created;
        }
        catch (DbUpdateException)
        {
            // Another request created it first.
            db.Entry(created).State = EntityState.Detached;
            return await db.ClassificationSettings.AsNoTracking()
                .FirstAsync(s => s.Id == ClassificationSettings.SingletonId, ct);
        }
    }

    public static ClassificationSettingsDto Map(ClassificationSettings e) => new(
        e.DocumentsPerTask, e.AssignmentExpiryHours, e.MinSecondsBeforeVote, e.RequiredVoters, e.AgreementPercent,
        FieldNames(e.RequiredFields), e.NonEducationalPercent, e.NonEducationalPolicy.ToString(),
        e.LoginTriggerEnabled, e.DownloadTriggerEnabled, e.DownloadsPerPrompt, e.PromptSnoozeMinutes,
        e.QuotaEnabled, e.FreeDownloadsPerWindow, e.QuotaWindowDays, e.BonusDownloadsPerContribution,
        e.MaxBonusPerWindow, e.MaxRewardedContributionsPerDay, e.MaxPendingProposalsPerUser, e.UpdatedAt);

    public static IReadOnlyList<string> FieldNames(ClassificationField fields) =>
        Enum.GetValues<ClassificationField>()
            .Where(f => f != ClassificationField.None && fields.HasFlag(f))
            .Select(f => f.ToString())
            .ToArray();

    public static ClassificationField ParseFields(IEnumerable<string> names) =>
        names.Aggregate(ClassificationField.None, (acc, n) => acc | Enum.Parse<ClassificationField>(n, true));
}

/// <summary>
/// Atomic counter operations on <see cref="UserContributionStats"/>. Each is a single SQL statement so
/// concurrent requests (two downloads, a download and a vote) can never lose an update or overspend a quota.
/// </summary>
internal static class StatsOps
{
    public static async Task EnsureAsync(AppDbContext db, IDateTimeProvider clock, Guid userId, CancellationToken ct)
    {
        if (await db.ContributionStats.AnyAsync(s => s.UserId == userId, ct))
        {
            return;
        }

        var row = new UserContributionStats { UserId = userId, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.ContributionStats.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a creation race — the row exists now.
            db.Entry(row).State = EntityState.Detached;
        }
    }

    /// <summary>Starts a new quota window when the current one has lapsed (or never started).</summary>
    public static Task ResetWindowIfDueAsync(
        AppDbContext db, Guid userId, DateTime now, int windowDays, CancellationToken ct)
    {
        var cutoff = now.AddDays(-windowDays);
        return db.ContributionStats
            .Where(s => s.UserId == userId && (s.QuotaWindowStart == null || s.QuotaWindowStart <= cutoff))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.QuotaWindowStart, now)
                .SetProperty(s => s.QuotaDownloadsUsed, 0)
                .SetProperty(s => s.QuotaBonusEarned, 0), ct);
    }

    public static Task ResetRewardDayIfDueAsync(AppDbContext db, Guid userId, DateTime today, CancellationToken ct) =>
        db.ContributionStats
            .Where(s => s.UserId == userId && (s.RewardDay == null || s.RewardDay < today))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.RewardDay, today)
                .SetProperty(s => s.RewardsToday, 0), ct);

    /// <summary>Claims one of today's reward slots; false when the daily cap is reached.</summary>
    public static async Task<bool> TryClaimDailyRewardAsync(
        AppDbContext db, Guid userId, int cap, CancellationToken ct) =>
        await db.ContributionStats
            .Where(s => s.UserId == userId && s.RewardsToday < cap)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RewardsToday, s => s.RewardsToday + 1), ct) == 1;

    public static Task AddBonusAsync(AppDbContext db, Guid userId, int bonus, int maxBonus, CancellationToken ct) =>
        db.ContributionStats
            .Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(
                s => s.QuotaBonusEarned,
                s => s.QuotaBonusEarned + bonus > maxBonus ? maxBonus : s.QuotaBonusEarned + bonus), ct);
}

/// <summary>Optimistic-concurrency retry shared by every mutation of voting state.</summary>
internal static class ConcurrencyRetry
{
    public static async Task<T> RunAsync<T>(AppDbContext db, Func<Task<T>> action, int attempts = 6)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (DbUpdateConcurrencyException) when (attempt < attempts)
            {
                db.ChangeTracker.Clear();
            }
        }
    }
}

internal static class DocumentTypeLabels
{
    public static IReadOnlyList<DocumentTypeOptionDto> All { get; } =
        Enum.GetValues<DocumentType>()
            .Select(t => new DocumentTypeOptionDto(t.ToString(), t.ToString()))
            .ToArray();
}

internal static class ActiveUser
{
    /// <summary>Suspended accounts keep a valid token until it expires — block them at the action instead.</summary>
    public static async Task EnsureAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, ct))
        {
            throw new USTHBStudy.Application.Common.ForbiddenAppException("This account is disabled.");
        }
    }
}
