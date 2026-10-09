namespace USTHBStudy.Infrastructure.Classification;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Classification;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>An audit entry to be written once the surrounding transaction has committed.</summary>
public sealed record ConsensusAudit(string Action, Guid DocumentId, object Metadata);

/// <summary>
/// Applies a consensus decision (or an admin decision) to a document and its votes. Shared by the voting
/// flow, taxonomy moderation (which can change what votes mean) and the admin review actions so the rules
/// live in exactly one place.
/// </summary>
public static class ConsensusApplier
{
    /// <summary>Writes the consensus decision onto the document and records which votes agreed.</summary>
    public static async Task<ConsensusAudit> ApplyOutcomeAsync(
        AppDbContext db, Document document, IReadOnlyList<ClassificationVote> votes, ConsensusOutcome outcome,
        ClassificationSettings settings, DateTime now, CancellationToken ct)
    {
        switch (outcome.Kind)
        {
            case ConsensusKind.Verified:
                await ApplyAcceptedFieldsAsync(db, document, outcome.Accepted, ct);
                document.ClassificationStatus = ClassificationStatus.Classified;
                document.VerificationStatus = VerificationStatus.Verified;
                document.ClassificationReviewReason = null;
                document.ClassifiedAt = now;
                document.VerifiedAt = now;
                document.VerifiedById = null;
                await ResolveVotesAsync(db, document, votes, nonEducational: false, ct);
                return new ConsensusAudit("classification.verified", document.Id,
                    new { by = "consensus", round = document.VotingRound, voters = votes.Count, detail = outcome.Detail });

            case ConsensusKind.NonEducational when settings.NonEducationalPolicy == NonEducationalPolicy.AutoReject:
                document.ClassificationStatus = ClassificationStatus.NotEducational;
                document.VerificationStatus = VerificationStatus.Rejected;
                document.Status = DocumentStatus.Rejected;
                document.ReviewNote = "Rejected by community vote (not educational material).";
                document.ClassificationReviewReason = null;
                await ResolveVotesAsync(db, document, votes, nonEducational: true, ct);
                return new ConsensusAudit("classification.rejected", document.Id,
                    new { by = "consensus", policy = "AutoReject", detail = outcome.Detail });

            default:
                document.VerificationStatus = VerificationStatus.NeedsReview;
                document.ClassificationReviewReason = outcome.Reason;
                return new ConsensusAudit("classification.needs_review", document.Id,
                    new { by = "consensus", reason = outcome.Reason, detail = outcome.Detail });
        }
    }

    public static async Task ApplyAcceptedFieldsAsync(
        AppDbContext db, Document document, IReadOnlyDictionary<ClassificationField, FieldValue> accepted, CancellationToken ct)
    {
        if (accepted.TryGetValue(ClassificationField.DocumentType, out var type) && !type.IsPending
            && Enum.TryParse<DocumentType>(type.Key, out var parsedType))
        {
            document.Type = parsedType;
        }

        if (accepted.TryGetValue(ClassificationField.AcademicYear, out var year) && Guid.TryParse(year.Key, out var yearId))
        {
            document.AcademicYearId = yearId;
        }

        if (accepted.TryGetValue(ClassificationField.Session, out var session) && Guid.TryParse(session.Key, out var sessionId))
        {
            document.SessionId = sessionId;
        }

        if (accepted.TryGetValue(ClassificationField.Specialty, out var specialty) && Guid.TryParse(specialty.Key, out var specialtyId))
        {
            document.SpecialtyId = specialtyId;
            // The department follows from the specialty so the two can never disagree.
            document.DepartmentId = await db.Specialties.AsNoTracking()
                .Where(s => s.Id == specialtyId).Select(s => (Guid?)s.DepartmentId).FirstOrDefaultAsync(ct);
        }
        else if (accepted.TryGetValue(ClassificationField.Department, out var department) && Guid.TryParse(department.Key, out var departmentId))
        {
            document.DepartmentId = departmentId;
        }
    }

    /// <summary>Stamps each vote with whether it matched the final decision and updates voter agreement counters.</summary>
    public static async Task ResolveVotesAsync(
        AppDbContext db, Document document, IReadOnlyList<ClassificationVote> votes, bool nonEducational, CancellationToken ct)
    {
        foreach (var vote in votes)
        {
            vote.AgreedWithOutcome = nonEducational
                ? vote.Decision == VoteDecision.NotEducational
                : vote.Decision == VoteDecision.Classify && Matches(vote, document);
        }

        foreach (var group in votes.Where(v => v.Id != Guid.Empty).GroupBy(v => v.UserId))
        {
            var agreed = group.Count(v => v.AgreedWithOutcome == true);
            var total = group.Count();
            var userId = group.Key;
            await db.ContributionStats.Where(s => s.UserId == userId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.ResolvedVotes, s => s.ResolvedVotes + total)
                    .SetProperty(s => s.AgreedVotes, s => s.AgreedVotes + agreed), ct);
        }
    }

    public static bool Matches(ClassificationVote v, Document d) =>
        (v.SpecialtyId is null || v.SpecialtyId == d.SpecialtyId)
        && (v.DepartmentId is null || v.DepartmentId == d.DepartmentId)
        && (v.DocumentType is null || v.DocumentType == d.Type)
        && (v.AcademicYearId is null || v.AcademicYearId == d.AcademicYearId)
        && (v.SessionId is null || v.SessionId == d.SessionId)
        && v.SpecialtyProposalId is null && v.DepartmentProposalId is null && v.DocumentTypeProposalId is null
        && v.AcademicYearProposalId is null && v.SessionProposalId is null;
}
