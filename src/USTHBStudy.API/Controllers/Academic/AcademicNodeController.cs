namespace USTHBStudy.API.Controllers.Academic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Academic;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;

/// <summary>
/// CRUD endpoints shared by every academic-hierarchy resource. Reads are public (PRD §16);
/// writes require the <c>AcademicData.Manage</c> permission (PRD §34/§43).
/// </summary>
[Authorize(Policy = Permissions.AcademicData.Manage)]
public abstract class AcademicNodeController<TDto, TInput> : ApiControllerBase
{
    private readonly IAcademicNodeService<TDto, TInput> _service;

    protected AcademicNodeController(IAcademicNodeService<TDto, TInput> service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TDto>>>> List(
        [FromQuery] AcademicQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Page(await _service.ListAsync(query, ct)));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TDto>>> Get(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _service.GetAsync(id, ct)));

    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TDto>>> GetBySlug(string slug, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _service.GetBySlugAsync(slug, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TDto>>> Create(TInput input, CancellationToken ct)
    {
        var created = await _service.CreateAsync(input, ct);
        return Ok(ApiResponse.Data(created, "Created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TDto>>> Update(Guid id, TInput input, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _service.UpdateAsync(id, input, ct), "Updated."));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Deleted."));
    }
}
