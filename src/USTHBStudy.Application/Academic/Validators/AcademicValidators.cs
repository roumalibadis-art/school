namespace USTHBStudy.Application.Academic.Validators;

using FluentValidation;

public sealed class UniversityInputValidator : AbstractValidator<UniversityInput>
{
    public UniversityInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.City).MaximumLength(120);
        RuleFor(x => x.Country).MaximumLength(80);
    }
}

public sealed class FacultyInputValidator : AbstractValidator<FacultyInput>
{
    public FacultyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.UniversityId).NotEmpty();
    }
}

public sealed class DepartmentInputValidator : AbstractValidator<DepartmentInput>
{
    public DepartmentInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.FacultyId).NotEmpty();
    }
}

public sealed class DomainInputValidator : AbstractValidator<DomainInput>
{
    public DomainInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.FacultyId).NotEmpty();
    }
}

public sealed class SpecialtyInputValidator : AbstractValidator<SpecialtyInput>
{
    public SpecialtyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.DepartmentId).NotEmpty();
    }
}

public sealed class LevelInputValidator : AbstractValidator<LevelInput>
{
    public LevelInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Cycle).NotEmpty()
            .Must(c => Enum.TryParse<Domain.Academic.StudyCycle>(c, ignoreCase: true, out _))
            .WithMessage("Cycle must be one of: Licence, Master, Doctorat, Engineer.");
        RuleFor(x => x.Order).InclusiveBetween(1, 12);
        RuleFor(x => x.SpecialtyId).NotEmpty();
    }
}

public sealed class SemesterInputValidator : AbstractValidator<SemesterInput>
{
    public SemesterInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Order).InclusiveBetween(1, 12);
        RuleFor(x => x.LevelId).NotEmpty();
    }
}

public sealed class AcademicYearInputValidator : AbstractValidator<AcademicYearInput>
{
    public AcademicYearInputValidator()
    {
        RuleFor(x => x.StartYear).InclusiveBetween(1962, 2100);
    }
}

public sealed class SessionInputValidator : AbstractValidator<SessionInput>
{
    public SessionInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).NotEmpty()
            .Must(k => Enum.TryParse<Domain.Academic.SessionKind>(k, ignoreCase: true, out _))
            .WithMessage("Kind must be one of: Normal, Retake, ContinuousAssessment, MakeUp, Other.");
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
    }
}

public sealed class ModuleInputValidator : AbstractValidator<ModuleInput>
{
    public ModuleInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Coefficient).InclusiveBetween(0.1m, 20m);
        RuleFor(x => x.Credits).InclusiveBetween(0, 60);
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.SpecialtyId).NotEmpty();
    }
}
