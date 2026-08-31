namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

/// <summary>A semester within a level (e.g. "Semestre 1" / "S1").</summary>
public class Semester : AcademicEntity
{
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Ordering within the level (1 or 2), and globally 1..6 for a Licence.</summary>
    public int Order { get; set; }

    public Guid LevelId { get; set; }
    public Level? Level { get; set; }

    public ICollection<Module> Modules { get; set; } = new List<Module>();
}
