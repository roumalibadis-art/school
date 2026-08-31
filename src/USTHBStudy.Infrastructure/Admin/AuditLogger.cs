namespace USTHBStudy.Infrastructure.Admin;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Common;
using USTHBStudy.Domain.Admin;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>Writes audit rows (PRD §42). Failures are logged, never surfaced to the caller.</summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(AppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, ILogger<AuditLogger> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action, string entityType, string? entityId = null, object? metadata = null, CancellationToken ct = default)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = _currentUser.UserId,
                ActorEmail = _currentUser.Email,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
                OccurredAt = _clock.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit entry {Action} on {EntityType}", action, entityType);
        }
    }
}

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly AppDbContext _db;

    public AuditQueryService(AppDbContext db) => _db = db;

    public async Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };

        var q = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            q = q.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            q = q.Where(a => a.EntityType == query.EntityType);
        }

        if (query.ActorId is { } actorId)
        {
            q = q.Where(a => a.ActorId == actorId);
        }

        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.OccurredAt)
            .Skip(paging.Skip).Take(paging.Take)
            .Select(a => new AuditEntryDto(
                a.Id, a.ActorId, a.ActorEmail, a.Action, a.EntityType, a.EntityId, a.Metadata, a.OccurredAt))
            .ToListAsync(ct);

        return new PagedResult<AuditEntryDto>(items, paging.Page, paging.PageSize, total);
    }
}
