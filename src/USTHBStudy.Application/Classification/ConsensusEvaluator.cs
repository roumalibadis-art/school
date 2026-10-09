namespace USTHBStudy.Application.Classification;

using USTHBStudy.Domain.Classification;

/// <summary>A voted value: an entity id / enum name (approved) or a proposal id (pending).</summary>
public readonly record struct FieldValue(string Key, bool IsPending);

/// <summary>The part of a vote that consensus looks at.</summary>
public sealed record VoteSnapshot(
    VoteDecision Decision,
    FieldValue? Specialty = null,
    FieldValue? Department = null,
    FieldValue? DocumentType = null,
    FieldValue? AcademicYear = null,
    FieldValue? Session = null)
{
    public FieldValue? Get(ClassificationField field) => field switch
    {
        ClassificationField.Specialty => Specialty,
        ClassificationField.Department => Department,
        ClassificationField.DocumentType => DocumentType,
        ClassificationField.AcademicYear => AcademicYear,
        ClassificationField.Session => Session,
        _ => null,
    };
}

public enum ConsensusKind
{
    /// <summary>Not enough votes yet.</summary>
    Pending,
    Verified,
    NonEducational,
    NeedsReview,
}

public static class ReviewReasons
{
    public const string Conflict = "conflict";
    public const string InsufficientAgreement = "insufficient_agreement";
    public const string PendingTaxonomy = "pending_taxonomy";
    public const string NonEducationalPolicy = "non_educational";
    public const string AdminReopened = "reopened";
}

public sealed record ConsensusOutcome(
    ConsensusKind Kind,
    string? Reason,
    IReadOnlyDictionary<ClassificationField, FieldValue> Accepted,
    string Detail)
{
    public static ConsensusOutcome Pending { get; } =
        new(ConsensusKind.Pending, null, new Dictionary<ClassificationField, FieldValue>(), "Waiting for more votes.");
}

/// <summary>
/// The explicit, configurable consensus policy. Pure function of the settings and the votes so it can be
/// tested exhaustively. Three votes never imply a correct classification: each <em>required</em> field
/// must individually reach <see cref="ClassificationSettings.AgreementPercent"/> of <em>all</em> votes
/// (abstentions count against), with a unique leading value, and must not rest on a pending proposal.
/// Optional fields are applied only when at least two voters supplied them and they agree; otherwise they
/// stay empty without blocking verification.
/// </summary>
public static class ConsensusEvaluator
{
    private static readonly ClassificationField[] AllFields =
    {
        ClassificationField.Specialty, ClassificationField.Department, ClassificationField.DocumentType,
        ClassificationField.AcademicYear, ClassificationField.Session,
    };

    public static ConsensusOutcome Evaluate(ClassificationSettings settings, IReadOnlyList<VoteSnapshot> votes)
    {
        var total = votes.Count;
        if (total == 0 || total < settings.RequiredVoters)
        {
            return ConsensusOutcome.Pending;
        }

        var notEducational = votes.Count(v => v.Decision == VoteDecision.NotEducational);
        if (notEducational * 100 >= settings.NonEducationalPercent * total)
        {
            return new ConsensusOutcome(
                ConsensusKind.NonEducational,
                ReviewReasons.NonEducationalPolicy,
                new Dictionary<ClassificationField, FieldValue>(),
                $"{notEducational}/{total} voters marked the document as not educational.");
        }

        var classify = votes.Where(v => v.Decision == VoteDecision.Classify).ToList();
        var accepted = new Dictionary<ClassificationField, FieldValue>();
        var problems = new List<string>();
        var hasConflict = false;
        var hasPendingRequired = false;

        foreach (var field in AllFields)
        {
            var required = settings.RequiredFields.HasFlag(field);
            var provided = classify.Select(v => v.Get(field)).Where(v => v.HasValue).Select(v => v!.Value).ToList();

            if (provided.Count == 0)
            {
                if (required)
                {
                    problems.Add($"{field}: no votes supplied a value");
                }

                continue;
            }

            var groups = provided.GroupBy(v => v.Key).Select(g => (Value: g.First(), Count: g.Count()))
                .OrderByDescending(g => g.Count).ToList();
            var top = groups[0];
            var unique = groups.Count == 1 || groups[1].Count < top.Count;
            var denominator = required ? total : provided.Count;
            var agrees = unique && top.Count * 100 >= settings.AgreementPercent * denominator;

            if (!required && provided.Count < 2)
            {
                continue;
            }

            if (agrees)
            {
                if (top.Value.IsPending)
                {
                    if (required)
                    {
                        hasPendingRequired = true;
                        problems.Add($"{field}: agreed value is a pending taxonomy proposal");
                    }

                    continue;
                }

                accepted[field] = top.Value;
            }
            else if (required)
            {
                if (groups.Count > 1)
                {
                    hasConflict = true;
                }

                problems.Add($"{field}: {top.Count}/{denominator} agree (need {settings.AgreementPercent}%)");
            }
        }

        if (problems.Count > 0)
        {
            var reason = hasPendingRequired && !hasConflict
                ? ReviewReasons.PendingTaxonomy
                : hasConflict ? ReviewReasons.Conflict : ReviewReasons.InsufficientAgreement;
            return new ConsensusOutcome(ConsensusKind.NeedsReview, reason, accepted, string.Join("; ", problems));
        }

        if (accepted.Count == 0)
        {
            return new ConsensusOutcome(
                ConsensusKind.NeedsReview, ReviewReasons.InsufficientAgreement, accepted,
                "No field reached agreement.");
        }

        return new ConsensusOutcome(ConsensusKind.Verified, null, accepted, "Consensus reached.");
    }
}
