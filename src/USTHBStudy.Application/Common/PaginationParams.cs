namespace USTHBStudy.Application.Common;

/// <summary>
/// Query-string pagination inputs with clamped, safe defaults (PRD §55): page ≥ 1,
/// 1 ≤ pageSize ≤ <see cref="MaxPageSize"/>.
/// </summary>
public class PaginationParams
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value,
        };
    }

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
