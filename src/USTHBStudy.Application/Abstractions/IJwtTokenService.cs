namespace USTHBStudy.Application.Abstractions;

using USTHBStudy.Application.Auth.Dtos;

/// <summary>
/// Issues signed JWT access tokens and opaque refresh tokens (PRD §44). Refresh tokens are
/// returned in plaintext to the client once and stored only as a hash (see <see cref="HashRefreshToken"/>).
/// </summary>
public interface IJwtTokenService
{
    AccessToken CreateAccessToken(TokenUser user);

    /// <summary>A cryptographically-random, URL-safe opaque string.</summary>
    string CreateRefreshToken();

    /// <summary>Deterministic SHA-256 hash (hex) used as the stored/lookup form of a refresh token.</summary>
    string HashRefreshToken(string refreshToken);
}
