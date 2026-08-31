namespace USTHBStudy.Application.Students;

public sealed record FavoriteDto(
    Guid Id,
    string Kind,
    Guid EntityId,
    string Title,
    string Slug,
    bool IsPremium,
    DateTime CreatedAt);

public sealed record AddFavoriteRequest(string Kind, Guid EntityId);

public sealed record HistoryEntryDto(
    string Kind,
    Guid EntityId,
    string Title,
    string Slug,
    string? DocumentType,
    DateTime OccurredAt);

public sealed record HistoryDto(
    IReadOnlyList<HistoryEntryDto> RecentDocuments,
    IReadOnlyList<HistoryEntryDto> RecentModules,
    IReadOnlyList<HistoryEntryDto> Downloads);
