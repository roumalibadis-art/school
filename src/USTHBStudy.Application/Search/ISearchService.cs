namespace USTHBStudy.Application.Search;

using USTHBStudy.Application.Common;

/// <summary>
/// Cross-cutting document search (PRD §14). MySQL-backed for now; the contract is engine-agnostic so
/// a Meilisearch implementation can replace it without touching callers.
/// </summary>
public interface ISearchService
{
    Task<PagedResult<SearchHit>> SearchAsync(SearchQuery query, CancellationToken ct = default);
}

/// <summary>Free text plus the filters from PRD §15.</summary>
public sealed record SearchQuery(
    string? Text = null,
    Guid? FacultyId = null,
    Guid? DepartmentId = null,
    Guid? SpecialtyId = null,
    Guid? LevelId = null,
    Guid? SemesterId = null,
    Guid? ModuleId = null,
    string? DocumentType = null,
    Guid? AcademicYearId = null,
    Guid? SessionId = null,
    bool? IsPremium = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>One result row (PRD §15: title, type, module, year, premium, preview).</summary>
public sealed record SearchHit(
    Guid Id,
    string Title,
    string Slug,
    string Type,
    string ModuleName,
    string ModuleSlug,
    string? SpecialtyName,
    int? Year,
    string? Session,
    bool IsPremium,
    bool HasPreview);
