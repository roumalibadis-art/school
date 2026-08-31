namespace USTHBStudy.Infrastructure.Documents;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Documents;

/// <summary>
/// HMAC-SHA256 signed download tokens. Payload: <c>key|docId|userId|expiryUnix</c> (base64url), then
/// <c>.</c> then the base64url signature. Verified with a constant-time compare.
/// </summary>
public sealed class HmacDownloadTokenService : IDownloadTokenService
{
    private readonly byte[] _key;
    private readonly IDateTimeProvider _clock;

    public HmacDownloadTokenService(IConfiguration configuration, IDateTimeProvider clock)
    {
        var secret = configuration["Downloads:SigningKey"]
                     ?? configuration["Jwt:Secret"]
                     ?? throw new InvalidOperationException("Downloads:SigningKey or Jwt:Secret must be configured.");
        _key = Encoding.UTF8.GetBytes(secret);
        _clock = clock;
    }

    public string Issue(DownloadGrant grant)
    {
        var payload = string.Join(
            '|',
            Convert.ToBase64String(Encoding.UTF8.GetBytes(grant.StorageKey)),
            grant.DocumentId.ToString("N"),
            grant.UserId?.ToString("N") ?? string.Empty,
            new DateTimeOffset(grant.ExpiresAtUtc, TimeSpan.Zero).ToUnixTimeSeconds());

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(_key, payloadBytes);

        return $"{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    public DownloadGrant? Verify(string token)
    {
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
        {
            return null;
        }

        byte[] payloadBytes;
        byte[] providedSignature;
        try
        {
            payloadBytes = FromBase64Url(token[..dot]);
            providedSignature = FromBase64Url(token[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expectedSignature = HMACSHA256.HashData(_key, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return null;
        }

        var parts = Encoding.UTF8.GetString(payloadBytes).Split('|');
        if (parts.Length != 4
            || !Guid.TryParseExact(parts[1], "N", out var documentId)
            || !long.TryParse(parts[3], out var expiryUnix))
        {
            return null;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiryUnix).UtcDateTime;
        if (expiresAt <= _clock.UtcNow)
        {
            return null;
        }

        var storageKey = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
        Guid? userId = Guid.TryParseExact(parts[2], "N", out var uid) ? uid : null;

        return new DownloadGrant(storageKey, documentId, userId, expiresAt);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }
}
