namespace USTHBStudy.Infrastructure.Persistence.Entities;

using USTHBStudy.Infrastructure.Identity;

/// <summary>
/// A persisted refresh token (PRD §44). Only the SHA-256 hash of the token is stored; tokens are
/// single-use and rotate on every refresh, with <see cref="ReplacedByTokenHash"/> forming the chain.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public string? RevokedByIp { get; set; }

    public ApplicationUser? User { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
