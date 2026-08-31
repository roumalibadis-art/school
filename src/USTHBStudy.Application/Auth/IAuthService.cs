namespace USTHBStudy.Application.Auth;

using USTHBStudy.Application.Auth.Dtos;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);

    Task<AuthResult> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default);

    Task LogoutAsync(LogoutRequest request, string? ip, CancellationToken ct = default);

    Task<CurrentUserDto> GetCurrentAsync(Guid userId, CancellationToken ct = default);
}
