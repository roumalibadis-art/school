namespace USTHBStudy.API.Controllers.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Classification;
using USTHBStudy.Application.Common;

[Route("api/admin/classification")]
public sealed class AdminClassificationController : ApiControllerBase
{
    private readonly IClassificationAdminService _admin;
    private readonly IClassificationSettingsService _settings;

    public AdminClassificationController(IClassificationAdminService admin, IClassificationSettingsService settings)
    {
        _admin = admin;
        _settings = settings;
    }

    [HttpGet("documents")]
    [Authorize(Policy = Permissions.Classification.Review)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ClassificationDocumentItem>>>> List(
        [FromQuery] string? queue, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponse.Page(await _admin.ListAsync(new ClassificationQueueQuery(queue ?? "needs-review", search, page, pageSize), ct)));

    [HttpGet("documents/{id:guid}")]
    [Authorize(Policy = Permissions.Classification.Review)]
    public async Task<ActionResult<ApiResponse<ClassificationDocumentDetail>>> Get(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _admin.GetAsync(id, ct)));

    [HttpGet("documents/{id:guid}/preview")]
    [Authorize(Policy = Permissions.Classification.Review)]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var content = await _admin.OpenPreviewAsync(id, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(content.Stream, content.ContentType);
    }

    /// <summary>Confirm or correct a classification (explicit values, or the leading votes when none are given).</summary>
    [HttpPost("documents/{id:guid}/verify")]
    [Authorize(Policy = Permissions.Classification.Review)]
    public async Task<ActionResult<ApiResponse<ClassificationDocumentDetail>>> Verify(
        Guid id, AdminClassificationDecision decision, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _admin.VerifyAsync(id, decision, ct), "Classification verified."));

    [HttpPost("documents/{id:guid}/reject")]
    [Authorize(Policy = Permissions.Classification.Review)]
    public async Task<ActionResult<ApiResponse<ClassificationDocumentDetail>>> Reject(Guid id, AdminNoteRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _admin.RejectAsync(id, request.Note, ct), "Document rejected."));

    [HttpPost("documents/{id:guid}/reopen")]
    [Authorize(Policy = Permissions.Classification.Review)]
    public async Task<ActionResult<ApiResponse<ClassificationDocumentDetail>>> Reopen(Guid id, AdminNoteRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _admin.ReopenAsync(id, request.Note, ct), "Classification reopened for voting."));

    [HttpGet("report")]
    [Authorize(Policy = Permissions.Classification.Review)]
    public async Task<ActionResult<ApiResponse<ClassificationReportDto>>> Report(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _admin.GetReportAsync(ct)));

    [HttpGet("settings")]
    [Authorize(Policy = Permissions.Classification.Settings)]
    public async Task<ActionResult<ApiResponse<ClassificationSettingsDto>>> GetSettings(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _settings.GetAsync(ct)));

    [HttpPut("settings")]
    [Authorize(Policy = Permissions.Classification.Settings)]
    public async Task<ActionResult<ApiResponse<ClassificationSettingsDto>>> UpdateSettings(
        UpdateClassificationSettingsRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _settings.UpdateAsync(request, ct), "Settings saved."));
}

[Route("api/admin/taxonomy/proposals")]
[Authorize(Policy = Permissions.Taxonomy.Review)]
public sealed class AdminTaxonomyController : ApiControllerBase
{
    private readonly ITaxonomyProposalService _proposals;

    public AdminTaxonomyController(ITaxonomyProposalService proposals) => _proposals = proposals;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProposalDto>>>> List(
        [FromQuery] string? status, [FromQuery] string? category, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse.Page(await _proposals.ListAsync(new ProposalQuery(status ?? "Pending", category, search, page, pageSize), ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProposalDetailDto>>> Get(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _proposals.GetAsync(id, ct)));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<ProposalDto>>> Approve(Guid id, ApproveProposalRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _proposals.ApproveAsync(id, request, ct), "Proposal approved."));

    [HttpPost("{id:guid}/rename")]
    public async Task<ActionResult<ApiResponse<ProposalDto>>> Rename(Guid id, RenameProposalRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _proposals.RenameAsync(id, request.Name, ct), "Proposal renamed."));

    [HttpPost("{id:guid}/merge")]
    public async Task<ActionResult<ApiResponse<ProposalDto>>> Merge(Guid id, MergeProposalRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _proposals.MergeAsync(id, request, ct), "Proposal merged."));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<ProposalDto>>> Reject(Guid id, RejectProposalRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _proposals.RejectAsync(id, request.Note, ct), "Proposal rejected."));
}
