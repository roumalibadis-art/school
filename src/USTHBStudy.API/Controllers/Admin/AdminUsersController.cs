namespace USTHBStudy.API.Controllers.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.API.Controllers;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Admin.Validators;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;

/// <summary>Admin user management (PRD §33).</summary>
[Route("api/admin/users")]
public sealed class AdminUsersController : ApiControllerBase
{
    private readonly IUserAdminService _users;

    public AdminUsersController(IUserAdminService users) => _users = users;

    public sealed record GrantPremiumRequest(int Months);

    [HttpGet]
    [Authorize(Policy = Permissions.Users.View)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminUserDto>>>> List(
        [FromQuery] string? search, [FromQuery] string? role, [FromQuery] bool? isActive, [FromQuery] bool? isPremium,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse.Page(await _users.ListAsync(new AdminUserQuery(search, role, isActive, isPremium, page, pageSize), ct)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Users.View)]
    public async Task<ActionResult<ApiResponse<AdminUserDetailDto>>> Get(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _users.GetAsync(id, ct)));

    [HttpPut("{id:guid}/roles")]
    [Authorize(Policy = Permissions.Users.Update)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> SetRoles(Guid id, SetRolesRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _users.SetRolesAsync(id, request.Roles, ct), "Roles updated."));

    [HttpPost("{id:guid}/premium")]
    [Authorize(Policy = Permissions.Subscriptions.Manage)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> GrantPremium(Guid id, GrantPremiumRequest request, CancellationToken ct) =>
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
