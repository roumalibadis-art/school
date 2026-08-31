namespace USTHBStudy.Application.Documents.Validators;

using FluentValidation;
using USTHBStudy.Domain.Documents;

public sealed class DocumentUploadRequestValidator : AbstractValidator<DocumentUploadRequest>
{
    public DocumentUploadRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Type).NotEmpty()
            .Must(t => Enum.TryParse<DocumentType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be one of: " + string.Join(", ", Enum.GetNames<DocumentType>()) + ".");
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Source).MaximumLength(500);
        RuleFor(x => x.PermissionNotes).MaximumLength(1000);
        RuleFor(x => x.RightsStatus)
            .Must(r => r is null || Enum.TryParse<RightsStatus>(r, ignoreCase: true, out _))
            .WithMessage("RightsStatus must be one of: " + string.Join(", ", Enum.GetNames<RightsStatus>()) + ".");
    }
}

public sealed class DocumentMetadataUpdateValidator : AbstractValidator<DocumentMetadataUpdate>
{
    public DocumentMetadataUpdateValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Type).NotEmpty()
            .Must(t => Enum.TryParse<DocumentType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be a valid document type.");
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Source).MaximumLength(500);
        RuleFor(x => x.PermissionNotes).MaximumLength(1000);
        RuleFor(x => x.RightsStatus)
            .Must(r => r is null || Enum.TryParse<RightsStatus>(r, ignoreCase: true, out _))
            .WithMessage("RightsStatus must be a valid value.");
    }
}

public sealed class DocumentStatusChangeValidator : AbstractValidator<DocumentStatusChange>
{
    public DocumentStatusChangeValidator()
    {
        RuleFor(x => x.Status).NotEmpty()
            .Must(s => Enum.TryParse<DocumentStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be one of: " + string.Join(", ", Enum.GetNames<DocumentStatus>()) + ".");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}
