namespace USTHBStudy.API.Controllers.Classification;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Classification;
using USTHBStudy.Application.Common;

/// <summary>The student-facing community classification workflow. Every route needs a signed-in user.</summary>
[Route("api/classification")]
[Authorize]
public sealed class ClassificationController : ApiControllerBase
{
    private readonly IClassificationService _classification;
    private readonly ITaxonomyProposalService _taxonomy;
    private readonly ICurrentUser _currentUser;

    public ClassificationController(
        IClassificationService classification, ITaxonomyProposalService taxonomy, ICurrentUser currentUser)
    {
        _classification = classification;
        _taxonomy = taxonomy;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAppException();

    /// <summary>Should the UI invite the user to classify right now? Read-only; coordinates login/download triggers.</summary>
    [HttpGet("prompt")]
    public async Task<ActionResult<ApiResponse<ClassificationPromptDto>>> Prompt(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _classification.GetPromptAsync(UserId, ct)));

    /// <summary><c>start</c> or <c>later</c>: consumes the trigger so the same prompt never repeats.</summary>
    [HttpPost("prompt/ack")]
    public async Task<ActionResult<ApiResponse<ClassificationTaskDto?>>> Acknowledge(PromptAckRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.Action, "start", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Action, "later", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Action must be 'start' or 'later'.");
        }

        return Ok(ApiResponse.Data(await _classification.AcknowledgePromptAsync(UserId, request.Action, ct)));
    }

    /// <summary>The caller's open task (or a fresh one). <c>data</c> is null when nothing needs classifying.</summary>
    [HttpPost("tasks/next")]
    public async Task<ActionResult<ApiResponse<ClassificationTaskDto?>>> NextTask(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _classification.GetOrCreateTaskAsync(UserId, ct)));

    [HttpPost("assignments/{id:guid}/vote")]
    public async Task<ActionResult<ApiResponse<VoteResultDto>>> Vote(Guid id, SubmitVoteRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _classification.SubmitVoteAsync(UserId, id, request, ct), "Thank you — your answer was recorded."));

    [HttpPost("assignments/{id:guid}/skip")]
    public async Task<ActionResult<ApiResponse<SkipResultDto>>> Skip(Guid id, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _classification.SkipAsync(UserId, id, ct)));

    /// <summary>Preview image of a document in the caller's own open assignment — nothing else is reachable.</summary>
    [HttpGet("assignments/{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var content = await _classification.OpenAssignmentPreviewAsync(UserId, id, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(content.Stream, content.ContentType);
    }

    [HttpGet("options")]
    public async Task<ActionResult<ApiResponse<ClassificationOptionsDto>>> Options(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _classification.GetOptionsAsync(UserId, ct)));

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<MyContributionsDto>>> Me(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _classification.GetMyContributionsAsync(UserId, ct)));

    // ---- "Add new…" ----

    [HttpPost("proposals")]
    public async Task<ActionResult<ApiResponse<ProposeResultDto>>> Propose(ProposeRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _taxonomy.ProposeAsync(UserId, request, ct)));

    [HttpGet("proposals/similar")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SimilarValueDto>>>> Similar(
        [FromQuery] string category, [FromQuery] string value, [FromQuery] Guid? parentId, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _taxonomy.FindSimilarAsync(category, value, parentId, ct)));
}
