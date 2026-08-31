namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Contributions;
using USTHBStudy.Application.Students;

[Authorize]
[Route("api/me")]
public sealed class MeController : ApiControllerBase
{
    private readonly IStudentService _students;
    private readonly IActivityService _activity;
    private readonly IContributionService _contributions;
    private readonly ICurrentUser _currentUser;

    public MeController(
        IStudentService students, IActivityService activity, IContributionService contributions, ICurrentUser currentUser)
    {
        _students = students;
        _activity = activity;
        _contributions = contributions;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAppException();

    /// <summary>Current user's profile, academic profile, roles and Premium status (PRD §20).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<StudentProfileDto>>> Get(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _students.GetProfileAsync(UserId, ct)));

    /// <summary>Sets the academic profile (PRD §19) and name.</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<StudentProfileDto>>> Update(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _students.UpdateProfileAsync(UserId, request, ct), "Profile updated."));

    /// <summary>Personalized dashboard: modules, recent resources, popular exams, subscription (PRD §20/§21).</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<StudentDashboardDto>>> Dashboard(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _students.GetDashboardAsync(UserId, ct)));

    /// <summary>Recently viewed documents / modules and downloads (PRD §28).</summary>
    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<HistoryDto>>> History(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _activity.GetHistoryAsync(UserId, ct)));

    /// <summary>My submitted contributions and their status (PRD §37).</summary>
    [HttpGet("contributions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContributionDto>>>> Contributions(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _contributions.ListMineAsync(UserId, ct)));
}
