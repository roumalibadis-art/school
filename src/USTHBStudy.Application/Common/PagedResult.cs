namespace USTHBStudy.Application.Common;

/// <summary>A single page of results plus the total count (PRD §55).</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public long Total { get; init; }

    public PagedResult()
    {
    }

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, long total)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        Total = total;
    }

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) =>
        new(Array.Empty<T>(), page, pageSize, 0);
}
