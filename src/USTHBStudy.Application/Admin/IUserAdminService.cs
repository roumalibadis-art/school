namespace USTHBStudy.Application.Admin;

/// <summary>
/// The minimal admin user operations needed for Premium enforcement (PRD §24/§33). Full user
/// management (search, filter, roles, subscriptions) is Phase 7.
/// </summary>
public interface IUserAdminService
{
    Task<AdminUserDto> GrantPremiumAsync(Guid userId, int months, CancellationToken ct = default);

    Task<AdminUserDto> RevokePremiumAsync(Guid userId, CancellationToken ct = default);

    Task<AdminUserDto> SetActiveAsync(Guid userId, bool active, CancellationToken ct = default);
}

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool IsPremium,
    DateTime? PremiumExpiresAt);
