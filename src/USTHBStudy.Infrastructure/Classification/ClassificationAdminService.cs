namespace USTHBStudy.Infrastructure.Classification;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Classification;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Documents;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>Administrator review of community classification: queues, manual decisions, reopening, reports.</summary>
public sealed class ClassificationAdminService : IClassificationAdminService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IFileStorageService _storage;

    public ClassificationAdminService(
        AppDbContext db, IDateTimeProvider clock, ICurrentUser currentUser, IAuditLogger audit, IFileStorageService storage)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _storage = storage;
    }

    // ------------------------------------------------------------------ queues

    public async Task<PagedResult<ClassificationDocumentItem>> ListAsync(
        ClassificationQueueQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        var required = settings.RequiredVoters;

        var q = _db.Documents.AsNoTracking().AsQueryable();
        q = (query.Queue ?? "needs-review").ToLowerInvariant() switch
        {
            "pending" => q.Where(d => d.ClassificationStatus == ClassificationStatus.Unclassified
                                      && d.VerificationStatus == VerificationStatus.Pending
                                      && !_db.ClassificationVotes.Any(v => v.DocumentId == d.Id && v.Round == d.VotingRound)),
            "awaiting-votes" => q.Where(d => d.ClassificationStatus == ClassificationStatus.Unclassified
                                             && d.VerificationStatus == VerificationStatus.Pending
                                             && _db.ClassificationVotes.Any(v => v.DocumentId == d.Id && v.Round == d.VotingRound)),
            "needs-review" => q.Where(d => d.VerificationStatus == VerificationStatus.NeedsReview),
            "conflicting" => q.Where(d => d.VerificationStatus == VerificationStatus.NeedsReview
                                          && d.ClassificationReviewReason == ReviewReasons.Conflict),
            "rejected" => q.Where(d => d.VerificationStatus == VerificationStatus.Rejected
                                       || d.ClassificationStatus == ClassificationStatus.NotEducational),
            "verified" => q.Where(d => d.VerificationStatus == VerificationStatus.Verified),
            "all" => q.Where(d => d.ClassificationStatus == ClassificationStatus.Unclassified
                                  || d.VerificationStatus != VerificationStatus.Unverified),
            _ => throw new BadRequestException("Unknown queue. Use pending, awaiting-votes, needs-review, conflicting, rejected, verified or all."),
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            q = q.Where(d => EF.Functions.Like(d.Title, term));
        }

        var total = await q.LongCountAsync(ct);
        var rows = await q.OrderBy(d => d.CreatedAt)
            .Skip(paging.Skip).Take(paging.Take)
            .Select(d => new
            {
                d.Id, d.Title, d.Slug, d.Status, d.ClassificationStatus, d.VerificationStatus,
                d.ClassificationReviewReason, d.VotingRound, d.CreatedAt,
                Votes = _db.ClassificationVotes.Count(v => v.DocumentId == d.Id && v.Round == d.VotingRound),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ClassificationDocumentItem(
            r.Id, r.Title, r.Slug, r.Status.ToString(), r.ClassificationStatus.ToString(), r.VerificationStatus.ToString(),
            r.ClassificationReviewReason, r.VotingRound, r.Votes, required, r.CreatedAt)).ToArray();

        return new PagedResult<ClassificationDocumentItem>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<ClassificationDocumentDetail> GetAsync(Guid documentId, CancellationToken ct = default)
    {
        var d = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct)
                ?? throw new NotFoundException("Document", documentId);
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);

        var votes = await _db.ClassificationVotes.AsNoTracking()
            .Where(v => v.DocumentId == documentId).OrderBy(v => v.Round).ThenBy(v => v.CreatedAt).ToListAsync(ct);

        var userIds = votes.Select(v => v.UserId).Distinct().ToList();
        var emails = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        var specialties = await NamesAsync(_db.Specialties, votes.Select(v => v.SpecialtyId), ct);
        var departments = await NamesAsync(_db.Departments, votes.Select(v => v.DepartmentId), ct);
        var years = await NamesAsync(_db.AcademicYears, votes.Select(v => v.AcademicYearId), ct);
        var sessions = await NamesAsync(_db.Sessions, votes.Select(v => v.SessionId), ct);

        var proposalIds = votes.SelectMany(v => new[]
            { v.SpecialtyProposalId, v.DepartmentProposalId, v.DocumentTypeProposalId, v.AcademicYearProposalId, v.SessionProposalId })
            .Where(p => p != null).Select(p => p!.Value).Distinct().ToList();
        var proposals = await _db.TaxonomyProposals.AsNoTracking().Where(p => proposalIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => $"{p.Category}: {p.ApprovedName ?? p.Value} (proposed)", ct);

        string? Name(Dictionary<Guid, string> map, Guid? id) => id is { } i && map.TryGetValue(i, out var n) ? n : null;

        var voteDtos = votes.Select(v => new VoteDetailDto(
            v.Id, v.UserId, emails.GetValueOrDefault(v.UserId), v.Round, v.Decision.ToString(),
            Name(specialties, v.SpecialtyId), Name(departments, v.DepartmentId), v.DocumentType?.ToString(),
            Name(years, v.AcademicYearId), Name(sessions, v.SessionId),
            new[] { v.SpecialtyProposalId, v.DepartmentProposalId, v.DocumentTypeProposalId, v.AcademicYearProposalId, v.SessionProposalId }
                .Where(p => p != null).Select(p => proposals.GetValueOrDefault(p!.Value) ?? "proposal").ToArray(),
            v.AgreedWithOutcome, v.CreatedAt)).ToArray();

        var id = documentId.ToString();
        var history = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "Document" && a.EntityId == id && a.Action.StartsWith("classification."))
            .OrderBy(a => a.OccurredAt)
            .Select(a => new ClassificationHistoryEntry(a.Action, a.ActorEmail, a.Metadata, a.OccurredAt))
            .ToListAsync(ct);

        var currentVotes = votes.Count(v => v.Round == d.VotingRound);
        var summary = new ClassificationDocumentItem(
            d.Id, d.Title, d.Slug, d.Status.ToString(), d.ClassificationStatus.ToString(), d.VerificationStatus.ToString(),
            d.ClassificationReviewReason, d.VotingRound, currentVotes, settings.RequiredVoters, d.CreatedAt);

        return new ClassificationDocumentDetail(
            summary, d.FileName, d.Description, d.Source, d.ModuleId, d.SpecialtyId, d.DepartmentId, d.Type.ToString(),
            d.AcademicYearId, d.SessionId, d.PreviewStorageKey is not null, voteDtos, history);
    }

    private static async Task<Dictionary<Guid, string>> NamesAsync<T>(
        DbSet<T> set, IEnumerable<Guid?> ids, CancellationToken ct)
        where T : USTHBStudy.Domain.Common.AcademicEntity
    {
        var wanted = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        return wanted.Count == 0
            ? new Dictionary<Guid, string>()
            : await set.IgnoreQueryFilters().AsNoTracking().Where(e => wanted.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
    }

    // ------------------------------------------------------------------ decisions

    public async Task<ClassificationDocumentDetail> VerifyAsync(
        Guid documentId, AdminClassificationDecision decision, CancellationToken ct = default)
    {
        object before = null!;
        object after = null!;

        await ConcurrencyRetry.RunAsync(_db, async () =>
        {
            var doc = await RequireAsync(documentId, ct);
            before = Snapshot(doc);

            var votes = await _db.ClassificationVotes.Where(v => v.DocumentId == documentId && v.Round == doc.VotingRound).ToListAsync(ct);
            var values = await ResolveDecisionAsync(decision, votes, ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await ApplyDecisionAsync(doc, values, ct);
            if (decision.ModuleId is { } moduleId)
            {
                var module = await _db.Modules.AsNoTracking().Where(m => m.Id == moduleId)
                                 .Select(m => new { m.SpecialtyId, DepartmentId = m.Specialty!.DepartmentId }).FirstOrDefaultAsync(ct)
                             ?? throw new NotFoundException("Module", moduleId);
                doc.ModuleId = moduleId;
                doc.SpecialtyId ??= module.SpecialtyId;
                doc.DepartmentId ??= module.DepartmentId;
            }

            var now = _clock.UtcNow;
            doc.ClassificationStatus = ClassificationStatus.Classified;
            doc.VerificationStatus = VerificationStatus.Verified;
            doc.ClassificationReviewReason = null;
            doc.ClassifiedAt ??= now;
            doc.VerifiedAt = now;
            doc.VerifiedById = _currentUser.UserId;
            doc.ClassificationVersion++;

            var unresolved = votes.Where(v => v.AgreedWithOutcome is null).ToList();
            await ConsensusApplier.ResolveVotesAsync(_db, doc, unresolved, nonEducational: false, ct);
            await ExpireOpenAssignmentsAsync(doc, now, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            after = Snapshot(doc);
            return 0;
        });

        await _audit.WriteAsync("classification.admin_verified", "Document", documentId.ToString(),
            new { before, after, note = decision.Note }, ct);
        return await GetAsync(documentId, ct);
    }

    public async Task<ClassificationDocumentDetail> RejectAsync(Guid documentId, string? note, CancellationToken ct = default)
    {
        object before = null!;

        await ConcurrencyRetry.RunAsync(_db, async () =>
        {
            var doc = await RequireAsync(documentId, ct);
            before = Snapshot(doc);
            var votes = await _db.ClassificationVotes
                .Where(v => v.DocumentId == documentId && v.Round == doc.VotingRound && v.AgreedWithOutcome == null).ToListAsync(ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var now = _clock.UtcNow;
            doc.ClassificationStatus = ClassificationStatus.NotEducational;
            doc.VerificationStatus = VerificationStatus.Rejected;
            doc.Status = DocumentStatus.Rejected;
            doc.ReviewNote = string.IsNullOrWhiteSpace(note) ? "Rejected as non-educational material." : note.Trim();
            doc.ClassificationReviewReason = null;
            doc.VerifiedAt = now;
            doc.VerifiedById = _currentUser.UserId;
            doc.ClassificationVersion++;

            await ConsensusApplier.ResolveVotesAsync(_db, doc, votes, nonEducational: true, ct);
            await ExpireOpenAssignmentsAsync(doc, now, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return 0;
        });

        await _audit.WriteAsync("classification.admin_rejected", "Document", documentId.ToString(), new { before, note }, ct);
        return await GetAsync(documentId, ct);
    }

    public async Task<ClassificationDocumentDetail> ReopenAsync(Guid documentId, string? note, CancellationToken ct = default)
    {
        object before = null!;

        await ConcurrencyRetry.RunAsync(_db, async () =>
        {
            var doc = await RequireAsync(documentId, ct);
            before = Snapshot(doc);
            var now = _clock.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await ExpireOpenAssignmentsAsync(doc, now, ct);

            // A new round: earlier votes are kept as history but no longer count, and every user may vote again.
            doc.VotingRound++;
            doc.ClassificationStatus = ClassificationStatus.Unclassified;
            doc.VerificationStatus = VerificationStatus.Pending;
            doc.ClassificationReviewReason = null;
            doc.ClassifiedAt = null;
            doc.VerifiedAt = null;
            doc.VerifiedById = null;
            if (doc.Status == DocumentStatus.Rejected)
            {
                doc.Status = DocumentStatus.Draft;
                doc.ReviewNote = null;
            }

            doc.ClassificationVersion++;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return 0;
        });

        await _audit.WriteAsync("classification.admin_reopened", "Document", documentId.ToString(), new { before, note }, ct);
        return await GetAsync(documentId, ct);
    }

    private async Task<Document> RequireAsync(Guid documentId, CancellationToken ct) =>
        await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
        ?? throw new NotFoundException("Document", documentId);

    private Task ExpireOpenAssignmentsAsync(Document doc, DateTime now, CancellationToken ct)
    {
        var id = doc.Id;
        var round = doc.VotingRound;
        return _db.ClassificationAssignments
            .Where(a => a.DocumentId == id && a.Round == round && a.Status == AssignmentStatus.Assigned)
            .ExecuteUpdateAsync(u => u
                .SetProperty(a => a.Status, AssignmentStatus.Expired)
                .SetProperty(a => a.ResolvedAt, now), ct);
    }

    private static object Snapshot(Document d) => new
    {
        classification = d.ClassificationStatus.ToString(),
        verification = d.VerificationStatus.ToString(),
        round = d.VotingRound,
        type = d.Type.ToString(),
        d.ModuleId,
        d.SpecialtyId,
        d.DepartmentId,
        d.AcademicYearId,
        d.SessionId,
        status = d.Status.ToString(),
    };

    private sealed record ResolvedDecision(
        Guid? SpecialtyId, Guid? DepartmentId, DocumentType? Type, Guid? AcademicYearId, Guid? SessionId);

    /// <summary>Takes the admin's explicit values, or — when none are given — the leading vote per field.</summary>
    private async Task<ResolvedDecision> ResolveDecisionAsync(
        AdminClassificationDecision d, IReadOnlyList<ClassificationVote> votes, CancellationToken ct)
    {
        DocumentType? type = null;
        if (!string.IsNullOrWhiteSpace(d.DocumentType))
        {
            if (!Enum.TryParse<DocumentType>(d.DocumentType, true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new BadRequestException("Unknown document type.");
            }

            type = parsed;
        }

        var explicitGiven = d.SpecialtyId != null || d.DepartmentId != null || type != null
                            || d.AcademicYearId != null || d.SessionId != null || d.ModuleId != null;

        var result = new ResolvedDecision(d.SpecialtyId, d.DepartmentId, type, d.AcademicYearId, d.SessionId);
        if (!explicitGiven)
        {
            // Accept the leading approved value per field from the current round's votes.
            var classify = votes.Where(v => v.Decision == VoteDecision.Classify).ToList();
            result = new ResolvedDecision(
                Top(classify.Select(v => v.SpecialtyId)),
                Top(classify.Select(v => v.DepartmentId)),
                classify.Where(v => v.DocumentType != null).GroupBy(v => v.DocumentType)
                    .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault(),
                Top(classify.Select(v => v.AcademicYearId)),
                Top(classify.Select(v => v.SessionId)));

            if (result.SpecialtyId == null && result.DepartmentId == null && result.Type == null
                && result.AcademicYearId == null && result.SessionId == null)
            {
                throw new BadRequestException("Provide the classification values to apply: the votes do not contain a usable one.");
            }
        }

        if (result.SpecialtyId is { } sid)
        {
            var dept = await _db.Specialties.AsNoTracking().Where(s => s.Id == sid).Select(s => (Guid?)s.DepartmentId).FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException("Specialty", sid);
            if (result.DepartmentId is { } given && given != dept)
            {
                throw new BadRequestException("The specialty does not belong to the selected department.");
            }

            result = result with { DepartmentId = dept };
        }
        else if (result.DepartmentId is { } did && !await _db.Departments.AnyAsync(x => x.Id == did, ct))
        {
            throw new NotFoundException("Department", did);
        }

        if (result.AcademicYearId is { } yid && !await _db.AcademicYears.AnyAsync(x => x.Id == yid, ct))
        {
            throw new NotFoundException("AcademicYear", yid);
        }

        if (result.SessionId is { } sessionId && !await _db.Sessions.AnyAsync(x => x.Id == sessionId, ct))
        {
            throw new NotFoundException("Session", sessionId);
        }

        return result;
    }

    private static Guid? Top(IEnumerable<Guid?> values) =>
        values.Where(v => v != null).GroupBy(v => v).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

    private Task ApplyDecisionAsync(Document doc, ResolvedDecision v, CancellationToken ct)
    {
        if (v.Type is { } t)
        {
            doc.Type = t;
        }

        if (v.SpecialtyId is { } s)
        {
            doc.SpecialtyId = s;
        }

        if (v.DepartmentId is { } dep)
        {
            doc.DepartmentId = dep;
        }

        if (v.AcademicYearId is { } y)
        {
            doc.AcademicYearId = y;
        }

        if (v.SessionId is { } se)
        {
            doc.SessionId = se;
        }

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ report / preview

    public async Task<ClassificationReportDto> GetReportAsync(CancellationToken ct = default)
    {
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        var docs = _db.Documents.AsNoTracking();

        var totalDocs = await docs.LongCountAsync(ct);
        var unclassified = await docs.LongCountAsync(d => d.ClassificationStatus == ClassificationStatus.Unclassified, ct);
        var classified = await docs.LongCountAsync(d => d.ClassificationStatus == ClassificationStatus.Classified, ct);
        var verified = await docs.LongCountAsync(d => d.VerificationStatus == VerificationStatus.Verified, ct);
        var awaitingVotes = await docs.LongCountAsync(d => d.ClassificationStatus == ClassificationStatus.Unclassified
                                                          && d.VerificationStatus == VerificationStatus.Pending, ct);
        var awaitingReview = await docs.LongCountAsync(d => d.VerificationStatus == VerificationStatus.NeedsReview, ct);
        var rejected = await docs.LongCountAsync(d => d.VerificationStatus == VerificationStatus.Rejected, ct);

        var tasksTotal = await _db.ClassificationTasks.LongCountAsync(ct);
        var tasksCompleted = await _db.ClassificationTasks.LongCountAsync(t => t.Status == ClassificationTaskStatus.Completed, ct);
        var tasksExpired = await _db.ClassificationTasks.LongCountAsync(t => t.Status == ClassificationTaskStatus.Expired, ct);
        var closed = tasksCompleted + tasksExpired;

        var votes = await _db.ClassificationVotes.LongCountAsync(ct);
        var resolved = await _db.ClassificationVotes.LongCountAsync(v => v.AgreedWithOutcome != null, ct);
        var agreed = await _db.ClassificationVotes.LongCountAsync(v => v.AgreedWithOutcome == true, ct);
        var skipped = await _db.ClassificationAssignments.LongCountAsync(a => a.Status == AssignmentStatus.Skipped, ct);

        var validContributions = (long)(await _db.ContributionStats.SumAsync(s => (int?)s.ValidContributions, ct) ?? 0);
        var downloads = await _db.ContributionStats.SumAsync(s => (long?)s.TotalDownloads, ct) ?? 0;
        var active = await _db.ContributionStats.LongCountAsync(s => s.ValidContributions > 0, ct);
        var free = settings.FreeDownloadsPerWindow;
        var overQuota = settings.QuotaEnabled
            ? await _db.ContributionStats.LongCountAsync(s => s.QuotaDownloadsUsed >= free + s.QuotaBonusEarned, ct)
            : 0;

        return new ClassificationReportDto(
            totalDocs, unclassified, classified, verified, awaitingVotes, awaitingReview, rejected,
            tasksTotal, tasksCompleted, closed == 0 ? 0 : Math.Round(tasksCompleted * 100d / closed, 1),
            votes, resolved, resolved == 0 ? 0 : Math.Round(agreed * 100d / resolved, 1),
            validContributions, skipped,
            await _db.TaxonomyProposals.LongCountAsync(p => p.Status == ProposalStatus.Pending, ct),
            await _db.TaxonomyProposals.LongCountAsync(p => p.Status == ProposalStatus.Approved, ct),
            await _db.TaxonomyProposals.LongCountAsync(p => p.Status == ProposalStatus.Rejected, ct),
            await _db.TaxonomyProposals.LongCountAsync(p => p.Status == ProposalStatus.Merged, ct),
            downloads, active, overQuota);
    }

    public async Task<DocumentContent> OpenPreviewAsync(Guid documentId, CancellationToken ct = default)
    {
        var row = await _db.Documents.AsNoTracking().Where(d => d.Id == documentId)
            .Select(d => new { d.PreviewStorageKey, d.Slug }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Document", documentId);
        if (row.PreviewStorageKey is null)
        {
            throw new NotFoundException("Preview not available.");
        }

        return new DocumentContent(await _storage.OpenReadAsync(row.PreviewStorageKey, ct), "image/png", $"{row.Slug}-preview.png");
    }
}
