namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

public class Department : AcademicEntity
{
    public string? Code { get; set; }

    public Guid FacultyId { get; set; }
    public Faculty? Faculty { get; set; }

    public ICollection<Specialty> Specialties { get; set; } = new List<Specialty>();
}
