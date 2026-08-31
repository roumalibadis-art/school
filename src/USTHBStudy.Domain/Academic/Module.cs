namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

/// <summary>A course module (PRD §11). Belongs to one semester and one specialty.</summary>
public class Module : AcademicEntity
{
    public string? Code { get; set; }
    public string? Description { get; set; }

    /// <summary>Weight in the semester average (PRD §11).</summary>
    public decimal Coefficient { get; set; } = 1m;

    /// <summary>ECTS-style credits (PRD §11).</summary>
    public int Credits { get; set; }

    public Guid SemesterId { get; set; }
    public Semester? Semester { get; set; }

    public Guid SpecialtyId { get; set; }
    public Specialty? Specialty { get; set; }
}
