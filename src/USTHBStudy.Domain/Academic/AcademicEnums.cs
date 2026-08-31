namespace USTHBStudy.Domain.Academic;

/// <summary>Higher-education cycle a <see cref="Level"/> belongs to (LMD system).</summary>
public enum StudyCycle
{
    Licence = 1,
    Master = 2,
    Doctorat = 3,
    Engineer = 4,
}

/// <summary>
/// Kind of exam session a <see cref="Session"/> represents (PRD §13). Stored as an entity rather
/// than a bare enum because admins manage the list and documents reference it.
/// </summary>
public enum SessionKind
{
    Normal = 1,
    Retake = 2,
    ContinuousAssessment = 3,
    MakeUp = 4,
    Other = 99,
}
