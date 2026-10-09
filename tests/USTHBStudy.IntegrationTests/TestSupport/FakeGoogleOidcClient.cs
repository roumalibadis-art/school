namespace USTHBStudy.IntegrationTests.TestSupport;

using System.Security.Cryptography;
using System.Text;
using USTHBStudy.Application.Auth.Google;
using USTHBStudy.Application.Common;

/// <summary>
/// Stands in for Google's endpoints but enforces the same contracts the real flow relies on: the code
/// exchange must present the PKCE verifier matching the challenge from the authorize URL, and the ID token's
/// nonce must be the one that was sent. Identities are keyed by authorization code.
/// </summary>
public sealed class FakeGoogleOidcClient : IGoogleOidcClient
{
    private readonly Dictionary<string, GoogleIdentity> _codes = new();
    private string? _challenge;
    private string? _nonce;

    public bool Enabled => true;

    public int ExchangeCalls { get; private set; }

    public void Register(string code, GoogleIdentity identity) => _codes[code] = identity;

    public string BuildAuthorizationUrl(string state, string nonce, string codeChallenge)
    {
        _challenge = codeChallenge;
        _nonce = nonce;
        return $"https://accounts.google.test/o/oauth2/v2/auth?state={Uri.EscapeDataString(state)}"
               + $"&nonce={Uri.EscapeDataString(nonce)}&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256";
    }

    public Task<GoogleIdentity> ExchangeAsync(string code, string codeVerifier, string expectedNonce, CancellationToken ct = default)
    {
        ExchangeCalls++;
        var expectedChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        if (!_codes.TryGetValue(code, out var identity) || expectedChallenge != _challenge || expectedNonce != _nonce)
        {
            throw new UnauthorizedAppException("Google sign-in could not be completed.");
        }

        return Task.FromResult(identity);
    }
}
