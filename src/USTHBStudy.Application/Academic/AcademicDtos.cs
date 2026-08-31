namespace USTHBStudy.Application.Academic;

/// <summary>Common query for academic list endpoints — optional parent filter, search and paging.</summary>
public sealed record AcademicQuery(
    Guid? ParentId = null,
    string? Search = null,
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = 20);

// ---------- output DTOs ----------

public sealed record UniversityDto(Guid Id, string Name, string Slug, string? Code, string? City, string Country, bool IsActive);

public sealed record FacultyDto(Guid Id, string Name, string Slug, string? Code, Guid UniversityId, bool IsActive);

public sealed record DepartmentDto(Guid Id, string Name, string Slug, string? Code, Guid FacultyId, bool IsActive);

public sealed record DomainDto(Guid Id, string Name, string Slug, string? Code, Guid FacultyId, bool IsActive);

public sealed record SpecialtyDto(
    Guid Id, string Name, string Slug, string? Code, string? Description,
    Guid DepartmentId, Guid? DomainId, bool IsActive);

public sealed record LevelDto(
    Guid Id, string Name, string Slug, string ShortName, string Cycle, int Order, Guid SpecialtyId, bool IsActive);

public sealed record SemesterDto(
    Guid Id, string Name, string Slug, string ShortName, int Order, Guid LevelId, bool IsActive);

public sealed record AcademicYearDto(
    Guid Id, string Name, string Slug, int StartYear, int EndYear, bool IsCurrent, bool IsActive);

public sealed record SessionDto(Guid Id, string Name, string Slug, string Kind, int Order, bool IsActive);

public sealed record ModuleDto(
    Guid Id, string Name, string Slug, string? Code, string? Description,
    decimal Coefficient, int Credits, Guid SemesterId, Guid SpecialtyId, bool IsActive);

// ---------- input records (used for both create and replace) ----------

public sealed record UniversityInput(string Name, string? Code, string? City, string? Country, bool IsActive = true);

public sealed record FacultyInput(string Name, string? Code, Guid UniversityId, bool IsActive = true);

public sealed record DepartmentInput(string Name, string? Code, Guid FacultyId, bool IsActive = true);

public sealed record DomainInput(string Name, string? Code, Guid FacultyId, bool IsActive = true);

public sealed record SpecialtyInput(
    string Name, string? Code, string? Description, Guid DepartmentId, Guid? DomainId, bool IsActive = true);

public sealed record LevelInput(
    string Name, string ShortName, string Cycle, int Order, Guid SpecialtyId, bool IsActive = true);

public sealed record SemesterInput(string Name, string ShortName, int Order, Guid LevelId, bool IsActive = true);

public sealed record AcademicYearInput(int StartYear, bool IsCurrent = false, bool IsActive = true);

public sealed record SessionInput(string Name, string Kind, int Order, bool IsActive = true);

public sealed record ModuleInput(
    string Name, string? Code, string? Description, decimal Coefficient, int Credits,
    Guid SemesterId, Guid SpecialtyId, bool IsActive = true);
