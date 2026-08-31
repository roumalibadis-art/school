namespace USTHBStudy.Domain.Contributions;

using USTHBStudy.Domain.Common;
using USTHBStudy.Domain.Documents;

public enum ContributionStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>A student-submitted document awaiting moderation (PRD §37). Students cannot publish directly.</summary>
public class Contribution : AuditableEntity
{
    public Guid SubmittedById { get; set; }

    public string Title { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public Guid ModuleId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public string? Description { get; set; }

    public string FileStorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string FileHashSha256 { get; set; } = string.Empty;

    public ContributionStatus Status { get; set; } = ContributionStatus.Pending;
    public string? ReviewNote { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Set when a moderator approves and a document is created (PRD §38).</summary>
    public Guid? CreatedDocumentId { get; set; }
}
