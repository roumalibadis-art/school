namespace USTHBStudy.Domain.Common;

/// <summary>
/// Marks an entity that is soft-deleted (kept in the table, hidden by a global query filter)
/// rather than physically removed. Applied to content that must remain recoverable
/// (documents, academic entities). See <c>PRD.md</c> §74/§80.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
