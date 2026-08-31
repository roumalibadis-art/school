namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

public class Faculty : AcademicEntity
{
    public string? Code { get; set; }

    public Guid UniversityId { get; set; }
    public University? University { get; set; }

    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<AcademicDomain> Domains { get; set; } = new List<AcademicDomain>();
}
