namespace USTHBStudy.API.Controllers.Documents;

using FluentValidation;
using USTHBStudy.Application.Documents;
using USTHBStudy.Domain.Documents;

/// <summary>Multipart upload payload — metadata fields plus the file (PRD §36).</summary>
public sealed class DocumentUploadForm
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ModuleId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? SessionId { get; set; }
    public string? Description { get; set; }
    public bool IsPremium { get; set; }
    public string? Source { get; set; }
    public string? RightsStatus { get; set; }
    public string? PermissionNotes { get; set; }
    public IFormFile? File { get; set; }

    public DocumentUploadRequest ToRequest() => new(
        Title, Type, ModuleId, AcademicYearId, SessionId, Description, IsPremium, Source, RightsStatus, PermissionNotes);
}

public sealed class DocumentUploadFormValidator : AbstractValidator<DocumentUploadForm>
{
    public DocumentUploadFormValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("A file is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Type).NotEmpty()
            .Must(t => Enum.TryParse<DocumentType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be one of: " + string.Join(", ", Enum.GetNames<DocumentType>()) + ".");
        RuleFor(x => x.ModuleId).Must(id => id is null || id != Guid.Empty).WithMessage("ModuleId is invalid.");
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Source).MaximumLength(500);
        RuleFor(x => x.PermissionNotes).MaximumLength(1000);
        RuleFor(x => x.RightsStatus)
            .Must(r => string.IsNullOrEmpty(r) || Enum.TryParse<RightsStatus>(r, ignoreCase: true, out _))
            .WithMessage("RightsStatus must be one of: " + string.Join(", ", Enum.GetNames<RightsStatus>()) + ".");
    }
}
