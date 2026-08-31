namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

public class Specialty : AcademicEntity
{
    public string? Code { get; set; }
    public string? Description { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Optional classification link (PRD §10 lists Domain as a managed entity).</summary>
    public Guid? AcademicDomainId { get; set; }
    public AcademicDomain? AcademicDomain { get; set; }

    public ICollection<Level> Levels { get; set; } = new List<Level>();
    public ICollection<Module> Modules { get; set; } = new List<Module>();
}
