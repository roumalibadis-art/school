namespace USTHBStudy.Application.Abstractions;

/// <summary>
/// The authenticated principal for the current request. Implemented in the API layer over
/// <c>HttpContext</c>; use-case code depends on this instead of touching HTTP directly.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }

    bool IsInRole(string role);
    bool HasPermission(string permission);
}
