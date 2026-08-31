namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

/// <summary>An academic year such as "2024-2025" (PRD §10). Independent of the hierarchy.</summary>
public class AcademicYear : AcademicEntity
{
    public int StartYear { get; set; }
    public int EndYear { get; set; }

    /// <summary>Exactly one year is normally flagged current; enforced by the service, not the schema.</summary>
    public bool IsCurrent { get; set; }
}
