namespace USTHBStudy.API.Controllers;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using USTHBStudy.API.Extensions;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Auth;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Auth.Google;
using USTHBStudy.Application.Common;
using USTHBStudy.Domain.Classification;

/// <summary>
/// "Sign in with Google": OAuth 2.0 authorization-code flow with PKCE (S256), <c>state</c> and <c>nonce</c>,
/// OpenID Connect ID-token validation, and a single-use ticket handoff to the frontend so no access token ever
/// appears in a URL. Sign-out is the ordinary <c>POST /api/auth/logout</c> (revokes our refresh token).
/// </summary>
[Route("api/auth")]
[EnableRateLimiting(ApiServiceExtensions.AuthRateLimitPolicy)]
public sealed class GoogleAuthController : ApiControllerBase
{
    private const string CookieName = "g_oauth";
    private const string Purpose = "USTHBStudy.GoogleOAuth.v1";
    private static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(60);

    private readonly IGoogleOidcClient _google;
    private readonly IExternalAccountService _accounts;
    private readonly IExternalLoginTicketService _tickets;
    private readonly IAuthService _auth;
    private readonly IDataProtector _protector;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly GoogleOptions _options;

    public GoogleAuthController(
        IGoogleOidcClient google,
        IExternalAccountService accounts,
        IExternalLoginTicketService tickets,
        IAuthService auth,
        IDataProtectionProvider dataProtection,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IOptions<GoogleOptions> options)
    {
        _google = google;
        _accounts = accounts;
        _tickets = tickets;
        _auth = auth;
        _protector = dataProtection.CreateProtector(Purpose);
        _clock = clock;
        _currentUser = currentUser;
        _options = options.Value;
    }

    /// <summary>Which sign-in providers are configured — the UI hides Google when it is off (no dead buttons).</summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<GoogleProvidersDto>> Providers() =>
        Ok(ApiResponse.Data(new GoogleProvidersDto(_google.Enabled)));

    /// <summary>Begins the flow: stores state/nonce/PKCE verifier in a short-lived protected cookie, redirects to Google.</summary>
    [HttpGet("google/start")]
    [AllowAnonymous]
    public async Task<IActionResult> Start([FromQuery] string? returnUrl, [FromQuery] string? link, CancellationToken ct)
    {
        if (!_google.Enabled)
        {
            return NotFound(ApiResponse.Fail("Google sign-in is not configured."));
        }

        Guid? linkUserId = null;
        if (!string.IsNullOrEmpty(link))
        {
            linkUserId = await _tickets.RedeemAsync(link, ExternalTicketPurpose.Link, ct);
            if (linkUserId is null)
            {
                return Redirect(FrontendUrl("/profile?google=link_expired"));
            }
        }

        var state = RandomToken();
        var nonce = RandomToken();
        var verifier = RandomToken();
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var flow = new FlowState(state, nonce, verifier, SafeReturnPath(returnUrl), linkUserId, _clock.UtcNow.Add(FlowLifetime));
        Response.Cookies.Append(CookieName, _protector.Protect(JsonSerializer.Serialize(flow)), new CookieOptions
        {
            HttpOnly = true,
            // Behind a TLS-terminating proxy Request.IsHttps is false, so also trust an https public origin.
            Secure = Request.IsHttps || _options.FrontendBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Lax, // must survive the top-level redirect back from Google
            Path = "/api/auth/google",
            MaxAge = FlowLifetime,
            IsEssential = true,
        });

        return Redirect(_google.BuildAuthorizationUrl(state, nonce, challenge));
    }

    /// <summary>Google redirects here. Validates state, exchanges the code, validates the ID token, resolves the account.</summary>
    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        var flow = ReadAndClearFlow();
        if (flow is null || state is null || !FixedEquals(flow.State, state) || flow.ExpiresAt <= _clock.UtcNow)
        {
            return Redirect(FrontendUrl("/login?error=google_state"));
        }

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            return Redirect(FrontendUrl(flow.LinkUserId is null ? "/login?error=google_cancelled" : "/profile?google=cancelled"));
        }

        GoogleIdentity identity;
        try
        {
            identity = await _google.ExchangeAsync(code, flow.Verifier, flow.Nonce, ct);
        }
        catch (AppException)
        {
            return Redirect(FrontendUrl(flow.LinkUserId is null ? "/login?error=google_failed" : "/profile?google=failed"));
        }

        if (flow.LinkUserId is { } linkUserId)
        {
            if (!identity.EmailVerified)
            {
                return Redirect(FrontendUrl("/profile?google=unverified"));
            }

            var status = await _accounts.LinkGoogleAsync(linkUserId, identity, ct);
            return Redirect(FrontendUrl("/profile?google=" + status switch
            {
                LinkStatus.Linked => "linked",
                LinkStatus.AlreadyLinked => "already_linked",
                LinkStatus.LinkedToAnotherAccount => "in_use",
                _ => "failed",
            }));
        }

        var result = await _accounts.SignInWithGoogleAsync(identity, ct);
        switch (result.Status)
        {
            case ExternalSignInStatus.SignedIn:
            case ExternalSignInStatus.Created:
                var ticket = await _tickets.IssueAsync(result.UserId!.Value, ExternalTicketPurpose.Login, TicketLifetime, ct);
                return Redirect(FrontendUrl($"/auth/google/complete?ticket={Uri.EscapeDataString(ticket)}&returnUrl={Uri.EscapeDataString(flow.ReturnUrl)}"));
            case ExternalSignInStatus.Blocked:
                return Redirect(FrontendUrl("/login?error=account_disabled"));
            case ExternalSignInStatus.EmailNotVerified:
                return Redirect(FrontendUrl("/login?error=google_unverified"));
            default:
                return Redirect(FrontendUrl("/login?error=google_link_required"));
        }
    }

    /// <summary>Trades the single-use ticket for the normal access + refresh token pair.</summary>
    [HttpPost("google/exchange")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Exchange(ExchangeTicketRequest request, CancellationToken ct)
    {
        var userId = await _tickets.RedeemAsync(request.Ticket, ExternalTicketPurpose.Login, ct)
                     ?? throw new UnauthorizedAppException("This sign-in link is invalid or has expired.");
        return Ok(ApiResponse.Data(await _auth.SignInExternalAsync(userId, ClientIp, ct)));
    }

    /// <summary>Authenticated users link Google from their profile: issues a one-time ticket bound to them.</summary>
    [HttpPost("google/link")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GoogleLinkStartDto>>> LinkStart(CancellationToken ct)
    {
        if (!_google.Enabled)
        {
            return NotFound(ApiResponse.Fail("Google sign-in is not configured."));
        }

        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var ticket = await _tickets.IssueAsync(userId, ExternalTicketPurpose.Link, TimeSpan.FromMinutes(5), ct);
        return Ok(ApiResponse.Data(new GoogleLinkStartDto($"/api/auth/google/start?link={Uri.EscapeDataString(ticket)}")));
    }

    [HttpGet("google/status")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> LinkStatusAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        return Ok(ApiResponse.Data<object>(new { enabled = _google.Enabled, linked = await _accounts.IsGoogleLinkedAsync(userId, ct) }));
    }

    // ---------------------------------------------------------------- helpers

    private sealed record FlowState(string State, string Nonce, string Verifier, string ReturnUrl, Guid? LinkUserId, DateTime ExpiresAt);

    private FlowState? ReadAndClearFlow()
    {
        var raw = Request.Cookies[CookieName];
        Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/api/auth/google" });
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FlowState>(_protector.Unprotect(raw));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null; // tampered or from another key ring
        }
    }

    private string FrontendUrl(string path) => _options.FrontendBaseUrl.TrimEnd('/') + path;

    /// <summary>Only same-site absolute paths — never a URL — so the flow cannot be used as an open redirect.</summary>
    internal static string SafeReturnPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 200 || path[0] != '/'
            || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') || path.Any(char.IsControl))
        {
            return "/dashboard";
        }

        return path;
    }

    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
