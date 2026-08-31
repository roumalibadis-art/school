namespace USTHBStudy.Domain.Common;

/// <summary>
/// Marks an entity whose create/update timestamps are maintained automatically by the
/// persistence layer (see the auditing interceptor in Infrastructure).
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
