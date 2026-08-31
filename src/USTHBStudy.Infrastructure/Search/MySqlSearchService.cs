namespace USTHBStudy.Infrastructure.Search;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Search;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>
/// MySQL <c>LIKE</c>-based document search (PRD §14 — "start with MySQL-compatible search if
/// sufficient"). Only <see cref="DocumentStatus.Published"/> documents are searchable.
/// </summary>
public sealed partial class MySqlSearchService : ISearchService
{
    private readonly AppDbContext _db;

    public MySqlSearchService(AppDbContext db) => _db = db;

    public async Task<PagedResult<SearchHit>> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };

        var q = _db.Documents.AsNoTracking()
            .Where(d => d.Status == DocumentStatus.Published);

        if (query.ModuleId is { } moduleId)
        {
            q = q.Where(d => d.ModuleId == moduleId);
        }

        if (query.SpecialtyId is { } specialtyId)
        {
            q = q.Where(d => d.Module!.SpecialtyId == specialtyId);
        }

        if (query.SemesterId is { } semesterId)
        {
            q = q.Where(d => d.Module!.SemesterId == semesterId);
        }

        if (query.LevelId is { } levelId)
        {
            q = q.Where(d => d.Module!.Semester!.LevelId == levelId);
        }

        if (query.DepartmentId is { } departmentId)
        {
            q = q.Where(d => d.Module!.Specialty!.DepartmentId == departmentId);
        }

        if (query.FacultyId is { } facultyId)
        {
            q = q.Where(d => d.Module!.Specialty!.Department!.FacultyId == facultyId);
        }

        if (query.AcademicYearId is { } yearId)
        {
            q = q.Where(d => d.AcademicYearId == yearId);
        }

        if (query.SessionId is { } sessionId)
        {
            q = q.Where(d => d.SessionId == sessionId);
        }

        if (query.IsPremium is { } isPremium)
        {
            q = q.Where(d => d.IsPremium == isPremium);
        }

        if (query.DocumentType is not null && Enum.TryParse<DocumentType>(query.DocumentType, true, out var type))
        {
            q = q.Where(d => d.Type == type);
        }

        foreach (var term in Terms(query.Text))
        {
            if (int.TryParse(term, out var year) && year is >= 1990 and <= 2100)
            {
                q = q.Where(d => d.AcademicYear!.StartYear == year || d.AcademicYear.EndYear == year);
                continue;
            }

            var like = $"%{term}%";
            q = q.Where(d =>
                EF.Functions.Like(d.Title, like) ||
                EF.Functions.Like(d.Module!.Name, like) ||
                (d.Description != null && EF.Functions.Like(d.Description, like)));
        }

        var total = await q.LongCountAsync(ct);

        var hits = await q
            .OrderByDescending(d => d.PublishedAt ?? d.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Take)
            .Select(d => new SearchHit(
                d.Id,
                d.Title,
                d.Slug,
                d.Type.ToString(),
                d.Module!.Name,
                d.Module.Slug,
                d.Module.Specialty!.Name,
                d.AcademicYear != null ? d.AcademicYear.StartYear : (int?)null,
                d.Session != null ? d.Session.Name : null,
                d.IsPremium,
                d.PreviewStorageKey != null))
            .ToListAsync(ct);

        return new PagedResult<SearchHit>(hits, paging.Page, paging.PageSize, total);
    }

    private static IEnumerable<string> Terms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in TermPattern().Matches(text))
        {
            if (match.Value.Length >= 2)
            {
                yield return match.Value;
            }
        }
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex TermPattern();
}
