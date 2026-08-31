namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Auth;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Common;

[Authorize]
[Route("api/me")]
public sealed class MeController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;

    public MeController(IAuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    /// <summary>Current user profile, roles and Premium status (PRD §20).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Get(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var user = await _auth.GetCurrentAsync(userId, ct);
        return Ok(ApiResponse.Data(user));
    }
}
