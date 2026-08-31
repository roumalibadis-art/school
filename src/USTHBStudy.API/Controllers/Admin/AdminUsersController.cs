namespace USTHBStudy.API.Controllers.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.API.Controllers;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;

/// <summary>
/// Minimal admin user operations for Premium and suspension (PRD §24/§33). Full user management is Phase 7.
/// </summary>
[Route("api/admin/users")]
public sealed class AdminUsersController : ApiControllerBase
{
    private readonly IUserAdminService _users;

    public AdminUsersController(IUserAdminService users) => _users = users;

    public sealed record GrantPremiumRequest(int Months);

    [HttpPost("{id:guid}/premium")]
    [Authorize(Policy = Permissions.Subscriptions.Manage)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> GrantPremium(
        Guid id, GrantPremiumRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _users.GrantPremiumAsync(id, request.Months, ct), "Premium granted."));

    [HttpDelete("{id:guid}/premium")]
    [Authorize(Policy = Permissions.Subscriptions.Manage)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> RevokePremium(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _users.RevokePremiumAsync(id, ct), "Premium revoked."));

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = Permissions.Users.Suspend)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> Suspend(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _users.SetActiveAsync(id, active: false, ct), "Account suspended."));

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = Permissions.Users.Suspend)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> Restore(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _users.SetActiveAsync(id, active: true, ct), "Account restored."));
}
