namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;

/// <summary>
/// Authorization self-check endpoints. Not part of the product surface — they exist so the
/// permission pipeline (PRD §43) can be verified end-to-end, including the "student → 403"
/// case from PRD §61.
/// </summary>
[Route("api/diagnostics")]
[ApiExplorerSettings(GroupName = "diagnostics")]
public sealed class DiagnosticsController : ApiControllerBase
{
    /// <summary>Requires the <c>AcademicData.Manage</c> permission — a Student always gets 403 here.</summary>
    [HttpGet("permission-probe")]
    [Authorize(Policy = Permissions.AcademicData.Manage)]
    public ActionResult<ApiResponse> PermissionProbe() =>
        Ok(ApiResponse.Ok("Authorized: you hold AcademicData.Manage."));

    /// <summary>Requires only authentication — used to check the 401 path.</summary>
    [HttpGet("whoami")]
    [Authorize]
    public ActionResult<ApiResponse> WhoAmI() =>
        Ok(ApiResponse.Ok($"Authenticated as {User.FindFirst("sub")?.Value}."));
}
