namespace USTHBStudy.Application.Students.Validators;

using FluentValidation;
using USTHBStudy.Domain.Students;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StudentId).MaximumLength(50);
    }
}

public sealed class AddFavoriteRequestValidator : AbstractValidator<AddFavoriteRequest>
{
    public AddFavoriteRequestValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty()
            .Must(k => Enum.TryParse<FavoriteKind>(k, ignoreCase: true, out _))
            .WithMessage("Kind must be one of: " + string.Join(", ", Enum.GetNames<FavoriteKind>()) + ".");
    }
}
