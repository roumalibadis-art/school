namespace USTHBStudy.Application.Abstractions;

using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Common;

/// <summary>
/// Port over ASP.NET Core Identity (implemented in Infrastructure). Keeps <c>UserManager</c> /
/// <c>SignInManager</c> out of the Application layer while the auth use-cases stay here.
/// </summary>
public interface IIdentityService
{
    Task<Result<AuthUser>> CreateStudentAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken ct = default);

    /// <summary>Verifies credentials and honours lockout / disabled-account rules (PRD §44/§61).</summary>
    Task<Result<AuthUser>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);

    Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Flattened permission claims across the user's roles (PRD §43).</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default);
}
