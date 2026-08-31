namespace USTHBStudy.Domain.Common;

/// <summary>Entity with automatic UTC create/update timestamps.</summary>
public abstract class AuditableEntity : BaseEntity, IAuditable
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
