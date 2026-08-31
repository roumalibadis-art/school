namespace USTHBStudy.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Infrastructure.Persistence.Entities;

/// <inheritdoc />
public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public RefreshTokenStore(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task AddAsync(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        string? createdByIp,
        CancellationToken ct = default)
    {
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = _clock.UtcNow,
            CreatedByIp = createdByIp,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StoredRefreshToken?> FindActiveAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

        if (token is null || !token.IsActive(_clock.UtcNow))
        {
            return null;
        }

        return new StoredRefreshToken(token.Id, token.UserId, token.ExpiresAtUtc, token.RevokedAtUtc);
    }

    public async Task RotateAsync(
        string oldTokenHash,
        string newTokenHash,
        Guid userId,
        DateTime newExpiresAtUtc,
        string? ip,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var old = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == oldTokenHash, ct);
        if (old is not null && old.RevokedAtUtc is null)
        {
            old.RevokedAtUtc = now;
            old.RevokedByIp = ip;
            old.ReplacedByTokenHash = newTokenHash;
        }

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = newTokenHash,
            ExpiresAtUtc = newExpiresAtUtc,
            CreatedAtUtc = now,
            CreatedByIp = ip,
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(string tokenHash, string? ip, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
        if (token is null || token.RevokedAtUtc is not null)
        {
            return;
        }

        token.RevokedAtUtc = _clock.UtcNow;
        token.RevokedByIp = ip;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var active = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(ct);

        if (active.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        foreach (var token in active)
        {
            token.RevokedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
    }
}
