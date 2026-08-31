namespace USTHBStudy.Application.Auth.Dtos;

/// <summary>User facts returned by <see cref="Abstractions.IIdentityService"/> — no Identity types leak upward.</summary>
public sealed record AuthUser(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool IsPremium,
    DateTime? PremiumExpiresAt);

/// <summary>Input to access-token creation: identity plus authorization claims.</summary>
public sealed record TokenUser(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>A signed JWT and its UTC expiry.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>Result of register / login / refresh.</summary>
public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    CurrentUserDto User);

/// <summary>Shape of <c>GET /api/me</c> and the <c>user</c> block of <see cref="AuthResult"/>.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool IsPremium,
    DateTime? PremiumExpiresAt,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
