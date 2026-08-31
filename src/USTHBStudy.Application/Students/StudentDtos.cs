namespace USTHBStudy.Application.Students;

public sealed record AcademicRef(Guid Id, string Name, string Slug);

public sealed record StudentProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? StudentId,
    bool IsActive,
    bool IsPremium,
    DateTime? PremiumExpiresAt,
    AcademicRef? University,
    AcademicRef? Faculty,
    AcademicRef? Department,
    AcademicRef? Specialty,
    AcademicRef? Level,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>Profile edit payload (PRD §19). Academic fields are optional so a student can fill them later.</summary>
public sealed record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? StudentId,
    Guid? UniversityId,
    Guid? FacultyId,
    Guid? DepartmentId,
    Guid? SpecialtyId,
    Guid? LevelId);

public sealed record ModuleSummary(Guid Id, string Name, string Slug, string? Semester);

public sealed record DocumentSummary(
    Guid Id, string Title, string Slug, string Type, bool IsPremium, bool HasPreview, long ViewCount);

public sealed record SubscriptionStatusDto(bool IsPremium, DateTime? ExpiresAt, string State);

/// <summary>Personalized dashboard (PRD §20/§21).</summary>
public sealed record StudentDashboardDto(
    string FirstName,
    AcademicRef? Specialty,
    AcademicRef? Level,
    IReadOnlyList<ModuleSummary> MyModules,
    IReadOnlyList<DocumentSummary> RecentResources,
    IReadOnlyList<DocumentSummary> PopularExams,
    int FavoritesCount,
    SubscriptionStatusDto Subscription);
