namespace USTHBStudy.Domain.Academic;

using USTHBStudy.Domain.Common;

/// <summary>
/// An exam session type — "Session normale", "Rattrapage", "Contrôle continu"… (PRD §13).
/// Documents (Phase 3) reference a session; admins manage the list (PRD §34).
/// </summary>
public class Session : AcademicEntity
{
    public SessionKind Kind { get; set; } = SessionKind.Normal;

    public int Order { get; set; }
}
