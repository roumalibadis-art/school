namespace USTHBStudy.Application.Documents;

using USTHBStudy.Application.Common;

public interface IReportService
{
    Task<ReportDto> SubmitAsync(string documentSlug, Guid? reporterId, string reason, string? comment, CancellationToken ct = default);

    Task<PagedResult<ReportDto>> ListAsync(ReportQuery query, CancellationToken ct = default);

    Task<ReportDto> ResolveAsync(Guid reportId, Guid resolvedById, string status, string? note, CancellationToken ct = default);
}

public sealed record SubmitReportRequest(string Reason, string? Comment);

public sealed record ResolveReportRequest(string Status, string? Note);

public sealed record ReportQuery(string? Status = "Open", int Page = 1, int PageSize = 20);

public sealed record ReportDto(
    Guid Id,
    Guid DocumentId,
    string DocumentTitle,
    string DocumentSlug,
    Guid? ReporterId,
    string Reason,
    string? Comment,
    string Status,
    string? ResolutionNote,
    DateTime CreatedAt);
