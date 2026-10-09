namespace USTHBStudy.Application.Classification;

using USTHBStudy.Application.Common;
using USTHBStudy.Application.Documents;

public interface IClassificationSettingsService
{
    Task<ClassificationSettingsDto> GetAsync(CancellationToken ct = default);

    Task<ClassificationSettingsDto> UpdateAsync(UpdateClassificationSettingsRequest request, CancellationToken ct = default);
}

/// <summary>The student-facing community classification workflow.</summary>
public interface IClassificationService
{
    /// <summary>Read-only: should the UI invite this user to classify right now, and why?</summary>
    Task<ClassificationPromptDto> GetPromptAsync(Guid userId, CancellationToken ct = default);

    /// <summary><c>start</c> opens (or resumes) a task; <c>later</c> snoozes. Either consumes the trigger so it never repeats.</summary>
    Task<ClassificationTaskDto?> AcknowledgePromptAsync(Guid userId, string action, CancellationToken ct = default);

    /// <summary>The user's open task, or a new one when documents are available; <c>null</c> when there is nothing to do.</summary>
    Task<ClassificationTaskDto?> GetOrCreateTaskAsync(Guid userId, CancellationToken ct = default);

    Task<VoteResultDto> SubmitVoteAsync(Guid userId, Guid assignmentId, SubmitVoteRequest request, CancellationToken ct = default);

    Task<SkipResultDto> SkipAsync(Guid userId, Guid assignmentId, CancellationToken ct = default);

    Task<ClassificationOptionsDto> GetOptionsAsync(Guid userId, CancellationToken ct = default);

    Task<MyContributionsDto> GetMyContributionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Preview image of a document in the user's own open assignment (never of arbitrary documents).</summary>
    Task<DocumentContent> OpenAssignmentPreviewAsync(Guid userId, Guid assignmentId, CancellationToken ct = default);
}

public interface IClassificationAdminService
{
    Task<PagedResult<ClassificationDocumentItem>> ListAsync(ClassificationQueueQuery query, CancellationToken ct = default);

    Task<ClassificationDocumentDetail> GetAsync(Guid documentId, CancellationToken ct = default);

    Task<ClassificationDocumentDetail> VerifyAsync(Guid documentId, AdminClassificationDecision decision, CancellationToken ct = default);

    Task<ClassificationDocumentDetail> RejectAsync(Guid documentId, string? note, CancellationToken ct = default);

    Task<ClassificationDocumentDetail> ReopenAsync(Guid documentId, string? note, CancellationToken ct = default);

    Task<ClassificationReportDto> GetReportAsync(CancellationToken ct = default);

    Task<DocumentContent> OpenPreviewAsync(Guid documentId, CancellationToken ct = default);
}

public interface ITaxonomyProposalService
{
    Task<ProposeResultDto> ProposeAsync(Guid userId, ProposeRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<SimilarValueDto>> FindSimilarAsync(string category, string value, Guid? parentId, CancellationToken ct = default);

    Task<PagedResult<ProposalDto>> ListAsync(ProposalQuery query, CancellationToken ct = default);

    Task<ProposalDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<ProposalDto> ApproveAsync(Guid id, ApproveProposalRequest request, CancellationToken ct = default);

    Task<ProposalDto> RenameAsync(Guid id, string name, CancellationToken ct = default);

    Task<ProposalDto> MergeAsync(Guid id, MergeProposalRequest request, CancellationToken ct = default);

    Task<ProposalDto> RejectAsync(Guid id, string? note, CancellationToken ct = default);
}

/// <summary>Free-tier download quota earned through valid classification contributions.</summary>
public interface IDownloadQuotaService
{
    Task<QuotaStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Counts one download for the trigger counters and, when the quota is enabled and the user is not
    /// exempt, atomically consumes one allowance. Throws <see cref="ForbiddenAppException"/> (with the
    /// <c>contribution_required</c> error) when the allowance is exhausted. Never relaxes access rules.
    /// </summary>
    Task ConsumeDownloadAsync(Guid userId, bool exempt, CancellationToken ct = default);
}

/// <summary>Records sign-ins so the login trigger can fire once per login.</summary>
public interface IContributionTracker
{
    Task RecordLoginAsync(Guid userId, CancellationToken ct = default);
}
