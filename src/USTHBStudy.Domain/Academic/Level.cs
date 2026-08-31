namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

/// <summary>A study year within a specialty (e.g. "Licence 1" / "L1").</summary>
public class Level : AcademicEntity
{
    /// <summary>Short label such as "L1", "M2".</summary>
    public string ShortName { get; set; } = string.Empty;

    public StudyCycle Cycle { get; set; } = StudyCycle.Licence;

    /// <summary>Ordering within the specialty (1 = first year).</summary>
    public int Order { get; set; }

    public Guid SpecialtyId { get; set; }
    public Specialty? Specialty { get; set; }

    public ICollection<Semester> Semesters { get; set; } = new List<Semester>();
}
