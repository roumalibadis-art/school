namespace USTHBStudy.Domain.Documents;

using USTHBStudy.Domain.Common;

public enum ReportReason
{
    WrongModule = 1,
    WrongYear = 2,
    Unreadable = 3,
    Duplicate = 4,
    IncorrectInformation = 5,
    Copyright = 6,
    Other = 99,
}

public enum ReportStatus
{
    Open = 1,
    Resolved = 2,
    Dismissed = 3,
}

/// <summary>A user report about a document (PRD §39), including copyright complaints (PRD §40).</summary>
public class DocumentReport : AuditableEntity
{
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public Guid? ReporterId { get; set; }

    public ReportReason Reason { get; set; }
    public string? Comment { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
