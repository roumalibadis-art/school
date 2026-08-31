namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using USTHBStudy.API.Extensions;
using USTHBStudy.Application.Auth;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Common;

[AllowAnonymous]
[EnableRateLimiting(ApiServiceExtensions.AuthRateLimitPolicy)]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Registers a new student account (PRD §19 — academic profile is added later via PUT /api/me).</summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ClientIp, ct);
        return Ok(ApiResponse.Data(result, "Account created."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ClientIp, ct);
        return Ok(ApiResponse.Data(result));
    }

    /// <summary>Exchanges a valid refresh token for a new token pair; the presented token is revoked (PRD §44).</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(request, ClientIp, ct);
        return Ok(ApiResponse.Data(result));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> Logout(LogoutRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request, ClientIp, ct);
        return Ok(ApiResponse.Ok("Logged out."));
    }
}
