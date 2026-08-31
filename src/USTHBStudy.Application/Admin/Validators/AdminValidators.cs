namespace USTHBStudy.Application.Admin.Validators;

using FluentValidation;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Contributions;
using USTHBStudy.Application.Documents;
using USTHBStudy.Domain.Documents;

public sealed class ContributionRequestValidator : AbstractValidator<ContributionRequest>
{
    public ContributionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Type).NotEmpty()
            .Must(t => Enum.TryParse<DocumentType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be a valid document type.");
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class SubmitReportRequestValidator : AbstractValidator<SubmitReportRequest>
{
    public SubmitReportRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty()
            .Must(r => Enum.TryParse<ReportReason>(r, ignoreCase: true, out _))
            .WithMessage("Reason must be one of: " + string.Join(", ", Enum.GetNames<ReportReason>()) + ".");
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

public sealed class ResolveReportRequestValidator : AbstractValidator<ResolveReportRequest>
{
    public ResolveReportRequestValidator()
    {
        RuleFor(x => x.Status).NotEmpty()
            .Must(s => Enum.TryParse<ReportStatus>(s, ignoreCase: true, out var v) && v != ReportStatus.Open)
            .WithMessage("Status must be Resolved or Dismissed.");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class SetRolesRequestValidator : AbstractValidator<SetRolesRequest>
{
    public SetRolesRequestValidator()
    {
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles)
            .Must(r => Roles.All.Contains(r))
            .WithMessage("Roles must be a subset of: " + string.Join(", ", Roles.All) + ".");
    }
}

public sealed record SetRolesRequest(IReadOnlyList<string> Roles);
