namespace USTHBStudy.Domain.Documents;

using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Domain.Common;

/// <summary>An academic document and its metadata (PRD §12). The binary lives in object storage;
/// only the keys and facts are here.</summary>
public class Document : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    // Classification
    /// <summary>Null while the document is unclassified (community queue); required to publish.</summary>
    public Guid? ModuleId { get; set; }
    public Module? Module { get; set; }

    /// <summary>Direct specialty/department classification for documents without a module yet.
    /// When a module is set, the module's own specialty remains the finer-grained source.</summary>
    public Guid? SpecialtyId { get; set; }
    public Specialty? Specialty { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }

    public Guid? SessionId { get; set; }
    public Session? Session { get; set; }

    // Storage (PRD §7)
    public string FileStorageKey { get; set; } = string.Empty;
    public string? PreviewStorageKey { get; set; }
    public string? ThumbnailStorageKey { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int? PageCount { get; set; }
    public string MimeType { get; set; } = string.Empty;

    /// <summary>SHA-256 of the file, for duplicate detection (PRD §75/§76).</summary>
    public string FileHashSha256 { get; set; } = string.Empty;

    // Access & provenance
    public bool IsPremium { get; set; }
    public string? Source { get; set; }
    public RightsStatus RightsStatus { get; set; } = RightsStatus.Unknown;
    public string? PermissionNotes { get; set; }

    public Guid? UploadedById { get; set; }

    // Community classification (kept separate: classification ≠ verification)
    public ClassificationStatus ClassificationStatus { get; set; } = ClassificationStatus.Classified;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Unverified;

    /// <summary>Incremented when an admin reopens classification; votes/assignments are per round.</summary>
    public int VotingRound { get; set; } = 1;

    /// <summary>Optimistic-concurrency token bumped by every voting/assignment mutation.</summary>
    public long ClassificationVersion { get; set; }

    /// <summary>Why the document is waiting for an administrator (conflict, insufficient agreement…).</summary>
    public string? ClassificationReviewReason { get; set; }

    public DateTime? ClassifiedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedById { get; set; }

    // Moderation
    public string? ReviewNote { get; set; }
    public DateTime? PublishedAt { get; set; }

    // Stats (PRD §77)
    public long ViewCount { get; set; }
    public long DownloadCount { get; set; }

    // Exam ↔ Solution (PRD §13): a solution document points at the exam it solves.
    public Guid? SolutionForDocumentId { get; set; }
    public Document? SolutionForDocument { get; set; }
    public ICollection<Document> Solutions { get; set; } = new List<Document>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
