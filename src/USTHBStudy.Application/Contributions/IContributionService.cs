namespace USTHBStudy.Application.Contributions;

using USTHBStudy.Application.Common;
using USTHBStudy.Application.Documents;

public interface IContributionService
{
    Task<ContributionDto> SubmitAsync(Guid userId, ContributionRequest request, DocumentFile file, CancellationToken ct = default);

    Task<IReadOnlyList<ContributionDto>> ListMineAsync(Guid userId, CancellationToken ct = default);

    Task<PagedResult<ContributionDto>> ListForModerationAsync(ContributionQuery query, CancellationToken ct = default);

    /// <summary>Creates a document from the submission and links it back (PRD §38).</summary>
    Task<ContributionDto> ApproveAsync(Guid contributionId, string? note, bool publishNow, CancellationToken ct = default);

    Task<ContributionDto> RejectAsync(Guid contributionId, string? note, CancellationToken ct = default);
}

public sealed record ContributionRequest(
    string Title,
    string Type,
    Guid ModuleId,
    Guid? AcademicYearId,
    string? Description);

public sealed record ContributionQuery(string? Status = "Pending", int Page = 1, int PageSize = 20);

public sealed record ContributionDto(
    Guid Id,
    string Title,
    string Type,
    Guid ModuleId,
    Guid? AcademicYearId,
    string? Description,
    string FileName,
    long FileSize,
    string Status,
    string? ReviewNote,
    Guid? CreatedDocumentId,
    Guid SubmittedById,
    DateTime CreatedAt);

public sealed record ModerateContributionRequest(string? Note, bool PublishNow = false);
