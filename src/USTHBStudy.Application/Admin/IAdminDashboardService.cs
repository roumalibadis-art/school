namespace USTHBStudy.Application.Admin;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAsync(CancellationToken ct = default);
}

public sealed record AdminDashboardDto(
    AdminStatsDto Stats,
    IReadOnlyList<TimePointDto> SignupsLast30Days,
    IReadOnlyList<TimePointDto> RevenueLast30Days,
    IReadOnlyList<TimePointDto> DownloadsLast30Days,
    IReadOnlyList<TopItemDto> TopModules,
    IReadOnlyList<TopItemDto> TopDocuments,
    IReadOnlyList<TopItemDto> TopSpecialties);

public sealed record AdminStatsDto(
    int TotalUsers,
    int ActiveUsers,
    int PremiumUsers,
    double ConversionRate,
    int TotalDocuments,
    int PublishedDocuments,
    int PendingDocuments,
    long TotalViews,
    long TotalDownloads,
    decimal Revenue,
    string Currency,
    int ActiveSubscriptions,
    int PendingContributions,
    int OpenReports);

public sealed record TimePointDto(DateOnly Date, decimal Value);

public sealed record TopItemDto(Guid Id, string Label, string Slug, long Count);
