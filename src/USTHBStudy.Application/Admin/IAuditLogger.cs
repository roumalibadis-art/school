namespace USTHBStudy.Application.Admin;

using USTHBStudy.Application.Common;

/// <summary>Writes administrative audit entries (PRD §42). Fire-and-persist; never throws to the caller path.</summary>
public interface IAuditLogger
{
    Task WriteAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? metadata = null,
        CancellationToken ct = default);
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct = default);
}

public sealed record AuditQuery(
    string? Action = null,
    string? EntityType = null,
    Guid? ActorId = null,
    int Page = 1,
    int PageSize = 30);

public sealed record AuditEntryDto(
    Guid Id,
    Guid? ActorId,
    string? ActorEmail,
    string Action,
    string EntityType,
    string? EntityId,
    string? Metadata,
    DateTime OccurredAt);
