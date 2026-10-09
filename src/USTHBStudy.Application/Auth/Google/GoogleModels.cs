namespace USTHBStudy.Application.Auth.Google;

using USTHBStudy.Domain.Classification;

/// <summary>
/// Google OpenID Connect settings (<c>Authentication:Google</c>). The client secret is read from user-secrets /
/// environment variables only — never from a committed file. Google sign-in is switched off unless
/// <see cref="ClientId"/>, <see cref="ClientSecret"/> and <see cref="RedirectUri"/> are all set.
/// </summary>
public sealed class GoogleOptions
{
    public const string SectionName = "Authentication:Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Must exactly match an "Authorised redirect URI" registered in Google Cloud Console.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Where the API sends the browser after the callback (the Next.js site).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";

    public string AuthorizationEndpoint { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";
    public string JwksUri { get; set; } = "https://www.googleapis.com/oauth2/v3/certs";

    public bool Enabled =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RedirectUri);
}

/// <summary>The verified claims we use from a validated Google ID token.</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? GivenName, string? FamilyName);

public interface IGoogleOidcClient
{
    bool Enabled { get; }

    string BuildAuthorizationUrl(string state, string nonce, string codeChallenge);

    /// <summary>Exchanges the code (with the PKCE verifier) and fully validates the ID token. Throws on any failure.</summary>
    Task<GoogleIdentity> ExchangeAsync(string code, string codeVerifier, string expectedNonce, CancellationToken ct = default);
}

public enum ExternalSignInStatus
{
    /// <summary>Known Google identity (or safely linked) — proceed to issue tokens.</summary>
    SignedIn,

    /// <summary>A new student account was created.</summary>
    Created,

    /// <summary>An account with this email exists but cannot be linked automatically.</summary>
    LinkRequired,

    /// <summary>The account is suspended.</summary>
    Blocked,

    /// <summary>The Google email is not verified.</summary>
    EmailNotVerified,
}

public sealed record ExternalSignInResult(ExternalSignInStatus Status, Guid? UserId);

public enum LinkStatus
{
    Linked,
    AlreadyLinked,
    LinkedToAnotherAccount,
    UserNotFound,
    Blocked,
}

public interface IExternalAccountService
{
    /// <summary>
    /// Resolves a validated Google identity to a local account. Links by verified email only when that is safe
    /// (confirmed email, non-privileged account); never elevates privileges.
    /// </summary>
    Task<ExternalSignInResult> SignInWithGoogleAsync(GoogleIdentity identity, CancellationToken ct = default);

    /// <summary>Links the identity to an account that is already authenticated.</summary>
    Task<LinkStatus> LinkGoogleAsync(Guid userId, GoogleIdentity identity, CancellationToken ct = default);

    Task<bool> IsGoogleLinkedAsync(Guid userId, CancellationToken ct = default);
}

public interface IExternalLoginTicketService
{
    /// <summary>Issues a single-use opaque ticket; only its SHA-256 hash is stored.</summary>
    Task<string> IssueAsync(Guid userId, ExternalTicketPurpose purpose, TimeSpan lifetime, CancellationToken ct = default);

    /// <summary>Atomically consumes the ticket. Returns the user id, or <c>null</c> if unknown, used, expired or of another purpose.</summary>
    Task<Guid?> RedeemAsync(string ticket, ExternalTicketPurpose purpose, CancellationToken ct = default);
}

public sealed record GoogleProvidersDto(bool Google);

public sealed record ExchangeTicketRequest(string Ticket);

public sealed record GoogleLinkStartDto(string Url);
