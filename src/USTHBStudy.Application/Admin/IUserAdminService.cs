namespace USTHBStudy.Application.Admin;

using USTHBStudy.Application.Common;

/// <summary>Admin user management (PRD §33).</summary>
public interface IUserAdminService
{
    Task<PagedResult<AdminUserDto>> ListAsync(AdminUserQuery query, CancellationToken ct = default);

    Task<AdminUserDetailDto> GetAsync(Guid userId, CancellationToken ct = default);

    Task<AdminUserDto> SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct = default);

    Task<AdminUserDto> GrantPremiumAsync(Guid userId, int months, CancellationToken ct = default);

    Task<AdminUserDto> RevokePremiumAsync(Guid userId, CancellationToken ct = default);

    Task<AdminUserDto> SetActiveAsync(Guid userId, bool active, CancellationToken ct = default);
}

public sealed record AdminUserQuery(
    string? Search = null,
    string? Role = null,
    bool? IsActive = null,
    bool? IsPremium = null,
    int Page = 1,
    int PageSize = 25);

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool IsPremium,
    DateTime? PremiumExpiresAt,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);

public sealed record AdminUserDetailDto(
    AdminUserDto User,
    string? StudentId,
    string? SpecialtyName,
    string? LevelName,
    int FavoritesCount,
    int SubscriptionsCount,
    DateTime? LastActivityAt);
