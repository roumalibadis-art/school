namespace USTHBStudy.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;

/// <inheritdoc />
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;

    public IdentityService(UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<Result<AuthUser>> CreateStudentAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken ct = default)
    {
        if (await _users.FindByEmailAsync(email) is not null)
        {
            return Result.Failure<AuthUser>("An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = false,
            IsActive = true,
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return Result.Failure<AuthUser>(created.Errors.Select(e => e.Description).ToArray());
        }

        await _users.AddToRoleAsync(user, Roles.Student);
        return Result.Success(Map(user));
    }

    public async Task<Result<AuthUser>> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null)
        {
            return Result.Failure<AuthUser>("Invalid credentials.");
        }

        if (await _users.IsLockedOutAsync(user))
        {
            return Result.Failure<AuthUser>("This account is temporarily locked. Try again later.");
        }

        if (!await _users.CheckPasswordAsync(user, password))
        {
            await _users.AccessFailedAsync(user);
            return Result.Failure<AuthUser>("Invalid credentials.");
        }

        if (_users.SupportsUserLockout)
        {
            await _users.ResetAccessFailedCountAsync(user);
        }

        return Result.Success(Map(user));
    }

    public async Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        return user is null ? null : Map(user);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Array.Empty<string>();
        }

        var roles = await _users.GetRolesAsync(user);
        return roles.ToArray();
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Array.Empty<string>();
        }

        var roleNames = await _users.GetRolesAsync(user);
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleName in roleNames)
        {
            var role = await _roles.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            foreach (var claim in await _roles.GetClaimsAsync(role))
            {
                if (claim.Type == Permissions.ClaimType)
                {
                    permissions.Add(claim.Value);
                }
            }
        }

        return permissions.ToArray();
    }

    private static AuthUser Map(ApplicationUser user) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.FirstName,
        user.LastName,
        user.IsActive,
        user.IsPremium,
        user.PremiumExpiresAt);
}
