namespace USTHBStudy.API.Controllers.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.API.Controllers;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Contributions;
using USTHBStudy.Application.Documents;

[Route("api/admin/contributions")]
[Authorize(Policy = Permissions.Contributions.Moderate)]
public sealed class AdminContributionsController : ApiControllerBase
{
    private readonly IContributionService _contributions;

    public AdminContributionsController(IContributionService contributions) => _contributions = contributions;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContributionDto>>>> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse.Page(await _contributions.ListForModerationAsync(new ContributionQuery(status ?? "Pending", page, pageSize), ct)));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<ContributionDto>>> Approve(Guid id, ModerateContributionRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _contributions.ApproveAsync(id, request.Note, request.PublishNow, ct), "Contribution approuvée."));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<ContributionDto>>> Reject(Guid id, ModerateContributionRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _contributions.RejectAsync(id, request.Note, ct), "Contribution refusée."));
}

[Route("api/admin/reports")]
[Authorize(Policy = Permissions.Reports.Resolve)]
public sealed class AdminReportsController : ApiControllerBase
{
    private readonly IReportService _reports;
    private readonly ICurrentUser _currentUser;

    public AdminReportsController(IReportService reports, ICurrentUser currentUser)
    {
        _reports = reports;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReportDto>>>> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse.Page(await _reports.ListAsync(new ReportQuery(status ?? "Open", page, pageSize), ct)));

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<ApiResponse<ReportDto>>> Resolve(Guid id, ResolveReportRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        return Ok(ApiResponse.Data(await _reports.ResolveAsync(id, userId, request.Status, request.Note, ct), "Signalement traité."));
    }
}

[Route("api/admin/audit")]
[Authorize(Policy = Permissions.Audit.View)]
public sealed class AdminAuditController : ApiControllerBase
{
    private readonly IAuditQueryService _audit;

    public AdminAuditController(IAuditQueryService audit) => _audit = audit;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditEntryDto>>>> List(
        [FromQuery] string? action, [FromQuery] string? entityType,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(ApiResponse.Page(await _audit.ListAsync(new AuditQuery(action, entityType, null, page, pageSize), ct)));
}

[Route("api/admin/dashboard")]
[Authorize(Policy = Permissions.Users.View)]
public sealed class AdminDashboardController : ApiControllerBase
{
    private readonly IAdminDashboardService _dashboard;

    public AdminDashboardController(IAdminDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<AdminDashboardDto>>> Get(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _dashboard.GetAsync(ct)));
}
