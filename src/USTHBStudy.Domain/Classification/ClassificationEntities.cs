namespace USTHBStudy.Domain.Classification;

using USTHBStudy.Domain.Common;
using USTHBStudy.Domain.Documents;

/// <summary>
/// Single-row, admin-editable configuration of the community classification workflow
/// (task size, consensus policy, triggers, rewards). Nothing here is hard-coded in services.
/// </summary>
public class ClassificationSettings : BaseEntity
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-00000000c1a5");

    // ---- task ----
    public int DocumentsPerTask { get; set; } = 3;
    public int AssignmentExpiryHours { get; set; } = 24;
    public int MinSecondsBeforeVote { get; set; } = 3;

    // ---- consensus ----
    public int RequiredVoters { get; set; } = 3;

    /// <summary>Share (51–100) of votes that must agree on a field's value for it to be accepted.</summary>
    public int AgreementPercent { get; set; } = 66;

    /// <summary>Fields that must reach agreement before a document can be auto-verified.</summary>
    public ClassificationField RequiredFields { get; set; } = ClassificationField.Specialty | ClassificationField.DocumentType;

    /// <summary>Share (51–100) of "not educational" votes that triggers <see cref="NonEducationalPolicy"/>.</summary>
    public int NonEducationalPercent { get; set; } = 66;

    public NonEducationalPolicy NonEducationalPolicy { get; set; } = NonEducationalPolicy.SendToReview;

    // ---- triggers ----
    public bool LoginTriggerEnabled { get; set; } = true;
    public bool DownloadTriggerEnabled { get; set; } = true;
    public int DownloadsPerPrompt { get; set; } = 10;
    public int PromptSnoozeMinutes { get; set; } = 60;

    // ---- rewards (free-tier download quota) ----
    /// <summary>When false (default) no download limits exist and nothing changes for current users.</summary>
    public bool QuotaEnabled { get; set; }
    public int FreeDownloadsPerWindow { get; set; } = 20;
    public int QuotaWindowDays { get; set; } = 30;
    public int BonusDownloadsPerContribution { get; set; } = 2;
    public int MaxBonusPerWindow { get; set; } = 100;
    public int MaxRewardedContributionsPerDay { get; set; } = 50;

    // ---- taxonomy ----
    public int MaxPendingProposalsPerUser { get; set; } = 10;

    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>A batch of documents presented to one user (the unit of "a classification task").</summary>
public class ClassificationTask : BaseEntity
{
    public Guid UserId { get; set; }
    public ContributionTrigger Trigger { get; set; }
    public ClassificationTaskStatus Status { get; set; } = ClassificationTaskStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<ClassificationAssignment> Assignments { get; set; } = new List<ClassificationAssignment>();
}

/// <summary>One document handed to one user in one voting round. Unique per (document, user, round).</summary>
public class ClassificationAssignment : BaseEntity
{
    public Guid TaskId { get; set; }
    public ClassificationTask? Task { get; set; }

    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public Guid UserId { get; set; }
    public int Round { get; set; } = 1;
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Assigned;
    public DateTime AssignedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// One user's verdict on one document in one round. Unique per (document, user, round) — the
/// database, not just the service, forbids a second vote. Skips are recorded on the assignment,
/// not as votes, because they carry no information and never count toward quorum.
/// </summary>
public class ClassificationVote : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public Guid UserId { get; set; }
    public int Round { get; set; }
    public Guid AssignmentId { get; set; }

    public VoteDecision Decision { get; set; }

    public Guid? SpecialtyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public DocumentType? DocumentType { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? SessionId { get; set; }

    // A field is either an approved value (above) or a pending proposal (below), never both.
    public Guid? SpecialtyProposalId { get; set; }
    public Guid? DepartmentProposalId { get; set; }
    public Guid? DocumentTypeProposalId { get; set; }
    public Guid? AcademicYearProposalId { get; set; }
    public Guid? SessionProposalId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Whether this vote earned a contribution reward (daily cap and self-upload rules apply).</summary>
    public bool Rewarded { get; set; }

    /// <summary>Set once the round resolved: did this vote match the outcome?</summary>
    public bool? AgreedWithOutcome { get; set; }
}

/// <summary>Per-user counters used for triggers, quota and reporting. Keyed by user id.</summary>
public class UserContributionStats
{
    public Guid UserId { get; set; }

    public int TasksAssigned { get; set; }
    public int TasksCompleted { get; set; }
    public int DocumentsAssigned { get; set; }
    public int ValidContributions { get; set; }
    public int SkippedCount { get; set; }
    public int ResolvedVotes { get; set; }
    public int AgreedVotes { get; set; }

    public long TotalDownloads { get; set; }
    public long DownloadsAtLastPrompt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastPromptAt { get; set; }
    public DateTime? SnoozedUntil { get; set; }

    // quota window
    public DateTime? QuotaWindowStart { get; set; }
    public int QuotaDownloadsUsed { get; set; }
    public int QuotaBonusEarned { get; set; }

    // daily reward cap
    public DateTime? RewardDay { get; set; }
    public int RewardsToday { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A user-submitted taxonomy value awaiting administrator review. Never visible as a real value.</summary>
public class TaxonomyProposal : BaseEntity
{
    public ProposalCategory Category { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    /// <summary>Normalised text as entered (whitespace-collapsed, trimmed).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Accent/case-insensitive key; with category + parent it forms the dedupe key.</summary>
    public string DedupeKey { get; set; } = string.Empty;

    /// <summary>Optional parent hint: department for a specialty, faculty for a department.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>The document being classified when the value was proposed (for admin context).</summary>
    public Guid? DocumentId { get; set; }

    public Guid SubmittedById { get; set; }
    public DateTime SubmittedAt { get; set; }

    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? AdminNote { get; set; }

    /// <summary>Entity id created by approval, or merged into (specialty/department/year/session).</summary>
    public Guid? ResolvedEntityId { get; set; }

    /// <summary>For DocumentType proposals: the existing enum value they were merged into.</summary>
    public DocumentType? ResolvedDocumentType { get; set; }

    /// <summary>Final name after an admin rename.</summary>
    public string? ApprovedName { get; set; }
}

/// <summary>Single-use, short-lived handoff between the Google callback and the frontend (and link intents).</summary>
public class ExternalLoginTicket : BaseEntity
{
    public string TokenHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public ExternalTicketPurpose Purpose { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
