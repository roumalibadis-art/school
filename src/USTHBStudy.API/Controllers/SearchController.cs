namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Search;

[Route("api/search")]
public sealed class SearchController : ApiControllerBase
{
    private readonly ISearchService _search;

    public SearchController(ISearchService search) => _search = search;

    /// <summary>
    /// Full-text + faceted document search (PRD §14/§15). Free text goes in <c>q</c>; a bare 4-digit
    /// number is treated as an academic year. Only published documents are returned.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SearchHit>>>> Search(
        [FromQuery(Name = "q")] string? text,
        [FromQuery] Guid? facultyId,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? specialtyId,
        [FromQuery] Guid? levelId,
        [FromQuery] Guid? semesterId,
        [FromQuery] Guid? moduleId,
        [FromQuery] string? type,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? sessionId,
        [FromQuery] bool? isPremium,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new SearchQuery(
            text, facultyId, departmentId, specialtyId, levelId, semesterId, moduleId,
            type, academicYearId, sessionId, isPremium, page, pageSize);

        return Ok(ApiResponse.Page(await _search.SearchAsync(query, ct)));
    }
}
