namespace USTHBStudy.Infrastructure.Documents;

/// <summary>Bound from the <c>Documents</c> configuration section.</summary>
public sealed class DocumentOptions
{
    public const string SectionName = "Documents";

    /// <summary>Hard cap on uploaded file size (PRD §36). Default 50 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Lifetime of a brokered download link (PRD §29). Default 120 s.</summary>
    public int DownloadLinkSeconds { get; set; } = 120;
}
