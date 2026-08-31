namespace USTHBStudy.Domain.Common;

/// <summary>
/// Base for the academic-hierarchy entities (PRD §10). Every node has a display name, a stable
/// unique slug (PRD §54), an active flag, audit timestamps and soft-delete support (PRD §74).
/// </summary>
public abstract class AcademicEntity : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
