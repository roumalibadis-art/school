namespace USTHBStudy.Infrastructure.Persistence;

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Infrastructure.Identity;

/// <summary>
/// Idempotent seeding (PRD §52). Roles and their permission claims are always ensured;
/// demo accounts are seeded only when explicitly enabled (Development).
/// </summary>
public sealed class DbSeeder
{
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        RoleManager<ApplicationRole> roles,
        UserManager<ApplicationUser> users,
        ILogger<DbSeeder> logger)
    {
        _roles = roles;
        _users = users;
        _logger = logger;
    }

    public async Task SeedAsync(bool seedDemoUsers, CancellationToken ct = default)
    {
        await SeedRolesAndPermissionsAsync();

        if (seedDemoUsers)
        {
            await SeedDemoUsersAsync();
        }
    }

    private async Task SeedRolesAndPermissionsAsync()
    {
        foreach (var (roleName, permissions) in Permissions.DefaultRoleGrants)
        {
            var role = await _roles.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ApplicationRole(roleName) { Description = $"{roleName} role" };
                var created = await _roles.CreateAsync(role);
                if (!created.Succeeded)
                {
                    _logger.LogError(
                        "Failed to create role {Role}: {Errors}",
                        roleName,
                        string.Join(", ", created.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            var existing = (await _roles.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permission in permissions.Where(p => !existing.Contains(p)))
            {
                await _roles.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
            }
        }
    }

    private async Task SeedDemoUsersAsync()
    {
        await EnsureUserAsync("admin@example.local", "Amine", "Admin", "Admin#2026!", Roles.Admin);
        await EnsureUserAsync("moderator@example.local", "Mira", "Moderator", "Moderator#2026!", Roles.Moderator);
        await EnsureUserAsync("student@example.local", "Sami", "Student", "Student#2026!", Roles.Student);
    }

    private async Task EnsureUserAsync(string email, string firstName, string lastName, string password, string role)
    {
        if (await _users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
            IsActive = true,
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            _logger.LogWarning(
                "Failed to seed demo user {Email}: {Errors}",
                email,
                string.Join(", ", created.Errors.Select(e => e.Description)));
            return;
        }

        await _users.AddToRoleAsync(user, role);
        _logger.LogInformation("Seeded demo {Role} account {Email}", role, email);
    }
}
