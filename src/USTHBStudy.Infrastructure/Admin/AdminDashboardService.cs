namespace USTHBStudy.Infrastructure.Admin;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Domain.Admin;
using USTHBStudy.Domain.Contributions;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Domain.Students;
using USTHBStudy.Domain.Subscriptions;
using USTHBStudy.Infrastructure.Persistence;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private const int WindowDays = 30;

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AdminDashboardService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AdminDashboardDto> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var cutoff = now.AddDays(-WindowDays);

        var totalUsers = await _db.Users.CountAsync(ct);
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive, ct);
        var premiumUsers = await _db.Users.CountAsync(
            u => u.IsPremium && u.PremiumExpiresAt != null && u.PremiumExpiresAt > now, ct);

        // decimal SUM is not translatable on SQLite — aggregate client-side.
        var totalRevenue = (await _db.Payments.Where(p => p.Status == PaymentStatus.Success)
            .Select(p => p.Amount).ToListAsync(ct)).Sum();

        var stats = new AdminStatsDto(
            totalUsers,
            activeUsers,
            premiumUsers,
            totalUsers == 0 ? 0 : Math.Round(premiumUsers * 100.0 / totalUsers, 1),
            await _db.Documents.CountAsync(ct),
            await _db.Documents.CountAsync(d => d.Status == DocumentStatus.Published, ct),
            await _db.Documents.CountAsync(d => d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingReview, ct),
            await _db.Documents.SumAsync(d => (long?)d.ViewCount, ct) ?? 0,
            await _db.Documents.SumAsync(d => (long?)d.DownloadCount, ct) ?? 0,
            totalRevenue,
            "DZD",
            await _db.Subscriptions.CountAsync(s => s.Status == SubscriptionStatus.Active && s.EndsAt > now, ct),
            await _db.Contributions.CountAsync(c => c.Status == ContributionStatus.Pending, ct),
            await _db.DocumentReports.CountAsync(r => r.Status == ReportStatus.Open, ct));

        // Time series — grouped in memory so it works identically on MySQL and SQLite.
        var signupDates = await _db.Users.Where(u => u.CreatedAt >= cutoff).Select(u => u.CreatedAt).ToListAsync(ct);
        var revenueRows = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt != null && p.PaidAt >= cutoff)
            .Select(p => new { p.PaidAt, p.Amount }).ToListAsync(ct);
        var downloadDates = await _db.UserActivities
            .Where(a => a.Kind == UserActivityKind.DocumentDownload && a.OccurredAt >= cutoff)
            .Select(a => a.OccurredAt).ToListAsync(ct);

        var signups = Series(cutoff, now, signupDates.Select(d => (d, 1m)));
        var revenue = Series(cutoff, now, revenueRows.Select(r => (r.PaidAt!.Value, r.Amount)));
        var downloads = Series(cutoff, now, downloadDates.Select(d => (d, 1m)));

        // "Top" lists aggregated client-side for provider portability. TODO(Phase 8): move to indexed
        // SQL aggregation once document volume grows.
        var publishedDocs = await _db.Documents.AsNoTracking()
            .Where(d => d.Status == DocumentStatus.Published)
            .Select(d => new { d.Id, d.Title, d.Slug, ModuleId = d.ModuleId ?? Guid.Empty, d.ViewCount })
            .ToListAsync(ct);
        var moduleById = (await _db.Modules.AsNoTracking()
                .Select(m => new { m.Id, m.Name, m.Slug, m.SpecialtyId }).ToListAsync(ct))
            .ToDictionary(m => m.Id);
        var specialtyById = (await _db.Specialties.AsNoTracking()
                .Select(s => new { s.Id, s.Name, s.Slug }).ToListAsync(ct))
            .ToDictionary(s => s.Id);

        var topDocuments = publishedDocs
            .OrderByDescending(d => d.ViewCount).Take(5)
            .Select(d => new TopItemDto(d.Id, d.Title, d.Slug, d.ViewCount)).ToArray();

        var topModules = publishedDocs
            .GroupBy(d => d.ModuleId)
            .Where(g => moduleById.ContainsKey(g.Key))
            .Select(g => new TopItemDto(g.Key, moduleById[g.Key].Name, moduleById[g.Key].Slug, g.Sum(d => d.ViewCount)))
            .OrderByDescending(x => x.Count).Take(5).ToArray();

        var topSpecialties = publishedDocs
            .Where(d => moduleById.ContainsKey(d.ModuleId))
            .GroupBy(d => moduleById[d.ModuleId].SpecialtyId)
            .Where(g => specialtyById.ContainsKey(g.Key))
            .Select(g => new TopItemDto(g.Key, specialtyById[g.Key].Name, specialtyById[g.Key].Slug, g.LongCount()))
            .OrderByDescending(x => x.Count).Take(5).ToArray();

        return new AdminDashboardDto(stats, signups, revenue, downloads, topModules, topDocuments, topSpecialties);
    }

    private static IReadOnlyList<TimePointDto> Series(
        DateTime from, DateTime to, IEnumerable<(DateTime When, decimal Value)> points)
    {
        var buckets = Enumerable.Range(0, (to.Date - from.Date).Days + 1)
            .ToDictionary(offset => DateOnly.FromDateTime(from.Date.AddDays(offset)), _ => 0m);

        foreach (var (when, value) in points)
        {
            var key = DateOnly.FromDateTime(when.Date);
            if (buckets.ContainsKey(key))
            {
                buckets[key] += value;
            }
        }

        return buckets.OrderBy(kv => kv.Key).Select(kv => new TimePointDto(kv.Key, kv.Value)).ToArray();
    }
}
