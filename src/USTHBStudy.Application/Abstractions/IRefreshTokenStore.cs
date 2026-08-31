namespace USTHBStudy.Application.Abstractions;

/// <summary>
/// Persistence for refresh tokens (PRD §44). Tokens are stored as hashes only, are single-use,
/// and rotate on every refresh; the replaced-by chain supports reuse detection.
/// </summary>
public interface IRefreshTokenStore
{
    Task AddAsync(Guid userId, string tokenHash, DateTime expiresAtUtc, string? createdByIp, CancellationToken ct = default);

    /// <summary>Returns the token if it exists, is not revoked, and has not expired; otherwise null.</summary>
    Task<StoredRefreshToken?> FindActiveAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Revokes <paramref name="oldTokenHash"/> and stores the new token in one unit of work.</summary>
    Task RotateAsync(
        string oldTokenHash,
        string newTokenHash,
        Guid userId,
        DateTime newExpiresAtUtc,
        string? ip,
        CancellationToken ct = default);

    Task RevokeAsync(string tokenHash, string? ip, CancellationToken ct = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed record StoredRefreshToken(Guid Id, Guid UserId, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc);
