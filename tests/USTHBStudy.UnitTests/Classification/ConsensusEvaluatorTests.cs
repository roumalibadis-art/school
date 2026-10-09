namespace USTHBStudy.UnitTests.Classification;

using FluentAssertions;
using USTHBStudy.Application.Classification;
using USTHBStudy.Domain.Classification;

public class ConsensusEvaluatorTests
{
    private static ClassificationSettings Settings(
        int voters = 3, int agreement = 66, int nonEdu = 66,
        ClassificationField required = ClassificationField.Specialty | ClassificationField.DocumentType) =>
        new() { RequiredVoters = voters, AgreementPercent = agreement, NonEducationalPercent = nonEdu, RequiredFields = required };

    private static FieldValue V(string key, bool pending = false) => new(key, pending);

    private static VoteSnapshot Classify(string? specialty = null, string? type = null, string? year = null, string? session = null, string? dept = null) =>
        new(VoteDecision.Classify,
            specialty is null ? null : V(specialty), dept is null ? null : V(dept), type is null ? null : V(type),
            year is null ? null : V(year), session is null ? null : V(session));

    private static VoteSnapshot NotEdu() => new(VoteDecision.NotEducational);

    [Fact]
    public void Fewer_votes_than_required_is_pending_whatever_they_say()
    {
        var outcome = ConsensusEvaluator.Evaluate(Settings(), new[] { Classify("s", "Exam"), Classify("s", "Exam") });
        outcome.Kind.Should().Be(ConsensusKind.Pending);
    }

    [Fact]
    public void Unanimous_votes_verify_and_return_the_accepted_values()
    {
        var votes = Enumerable.Range(0, 3).Select(_ => Classify("s1", "Exam", "y1", "se1")).ToArray();
        var outcome = ConsensusEvaluator.Evaluate(Settings(), votes);

        outcome.Kind.Should().Be(ConsensusKind.Verified);
        outcome.Accepted[ClassificationField.Specialty].Key.Should().Be("s1");
        outcome.Accepted[ClassificationField.DocumentType].Key.Should().Be("Exam");
        outcome.Accepted[ClassificationField.AcademicYear].Key.Should().Be("y1");
        outcome.Accepted[ClassificationField.Session].Key.Should().Be("se1");
    }

    [Theory]
    [InlineData(66, ConsensusKind.Verified)]   // 2/3 = 66.7%
    [InlineData(67, ConsensusKind.NeedsReview)] // 2/3 < 67%
    [InlineData(100, ConsensusKind.NeedsReview)]
    public void Agreement_threshold_is_honoured_exactly(int percent, ConsensusKind expected)
    {
        var votes = new[] { Classify("a", "Exam"), Classify("a", "Exam"), Classify("b", "Exam") };
        ConsensusEvaluator.Evaluate(Settings(agreement: percent), votes).Kind.Should().Be(expected);
    }

    [Fact]
    public void Disagreement_on_a_required_field_is_a_conflict_for_the_admin()
    {
        var votes = new[] { Classify("a", "Exam"), Classify("b", "Exam"), Classify("c", "Exam") };
        var outcome = ConsensusEvaluator.Evaluate(Settings(), votes);

        outcome.Kind.Should().Be(ConsensusKind.NeedsReview);
        outcome.Reason.Should().Be(ReviewReasons.Conflict);
        outcome.Detail.Should().Contain("Specialty");
    }

    [Fact]
    public void Abstaining_on_a_required_field_counts_against_agreement()
    {
        var votes = new[] { Classify("a", "Exam"), Classify(null, "Exam"), Classify(null, "Exam") };
        var outcome = ConsensusEvaluator.Evaluate(Settings(), votes);

        outcome.Kind.Should().Be(ConsensusKind.NeedsReview);
        outcome.Reason.Should().Be(ReviewReasons.InsufficientAgreement);
    }

    [Fact]
    public void A_tie_for_first_place_never_verifies_even_at_a_low_threshold()
    {
        var votes = new[] { Classify("a", "Exam"), Classify("a", "Exam"), Classify("b", "Exam"), Classify("b", "Exam") };
        ConsensusEvaluator.Evaluate(Settings(voters: 4, agreement: 51), votes).Kind.Should().Be(ConsensusKind.NeedsReview);
    }

    [Fact]
    public void Optional_fields_need_two_providers_and_never_block_verification()
    {
        var onlyOneYear = new[] { Classify("s", "Exam", year: "y1"), Classify("s", "Exam"), Classify("s", "Exam") };
        var a = ConsensusEvaluator.Evaluate(Settings(), onlyOneYear);
        a.Kind.Should().Be(ConsensusKind.Verified);
        a.Accepted.Should().NotContainKey(ClassificationField.AcademicYear);

        var disputedYear = new[] { Classify("s", "Exam", year: "y1"), Classify("s", "Exam", year: "y2"), Classify("s", "Exam") };
        var b = ConsensusEvaluator.Evaluate(Settings(), disputedYear);
        b.Kind.Should().Be(ConsensusKind.Verified);
        b.Accepted.Should().NotContainKey(ClassificationField.AcademicYear);

        var agreedYear = new[] { Classify("s", "Exam", year: "y1"), Classify("s", "Exam", year: "y1"), Classify("s", "Exam") };
        ConsensusEvaluator.Evaluate(Settings(), agreedYear).Accepted[ClassificationField.AcademicYear].Key.Should().Be("y1");
    }

    [Fact]
    public void Required_fields_are_configurable()
    {
        var votes = new[] { Classify("a", year: "y"), Classify("b", year: "y"), Classify("c", year: "y") };
        var outcome = ConsensusEvaluator.Evaluate(Settings(required: ClassificationField.AcademicYear), votes);

        outcome.Kind.Should().Be(ConsensusKind.Verified);
        outcome.Accepted.Should().ContainKey(ClassificationField.AcademicYear).And.NotContainKey(ClassificationField.Specialty);
    }

    [Fact]
    public void An_agreed_pending_proposal_cannot_auto_verify()
    {
        var pending = new VoteSnapshot(VoteDecision.Classify, Specialty: V("proposal-1", true), DocumentType: V("Exam"));
        var outcome = ConsensusEvaluator.Evaluate(Settings(), new[] { pending, pending, pending });

        outcome.Kind.Should().Be(ConsensusKind.NeedsReview);
        outcome.Reason.Should().Be(ReviewReasons.PendingTaxonomy);
    }

    [Fact]
    public void A_pending_value_does_not_win_over_an_approved_one_with_more_support()
    {
        var votes = new[]
        {
            new VoteSnapshot(VoteDecision.Classify, Specialty: V("real"), DocumentType: V("Exam")),
            new VoteSnapshot(VoteDecision.Classify, Specialty: V("real"), DocumentType: V("Exam")),
            new VoteSnapshot(VoteDecision.Classify, Specialty: V("proposal", true), DocumentType: V("Exam")),
        };
        var outcome = ConsensusEvaluator.Evaluate(Settings(), votes);

        outcome.Kind.Should().Be(ConsensusKind.Verified);
        outcome.Accepted[ClassificationField.Specialty].Key.Should().Be("real");
    }

    [Fact]
    public void A_non_educational_supermajority_triggers_the_policy_outcome()
    {
        var outcome = ConsensusEvaluator.Evaluate(Settings(), new[] { NotEdu(), NotEdu(), NotEdu() });
        outcome.Kind.Should().Be(ConsensusKind.NonEducational);
        outcome.Reason.Should().Be(ReviewReasons.NonEducationalPolicy);
    }

    [Theory]
    [InlineData(66, 2, ConsensusKind.NonEducational)] // 2/3 ≥ 66
    [InlineData(67, 2, ConsensusKind.NeedsReview)]    // 2/3 < 67 and the single classifier cannot carry the field
    [InlineData(66, 1, ConsensusKind.Verified)]       // a lone dissent is outvoted
    public void The_non_educational_threshold_is_configurable(int percent, int notEducationalVotes, ConsensusKind expected)
    {
        var votes = Enumerable.Range(0, 3).Select(i => i < notEducationalVotes ? NotEdu() : Classify("s", "Exam")).ToList();
        if (notEducationalVotes == 1)
        {
            votes[1] = Classify("s", "Exam");
        }

        ConsensusEvaluator.Evaluate(Settings(nonEdu: percent), votes).Kind.Should().Be(expected);
    }

    [Fact]
    public void A_single_required_voter_decides_alone_when_configured_so()
    {
        ConsensusEvaluator.Evaluate(Settings(voters: 1), new[] { Classify("s", "Exam") }).Kind.Should().Be(ConsensusKind.Verified);
    }

    [Fact]
    public void Votes_without_any_agreed_field_never_verify_an_empty_classification()
    {
        var outcome = ConsensusEvaluator.Evaluate(
            Settings(required: ClassificationField.None),
            new[] { Classify("a"), Classify("b"), Classify("c") });

        outcome.Kind.Should().Be(ConsensusKind.NeedsReview);
    }
}
