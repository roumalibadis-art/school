namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

/// <summary>Top of the academic hierarchy (PRD §10). Multi-university ready (PRD §67).</summary>
public class University : AcademicEntity
{
    public string? Code { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = "Algeria";

    public ICollection<Faculty> Faculties { get; set; } = new List<Faculty>();
}
