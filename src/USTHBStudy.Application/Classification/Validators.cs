namespace USTHBStudy.Application.Classification;

using FluentValidation;
using USTHBStudy.Domain.Classification;

public sealed class UpdateClassificationSettingsRequestValidator : AbstractValidator<UpdateClassificationSettingsRequest>
{
    public UpdateClassificationSettingsRequestValidator()
    {
        RuleFor(x => x.DocumentsPerTask).InclusiveBetween(1, 10);
        RuleFor(x => x.AssignmentExpiryHours).InclusiveBetween(1, 24 * 14);
        RuleFor(x => x.MinSecondsBeforeVote).InclusiveBetween(0, 120);
        RuleFor(x => x.RequiredVoters).InclusiveBetween(1, 15);
        RuleFor(x => x.AgreementPercent).InclusiveBetween(51, 100);
        RuleFor(x => x.NonEducationalPercent).InclusiveBetween(51, 100);
        RuleFor(x => x.NonEducationalPolicy).Must(v => Enum.TryParse<NonEducationalPolicy>(v, true, out _))
            .WithMessage("Unknown non-educational policy.");
        RuleFor(x => x.RequiredFields).NotNull().Must(fields => fields is not null
            && fields.All(f => Enum.TryParse<ClassificationField>(f, true, out var parsed) && parsed != ClassificationField.None))
            .WithMessage("Unknown classification field.");
        RuleFor(x => x.DownloadsPerPrompt).InclusiveBetween(1, 1000);
        RuleFor(x => x.PromptSnoozeMinutes).InclusiveBetween(1, 60 * 24 * 7);
        RuleFor(x => x.FreeDownloadsPerWindow).InclusiveBetween(0, 100_000);
        RuleFor(x => x.QuotaWindowDays).InclusiveBetween(1, 366);
        RuleFor(x => x.BonusDownloadsPerContribution).InclusiveBetween(0, 1000);
        RuleFor(x => x.MaxBonusPerWindow).InclusiveBetween(0, 100_000);
        RuleFor(x => x.MaxRewardedContributionsPerDay).InclusiveBetween(1, 10_000);
        RuleFor(x => x.MaxPendingProposalsPerUser).InclusiveBetween(1, 1000);
    }
}

public sealed class SubmitVoteRequestValidator : AbstractValidator<SubmitVoteRequest>
{
    public SubmitVoteRequestValidator()
    {
        RuleFor(x => x.Decision).Must(v => Enum.TryParse<VoteDecision>(v, true, out _))
            .WithMessage("Decision must be 'Classify' or 'NotEducational'.");
    }
}

public sealed class ProposeRequestValidator : AbstractValidator<ProposeRequest>
{
    public ProposeRequestValidator()
    {
        RuleFor(x => x.Category).Must(v => Enum.TryParse<ProposalCategory>(v, true, out _))
            .WithMessage("Unknown taxonomy category.");
        RuleFor(x => x.Value).NotEmpty().MaximumLength(400);
    }
}

public sealed class RenameProposalRequestValidator : AbstractValidator<RenameProposalRequest>
{
    public RenameProposalRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(400);
}
