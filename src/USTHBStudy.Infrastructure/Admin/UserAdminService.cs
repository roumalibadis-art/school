namespace USTHBStudy.Infrastructure.Admin;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Common;
using USTHBStudy.Domain.Students;
using USTHBStudy.Infrastructure.Identity;
using USTHBStudy.Infrastructure.Persistence;

public sealed class UserAdminService : IUserAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;

    public UserAdminService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IRefreshTokenStore refreshTokens,
        IAuditLogger audit,
        IDateTimeProvider clock)
    {
        _db = db;
        _users = users;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _clock = clock;
    }

    public async Task<PagedResult<AdminUserDto>> ListAsync(AdminUserQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };

        var q = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(u => EF.Functions.Like(u.Email!, $"%{term}%")
                             || EF.Functions.Like(u.FirstName, $"%{term}%")
                             || EF.Functions.Like(u.LastName, $"%{term}%"));
        }

        if (query.IsActive is { } isActive)
        {
            q = q.Where(u => u.IsActive == isActive);
        }

        if (query.IsPremium is { } isPremium)
        {
            var now = _clock.UtcNow;
            q = isPremium
                ? q.Where(u => u.IsPremium && u.PremiumExpiresAt != null && u.PremiumExpiresAt > now)
                : q.Where(u => !u.IsPremium || u.PremiumExpiresAt == null || u.PremiumExpiresAt <= now);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleName = query.Role.Trim();
            q = from u in q
                join ur in _db.UserRoles on u.Id equals ur.UserId
                join r in _db.Roles on ur.RoleId equals r.Id
                where r.Name == roleName
                select u;
        }

        var total = await q.LongCountAsync(ct);
        var users = await q.OrderByDescending(u => u.CreatedAt).Skip(paging.Skip).Take(paging.Take).ToListAsync(ct);

        var items = new List<AdminUserDto>(users.Count);
        foreach (var user in users)
        {
            items.Add(await MapAsync(user));
        }

        return new PagedResult<AdminUserDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<AdminUserDetailDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireAsync(userId, ct);

        var specialty = user.SpecialtyId is { } sid
            ? await _db.Specialties.AsNoTracking().Where(s => s.Id == sid).Select(s => s.Name).FirstOrDefaultAsync(ct)
            : null;
        var level = user.LevelId is { } lid
            ? await _db.Levels.AsNoTracking().Where(l => l.Id == lid).Select(l => l.Name).FirstOrDefaultAsync(ct)
            : null;

        var lastActivity = await _db.UserActivities.AsNoTracking()
            .Where(a => a.UserId == userId).MaxAsync(a => (DateTime?)a.OccurredAt, ct);

        return new AdminUserDetailDto(
            await MapAsync(user),
            user.StudentId,
            specialty,
            level,
            await _db.Favorites.CountAsync(f => f.UserId == userId, ct),
            await _db.Subscriptions.CountAsync(s => s.UserId == userId, ct),
            lastActivity);
    }

    public async Task<AdminUserDto> SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var user = await RequireAsync(userId, ct);
        var current = await _users.GetRolesAsync(user);

        var toAdd = roles.Except(current).ToArray();
        var toRemove = current.Except(roles).ToArray();

        if (toRemove.Length > 0)
        {
            await _users.RemoveFromRolesAsync(user, toRemove);
        }

        if (toAdd.Length > 0)
        {
            await _users.AddToRolesAsync(user, toAdd);
        }

        await _audit.WriteAsync("user.roles_changed", "User", userId.ToString(), new { added = toAdd, removed = toRemove }, ct);
        return await MapAsync(user);
    }

    public async Task<AdminUserDto> GrantPremiumAsync(Guid userId, int months, CancellationToken ct = default)
    {
        if (months is < 1 or > 36)
        {
            throw new BadRequestException("Months must be between 1 and 36.");
        }

        var user = await RequireAsync(userId, ct);
        var from = user.PremiumExpiresAt is { } current && current > _clock.UtcNow ? current : _clock.UtcNow;
        user.PremiumExpiresAt = from.AddMonths(months);
        user.IsPremium = true;
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("user.premium_granted", "User", userId.ToString(),
            new { months, expiresAt = user.PremiumExpiresAt }, ct);
        return await MapAsync(user);
    }

    public async Task<AdminUserDto> RevokePremiumAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireAsync(userId, ct);
        user.IsPremium = false;
        user.PremiumExpiresAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("user.premium_revoked", "User", userId.ToString(), null, ct);
        return await MapAsync(user);
    }

    public async Task<AdminUserDto> SetActiveAsync(Guid userId, bool active, CancellationToken ct = default)
    {
        var user = await RequireAsync(userId, ct);
        user.IsActive = active;
        await _db.SaveChangesAsync(ct);

        if (!active)
        {
            await _refreshTokens.RevokeAllForUserAsync(userId, ct);
        }

        await _audit.WriteAsync(active ? "user.restored" : "user.suspended", "User", userId.ToString(), null, ct);
        return await MapAsync(user);
    }

    private async Task<ApplicationUser> RequireAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
        ?? throw new NotFoundException("User", userId);

    private async Task<AdminUserDto> MapAsync(ApplicationUser u)
    {
        var roles = await _users.GetRolesAsync(u);
        return new AdminUserDto(
            u.Id, u.Email ?? string.Empty, u.FirstName, u.LastName,
            u.IsActive, u.IsPremium, u.PremiumExpiresAt, roles.ToArray(), u.CreatedAt);
    }
}
