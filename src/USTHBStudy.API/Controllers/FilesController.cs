namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Documents;

[Route("api/files")]
public sealed class FilesController : ApiControllerBase
{
    private readonly IDocumentService _documents;

    public FilesController(IDocumentService documents) => _documents = documents;

    /// <summary>
    /// Streams a file for a valid signed token (PRD §29). The token is the credential — no session
    /// needed — and expires within a couple of minutes.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Download([FromQuery(Name = "t")] string token, CancellationToken ct)
    {
        var content = await _documents.OpenDownloadAsync(token, ct);
        return File(content.Stream, content.ContentType, content.FileName);
    }
}
