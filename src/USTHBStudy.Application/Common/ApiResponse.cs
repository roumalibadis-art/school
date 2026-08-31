namespace USTHBStudy.Application.Common;

/// <summary>
/// Standard response envelope (PRD §47) and error body (PRD §45). Serialized as camelCase JSON.
/// Data endpoints return <see cref="ApiResponse{T}"/>; the exception middleware returns the
/// non-generic form for failures.
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse Ok(string? message = null) => new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors?.ToArray() ?? Array.Empty<string>() };

    public static ApiResponse<T> Data<T>(T data, string? message = null) =>
        ApiResponse<T>.Ok(data, message);

    public static ApiResponse<IReadOnlyList<TItem>> Page<TItem>(PagedResult<TItem> page) =>
        new()
        {
            Success = true,
            Data = page.Items,
            Pagination = new PaginationMeta(page.Page, page.PageSize, page.Total),
        };
}

public sealed class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
    public PaginationMeta? Pagination { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };
}

public sealed record PaginationMeta(int Page, int PageSize, long Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
