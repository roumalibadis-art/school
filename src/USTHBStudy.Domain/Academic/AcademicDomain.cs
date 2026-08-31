namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

/// <summary>
/// A broad field of study ("Domaine" in the LMD system, e.g. <c>Mathématiques et Informatique</c>).
/// A classification that groups specialties; it is not on the main navigation path
/// (University → Faculty → Department → Specialty → Level → Semester → Module).
/// Named <c>AcademicDomain</c> to avoid clashing with the <c>USTHBStudy.Domain</c> namespace.
/// </summary>
public class AcademicDomain : AcademicEntity
{
    public string? Code { get; set; }

    public Guid FacultyId { get; set; }
    public Faculty? Faculty { get; set; }

    public ICollection<Specialty> Specialties { get; set; } = new List<Specialty>();
}
