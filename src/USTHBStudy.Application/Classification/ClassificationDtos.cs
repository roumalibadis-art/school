namespace USTHBStudy.Application.Classification;

// ---------------- settings ----------------

public sealed record ClassificationSettingsDto(
    int DocumentsPerTask,
    int AssignmentExpiryHours,
    int MinSecondsBeforeVote,
    int RequiredVoters,
    int AgreementPercent,
    IReadOnlyList<string> RequiredFields,
    int NonEducationalPercent,
    string NonEducationalPolicy,
    bool LoginTriggerEnabled,
    bool DownloadTriggerEnabled,
    int DownloadsPerPrompt,
    int PromptSnoozeMinutes,
    bool QuotaEnabled,
    int FreeDownloadsPerWindow,
    int QuotaWindowDays,
    int BonusDownloadsPerContribution,
    int MaxBonusPerWindow,
    int MaxRewardedContributionsPerDay,
    int MaxPendingProposalsPerUser,
    DateTime UpdatedAt);

public sealed record UpdateClassificationSettingsRequest(
    int DocumentsPerTask,
    int AssignmentExpiryHours,
    int MinSecondsBeforeVote,
    int RequiredVoters,
    int AgreementPercent,
    IReadOnlyList<string> RequiredFields,
    int NonEducationalPercent,
    string NonEducationalPolicy,
    bool LoginTriggerEnabled,
    bool DownloadTriggerEnabled,
    int DownloadsPerPrompt,
    int PromptSnoozeMinutes,
    bool QuotaEnabled,
    int FreeDownloadsPerWindow,
    int QuotaWindowDays,
    int BonusDownloadsPerContribution,
    int MaxBonusPerWindow,
    int MaxRewardedContributionsPerDay,
    int MaxPendingProposalsPerUser);

// ---------------- user-facing workflow ----------------

public sealed record QuotaStatusDto(
    bool Enabled,
    bool Exempt,
    int Allowed,
    int Used,
    int Remaining,
    int BonusEarned,
    DateTime? ResetsAt,
    int BonusPerContribution,
    long TotalDownloads,
    int ValidContributions,
    int DocumentsPerTask);

public sealed record ClassificationPromptDto(
    bool ShouldPrompt,
    string? Reason,
    bool HasOpenTask,
    int OpenTaskRemaining,
    int DocumentsPerTask,
    int AvailableDocuments,
    QuotaStatusDto Quota);

public sealed record PromptAckRequest(string Action);

public sealed record TaskItemDto(
    Guid AssignmentId,
    Guid DocumentId,
    string Status,
    string Title,
    string FileName,
    long FileSize,
    int? PageCount,
    string MimeType,
    bool HasPreview,
    string? Description,
    string? Source,
    DateTime UploadedAt);

public sealed record ClassificationTaskDto(
    Guid Id,
    string Trigger,
    DateTime ExpiresAt,
    int Total,
    int Resolved,
    IReadOnlyList<TaskItemDto> Items);

public sealed record SubmitVoteRequest(
    string Decision,
    Guid? SpecialtyId = null,
    Guid? SpecialtyProposalId = null,
    Guid? DepartmentId = null,
    Guid? DepartmentProposalId = null,
    string? DocumentType = null,
    Guid? DocumentTypeProposalId = null,
    Guid? AcademicYearId = null,
    Guid? AcademicYearProposalId = null,
    Guid? SessionId = null,
    Guid? SessionProposalId = null);

public sealed record VoteResultDto(
    Guid DocumentId,
    string Outcome,
    bool Rewarded,
    int BonusDownloadsGranted,
    int Remaining,
    bool TaskCompleted,
    QuotaStatusDto Quota);

public sealed record SkipResultDto(Guid DocumentId, int Remaining, bool TaskCompleted);

public sealed record OptionDto(Guid Id, string Name, Guid? ParentId, bool Pending = false, Guid? ProposalId = null);

public sealed record DocumentTypeOptionDto(string Value, string Label);

public sealed record ClassificationOptionsDto(
    IReadOnlyList<OptionDto> Departments,
    IReadOnlyList<OptionDto> Specialties,
    IReadOnlyList<OptionDto> AcademicYears,
    IReadOnlyList<OptionDto> Sessions,
    IReadOnlyList<DocumentTypeOptionDto> DocumentTypes,
    IReadOnlyList<OptionDto> MyPendingProposals);

public sealed record MyContributionsDto(
    int TasksAssigned,
    int TasksCompleted,
    int ValidContributions,
    int SkippedCount,
    int ResolvedVotes,
    int AgreedVotes,
    QuotaStatusDto Quota);

// ---------------- admin ----------------

public sealed record ClassificationQueueQuery(
    string Queue = "needs-review",
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record ClassificationDocumentItem(
    Guid Id,
    string Title,
    string Slug,
    string DocumentStatus,
    string Classification,
    string Verification,
    string? ReviewReason,
    int Round,
    int Votes,
    int RequiredVoters,
    DateTime CreatedAt);

public sealed record VoteDetailDto(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    int Round,
    string Decision,
    string? Specialty,
    string? Department,
    string? DocumentType,
    string? AcademicYear,
    string? Session,
    IReadOnlyList<string> PendingProposals,
    bool? AgreedWithOutcome,
    DateTime CreatedAt);

public sealed record ClassificationHistoryEntry(string Action, string? Actor, string? Metadata, DateTime OccurredAt);

public sealed record ClassificationDocumentDetail(
    ClassificationDocumentItem Summary,
    string FileName,
    string? Description,
    string? Source,
    Guid? ModuleId,
    Guid? SpecialtyId,
    Guid? DepartmentId,
    string? DocumentType,
    Guid? AcademicYearId,
    Guid? SessionId,
    bool HasPreview,
    IReadOnlyList<VoteDetailDto> Votes,
    IReadOnlyList<ClassificationHistoryEntry> History);

public sealed record AdminClassificationDecision(
    Guid? SpecialtyId,
    Guid? DepartmentId,
    string? DocumentType,
    Guid? AcademicYearId,
    Guid? SessionId,
    Guid? ModuleId,
    string? Note);

public sealed record AdminNoteRequest(string? Note);

public sealed record ClassificationReportDto(
    long TotalDocuments,
    long Unclassified,
    long Classified,
    long Verified,
    long AwaitingVotes,
    long AwaitingReview,
    long Rejected,
    long TasksAssigned,
    long TasksCompleted,
    double TaskCompletionRate,
    long TotalVotes,
    long ResolvedVotes,
    double VotingAgreementRate,
    long ValidContributions,
    long SkippedAssignments,
    long ProposalsPending,
    long ProposalsApproved,
    long ProposalsRejected,
    long ProposalsMerged,
    long TotalDownloads,
    long ActiveContributors,
    long UsersOverFreeQuota);

// ---------------- taxonomy proposals ----------------

public sealed record ProposeRequest(string Category, string Value, Guid? ParentId = null, Guid? DocumentId = null);

public sealed record SimilarValueDto(string Name, Guid? Id, string? EnumValue, bool Pending);

public sealed record ProposalDto(
    Guid Id,
    string Category,
    string Value,
    string Status,
    Guid? ParentId,
    string? ParentName,
    Guid? DocumentId,
    string? DocumentTitle,
    Guid SubmittedById,
    string? SubmittedByEmail,
    DateTime SubmittedAt,
    Guid? ReviewedById,
    DateTime? ReviewedAt,
    string? AdminNote,
    Guid? ResolvedEntityId,
    string? ResolvedName,
    string? ResolvedDocumentType,
    string? ApprovedName,
    int VoteCount);

public sealed record ProposalDocumentDto(Guid Id, string Title, string Slug, string Verification);

public sealed record ProposalDetailDto(ProposalDto Proposal, IReadOnlyList<ProposalDocumentDto> Documents);

/// <summary>Outcome: <c>Created</c>, <c>ExistingProposal</c> (someone already proposed it) or <c>ExistingValue</c> (it already exists).</summary>
public sealed record ProposeResultDto(
    string Outcome,
    ProposalDto? Proposal,
    SimilarValueDto? Existing,
    IReadOnlyList<SimilarValueDto> Similar);

public sealed record ProposalQuery(
    string? Status = "Pending",
    string? Category = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record ApproveProposalRequest(string? Name, Guid? ParentId, string? Note);

public sealed record RenameProposalRequest(string Name);

public sealed record MergeProposalRequest(Guid? TargetId, string? TargetDocumentType, string? Note);

public sealed record RejectProposalRequest(string? Note);
