namespace USTHBStudy.Application.Auth;

using Microsoft.Extensions.Options;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Common;

/// <summary>
/// Orchestrates the authentication use-cases (PRD §44). Owns the token-issuance and
/// refresh-rotation policy; delegates identity work to <see cref="IIdentityService"/> and
/// token persistence to <see cref="IRefreshTokenStore"/>.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _tokens;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IDateTimeProvider _clock;
    private readonly JwtOptions _jwt;

    public AuthService(
        IIdentityService identity,
        IJwtTokenService tokens,
        IRefreshTokenStore refreshTokens,
        IDateTimeProvider clock,
        IOptions<JwtOptions> jwt)
    {
        _identity = identity;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _clock = clock;
        _jwt = jwt.Value;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default)
    {
        var created = await _identity.CreateStudentAsync(
            request.Email.Trim(), request.Password, request.FirstName.Trim(), request.LastName.Trim(), ct);

        if (created.Failed)
        {
            throw new ConflictException(string.Join(" ", created.Errors));
        }

        return await IssueAsync(created.Value!, ip, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        var check = await _identity.ValidateCredentialsAsync(request.Email.Trim(), request.Password, ct);
        if (check.Failed)
        {
            // Uniform message — do not reveal whether the account exists (PRD §44).
            throw new UnauthorizedAppException("Invalid email or password.");
        }

        var user = check.Value!;
        if (!user.IsActive)
        {
            throw new ForbiddenAppException("This account is disabled.");
        }

        return await IssueAsync(user, ip, ct);
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default)
    {
        var presentedHash = _tokens.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokens.FindActiveAsync(presentedHash, ct)
                     ?? throw new UnauthorizedAppException("Invalid or expired refresh token.");

        var user = await _identity.FindByIdAsync(stored.UserId, ct)
                   ?? throw new UnauthorizedAppException("Invalid or expired refresh token.");

        if (!user.IsActive)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
            throw new ForbiddenAppException("This account is disabled.");
        }

        var newRefresh = _tokens.CreateRefreshToken();
        var newHash = _tokens.HashRefreshToken(newRefresh);
        var expiresAt = _clock.UtcNow.AddDays(_jwt.RefreshTokenDays);

        await _refreshTokens.RotateAsync(presentedHash, newHash, user.Id, expiresAt, ip, ct);

        return await BuildResultAsync(user, newRefresh, ct);
    }

    public async Task LogoutAsync(LogoutRequest request, string? ip, CancellationToken ct = default)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        await _refreshTokens.RevokeAsync(hash, ip, ct);
    }

    public async Task<CurrentUserDto> GetCurrentAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _identity.FindByIdAsync(userId, ct)
                   ?? throw new NotFoundException("User", userId);
        return await ToDtoAsync(user, ct);
    }

    private async Task<AuthResult> IssueAsync(AuthUser user, string? ip, CancellationToken ct)
    {
        var refresh = _tokens.CreateRefreshToken();
        var hash = _tokens.HashRefreshToken(refresh);
        var expiresAt = _clock.UtcNow.AddDays(_jwt.RefreshTokenDays);

        await _refreshTokens.AddAsync(user.Id, hash, expiresAt, ip, ct);

        return await BuildResultAsync(user, refresh, ct);
    }

    private async Task<AuthResult> BuildResultAsync(AuthUser user, string refreshToken, CancellationToken ct)
    {
        var roles = await _identity.GetRolesAsync(user.Id, ct);
        var permissions = await _identity.GetPermissionsAsync(user.Id, ct);

        var access = _tokens.CreateAccessToken(new TokenUser(
            user.Id, user.Email, user.FirstName, user.LastName, roles, permissions));

        var dto = new CurrentUserDto(
            user.Id, user.Email, user.FirstName, user.LastName,
            user.IsActive, user.IsPremium, user.PremiumExpiresAt, roles, permissions);

        return new AuthResult(access.Value, refreshToken, access.ExpiresAtUtc, dto);
    }

    private async Task<CurrentUserDto> ToDtoAsync(AuthUser user, CancellationToken ct)
    {
        var roles = await _identity.GetRolesAsync(user.Id, ct);
        var permissions = await _identity.GetPermissionsAsync(user.Id, ct);
        return new CurrentUserDto(
            user.Id, user.Email, user.FirstName, user.LastName,
            user.IsActive, user.IsPremium, user.PremiumExpiresAt, roles, permissions);
    }
}
