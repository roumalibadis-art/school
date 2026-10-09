namespace USTHBStudy.Infrastructure.Auth.Google;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using USTHBStudy.Application.Auth.Google;
using USTHBStudy.Application.Common;

/// <summary>Talks to Google's token and JWKS endpoints. Server-side only: the client secret never reaches the browser.</summary>
public sealed class GoogleOidcClient : IGoogleOidcClient
{
    private static readonly TimeSpan KeyCacheTtl = TimeSpan.FromHours(1);

    private readonly GoogleOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<GoogleOidcClient> _logger;
    private readonly SemaphoreSlim _keyLock = new(1, 1);
    private IReadOnlyCollection<SecurityKey> _keys = Array.Empty<SecurityKey>();
    private DateTime _keysFetchedAt = DateTime.MinValue;

    public GoogleOidcClient(IOptions<GoogleOptions> options, HttpClient http, ILogger<GoogleOidcClient> logger)
    {
        _options = options.Value;
        _http = http;
        _logger = logger;
    }

    public bool Enabled => _options.Enabled;

    public string BuildAuthorizationUrl(string state, string nonce, string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account",
        };

        return _options.AuthorizationEndpoint + "?" + string.Join('&',
            query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<GoogleIdentity> ExchangeAsync(
        string code, string codeVerifier, string expectedNonce, CancellationToken ct = default)
    {
        using var response = await _http.PostAsync(_options.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier,
        }), ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google token exchange failed with HTTP {Status}.", (int)response.StatusCode);
            throw new UnauthorizedAppException("Google sign-in could not be completed.");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(token?.IdToken))
        {
            throw new UnauthorizedAppException("Google sign-in could not be completed.");
        }

        var validator = new GoogleIdTokenValidator(_options.ClientId);
        try
        {
            return validator.Validate(token.IdToken, await GetKeysAsync(false, ct), expectedNonce);
        }
        catch (UnauthorizedAppException)
        {
            // Key rotation: retry once against freshly fetched keys before giving up.
            return validator.Validate(token.IdToken, await GetKeysAsync(true, ct), expectedNonce);
        }
    }

    private async Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && DateTime.UtcNow - _keysFetchedAt < KeyCacheTtl && _keys.Count > 0)
        {
            return _keys;
        }

        await _keyLock.WaitAsync(ct);
        try
        {
            var json = await _http.GetStringAsync(_options.JwksUri, ct);
            _keys = new JsonWebKeySet(json).GetSigningKeys().ToArray();
            _keysFetchedAt = DateTime.UtcNow;
            return _keys;
        }
        finally
        {
            _keyLock.Release();
        }
    }

    private sealed record TokenResponse([property: JsonPropertyName("id_token")] string? IdToken);
}
