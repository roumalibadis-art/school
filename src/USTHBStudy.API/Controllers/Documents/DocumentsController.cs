namespace USTHBStudy.API.Controllers.Documents;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.API.Controllers;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Documents;

[Route("api/documents")]
public sealed class DocumentsController : ApiControllerBase
{
    private const long UploadSizeLimitBytes = 60L * 1024 * 1024;

    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

    /// <summary>Lists documents. Anonymous/students see only published; staff can filter by status (PRD §14/§15).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentDto>>>> List(
        [FromQuery] DocumentQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Page(await _documents.ListAsync(query, ct)));

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> GetBySlug(string slug, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _documents.GetBySlugAsync(slug, ct)));

    [HttpGet("id/{id:guid}")]
    [Authorize(Policy = Permissions.Documents.Update)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _documents.GetAsync(id, ct)));

    /// <summary>First-page preview image (PRD §31). Public for published documents.</summary>
    [HttpGet("{slug}/preview")]
    [AllowAnonymous]
    public async Task<IActionResult> Preview(string slug, CancellationToken ct)
    {
        var content = await _documents.OpenPreviewAsync(slug, ct);
        return File(content.Stream, content.ContentType);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Documents.Create)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadSizeLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadSizeLimitBytes)]
    public async Task<ActionResult<ApiResponse<DocumentUploadResult>>> Upload(
        [FromForm] DocumentUploadForm form, CancellationToken ct)
    {
        await using var stream = form.File!.OpenReadStream();
        var file = new DocumentFile(stream, form.File.FileName, form.File.ContentType, form.File.Length);

        var result = await _documents.UploadAsync(form.ToRequest(), file, ct);

        var message = result.PossibleDuplicate is null
            ? "Uploaded as a draft."
            : "Uploaded as a draft. Warning: a document with the same file already exists.";
        return Ok(ApiResponse.Data(result, message));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Documents.Update)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> Update(
        Guid id, DocumentMetadataUpdate update, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _documents.UpdateAsync(id, update, ct), "Updated."));

    /// <summary>Publish / reject / archive (PRD §12/§35).</summary>
    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Permissions.Documents.Publish)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> ChangeStatus(
        Guid id, DocumentStatusChange change, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _documents.ChangeStatusAsync(id, change, ct), $"Status set to {change.Status}."));

    [HttpPost("{parentId:guid}/solutions/{solutionId:guid}")]
    [Authorize(Policy = Permissions.Documents.Update)]
    public async Task<ActionResult<ApiResponse>> LinkSolution(Guid parentId, Guid solutionId, CancellationToken ct)
    {
        await _documents.LinkSolutionAsync(parentId, solutionId, ct);
        return Ok(ApiResponse.Ok("Solution linked."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Documents.Delete)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _documents.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Deleted."));
    }
}
