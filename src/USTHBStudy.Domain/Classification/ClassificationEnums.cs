namespace USTHBStudy.Domain.Classification;

/// <summary>
/// Whether a document carries trustworthy academic metadata. Stored as <c>int</c>.
/// Distinct from <see cref="VerificationStatus"/>: a legacy document is <see cref="Classified"/>
/// (an admin set its metadata) without ever going through community verification.
/// </summary>
public enum ClassificationStatus
{
    /// <summary>No usable metadata yet — eligible for the community queue.</summary>
    Unclassified = 0,

    /// <summary>Metadata present (set by staff, the uploader, consensus, or an admin decision).</summary>
    Classified = 1,

    /// <summary>The community or an admin judged it not to be educational material.</summary>
    NotEducational = 2,
}

/// <summary>How far a document is through the community verification process.</summary>
public enum VerificationStatus
{
    /// <summary>Not part of the community process (legacy or staff-classified documents).</summary>
    Unverified = 0,

    /// <summary>Collecting votes.</summary>
    Pending = 1,

    /// <summary>Votes conflict / are insufficient / reference pending taxonomy — an admin must decide.</summary>
    NeedsReview = 2,

    /// <summary>Consensus reached (or an admin confirmed) and the metadata applied.</summary>
    Verified = 3,

    /// <summary>Rejected as non-educational by policy or by an admin.</summary>
    Rejected = 4,
}

public enum VoteDecision
{
    Classify = 1,
    NotEducational = 2,
}

public enum AssignmentStatus
{
    Assigned = 1,
    Completed = 2,
    Skipped = 3,
    Expired = 4,
}

public enum ClassificationTaskStatus
{
    Open = 1,
    Completed = 2,
    Expired = 3,
}

/// <summary>What caused a task to be presented to the user.</summary>
public enum ContributionTrigger
{
    Manual = 0,
    Login = 1,
    Downloads = 2,
}

/// <summary>What happens when the community judges a document non-educational.</summary>
public enum NonEducationalPolicy
{
    /// <summary>Send to an administrator (safe default — two clicks must not delete content).</summary>
    SendToReview = 1,

    /// <summary>Reject automatically (document status becomes Rejected).</summary>
    AutoReject = 2,
}

/// <summary>The fields users classify. Flags so settings can say which ones are mandatory for consensus.</summary>
[Flags]
public enum ClassificationField
{
    None = 0,
    Specialty = 1,
    Department = 2,
    DocumentType = 4,
    AcademicYear = 8,
    Session = 16,
}

public enum ProposalCategory
{
    Specialty = 1,
    Department = 2,
    DocumentType = 3,
    AcademicYear = 4,
    Session = 5,
}

public enum ProposalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Merged = 4,
}

public enum ExternalTicketPurpose
{
    Login = 1,
    Link = 2,
}
