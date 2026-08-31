namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Best-effort client IP for audit fields on refresh tokens (PRD §44).</summary>
    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
