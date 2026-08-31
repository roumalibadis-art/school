namespace USTHBStudy.Application.Auth.Dtos;

/// <summary>
/// Phase 1 registration: identity + name only. The academic profile (university/faculty/…,
/// PRD §19) is collected in Phase 5 via <c>PUT /api/me</c>.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string FirstName,
    string LastName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);
