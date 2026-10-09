namespace USTHBStudy.Infrastructure.Auth.Google;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using USTHBStudy.Application.Auth.Google;
using USTHBStudy.Application.Common;

/// <summary>
/// Validates a Google ID token per OpenID Connect Core §3.1.3.7: signature (against the supplied JWKS keys),
/// issuer, audience, lifetime, and the nonce we sent. Pure and clock-injectable so every failure mode is unit-tested.
/// </summary>
public sealed class GoogleIdTokenValidator
{
    private static readonly string[] ValidIssuers = { "https://accounts.google.com", "accounts.google.com" };

    private readonly string _clientId;
    private readonly Func<DateTime> _utcNow;

    public GoogleIdTokenValidator(string clientId, Func<DateTime>? utcNow = null)
    {
        _clientId = clientId;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public GoogleIdentity Validate(string idToken, IEnumerable<SecurityKey> signingKeys, string expectedNonce)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = ValidIssuers,
            ValidateAudience = true,
            ValidAudience = _clientId,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                (notBefore is null || notBefore <= _utcNow().AddSeconds(30)) && expires is not null && expires > _utcNow().AddSeconds(-30),
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(idToken, parameters, out _);

            var nonce = principal.FindFirst("nonce")?.Value;
            if (nonce is null || !FixedTimeEquals(nonce, expectedNonce))
            {
                throw new UnauthorizedAppException("Google sign-in could not be verified (nonce mismatch).");
            }

            var subject = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
            {
                throw new UnauthorizedAppException("Google did not return the required profile information.");
            }

            var verified = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            return new GoogleIdentity(
                subject, email.Trim(), verified, principal.FindFirst("given_name")?.Value, principal.FindFirst("family_name")?.Value);
        }
        catch (SecurityTokenException)
        {
            // Includes bad signature, wrong issuer/audience, expiry, unsupported algorithm.
            throw new UnauthorizedAppException("Google sign-in could not be verified.");
        }
        catch (ArgumentException)
        {
            throw new UnauthorizedAppException("Google sign-in could not be verified.");
        }
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
