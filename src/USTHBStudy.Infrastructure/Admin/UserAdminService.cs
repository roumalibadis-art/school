namespace USTHBStudy.Infrastructure.Admin;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Common;
using USTHBStudy.Infrastructure.Identity;
using USTHBStudy.Infrastructure.Persistence;

public sealed class UserAdminService : IUserAdminService
{
    private readonly AppDbContext _db;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IDateTimeProvider _clock;

    public UserAdminService(AppDbContext db, IRefreshTokenStore refreshTokens, IDateTimeProvider clock)
    {
        _db = db;
        _refreshTokens = refreshTokens;
        _clock = clock;
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
        return Map(user);
    }

    public async Task<AdminUserDto> RevokePremiumAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireAsync(userId, ct);
        user.IsPremium = false;
        user.PremiumExpiresAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Map(user);
    }

    public async Task<AdminUserDto> SetActiveAsync(Guid userId, bool active, CancellationToken ct = default)
    {
        var user = await RequireAsync(userId, ct);
        user.IsActive = active;
        await _db.SaveChangesAsync(ct);

        if (!active)
        {
            // Kill existing sessions so suspension takes effect before the access token expires.
            await _refreshTokens.RevokeAllForUserAsync(userId, ct);
        }

        return Map(user);
    }

    private async Task<ApplicationUser> RequireAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
        ?? throw new NotFoundException("User", userId);

    private static AdminUserDto Map(ApplicationUser u) =>
        new(u.Id, u.Email ?? string.Empty, u.FirstName, u.LastName, u.IsActive, u.IsPremium, u.PremiumExpiresAt);
}
