namespace USTHBStudy.Application.Subscriptions.Validators;

using FluentValidation;

public sealed class SubscriptionPlanInputValidator : AbstractValidator<SubscriptionPlanInput>
{
    public SubscriptionPlanInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.DurationDays).InclusiveBetween(1, 730);
        RuleFor(x => x.Price).InclusiveBetween(0m, 1_000_000m);
        RuleFor(x => x.Currency).MaximumLength(3);
        RuleFor(x => x.Features).MaximumLength(4000);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator() => RuleFor(x => x.PlanId).NotEmpty();
}

public sealed class ExtendSubscriptionRequestValidator : AbstractValidator<ExtendSubscriptionRequest>
{
    public ExtendSubscriptionRequestValidator() => RuleFor(x => x.Days).InclusiveBetween(1, 730);
}
