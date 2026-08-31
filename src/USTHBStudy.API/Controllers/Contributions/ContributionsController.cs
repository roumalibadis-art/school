namespace USTHBStudy.API.Controllers.Contributions;

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.API.Controllers;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Contributions;
using USTHBStudy.Application.Documents;
using USTHBStudy.Domain.Documents;

public sealed class ContributionUploadForm
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid ModuleId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public string? Description { get; set; }
    public IFormFile? File { get; set; }

    public ContributionRequest ToRequest() => new(Title, Type, ModuleId, AcademicYearId, Description);
}

public sealed class ContributionUploadFormValidator : AbstractValidator<ContributionUploadForm>
{
    public ContributionUploadFormValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("A file is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Type).NotEmpty()
            .Must(t => Enum.TryParse<DocumentType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be a valid document type.");
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

[Route("api/contributions")]
[Authorize]
public sealed class ContributionsController : ApiControllerBase
{
    private const long UploadSizeLimitBytes = 60L * 1024 * 1024;

    private readonly IContributionService _contributions;
    private readonly ICurrentUser _currentUser;

    public ContributionsController(IContributionService contributions, ICurrentUser currentUser)
    {
        _contributions = contributions;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAppException();

    /// <summary>Submit a document for moderation (PRD §37). Students cannot publish directly.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadSizeLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadSizeLimitBytes)]
    public async Task<ActionResult<ApiResponse<ContributionDto>>> Submit([FromForm] ContributionUploadForm form, CancellationToken ct)
    {
        await using var stream = form.File!.OpenReadStream();
        var file = new DocumentFile(stream, form.File.FileName, form.File.ContentType, form.File.Length);
        var result = await _contributions.SubmitAsync(UserId, form.ToRequest(), file, ct);
        return Ok(ApiResponse.Data(result, "Contribution soumise. Un modérateur la vérifiera."));
    }
}
