namespace USTHBStudy.Domain.Common;

/// <summary>Base type for all persistent entities. Uses a client-generated GUID key.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
