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

/// <summary>
/// The student-facing community classification workflow: prompts, task assignment, voting, skipping and
/// consensus. Every mutation of a document's voting state is guarded by the document's
/// <c>ClassificationVersion</c> concurrency token and the (document, user, round) unique indexes, so two
/// requests racing for the same slot or vote can never both win.
/// </summary>
public sealed class ClassificationService : IClassificationService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IFileStorageService _storage;
    private readonly IAuditLogger _audit;

    public ClassificationService(
        AppDbContext db, IDateTimeProvider clock, IFileStorageService storage, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _storage = storage;
        _audit = audit;
    }

    // ------------------------------------------------------------------ prompt

    public async Task<ClassificationPromptDto> GetPromptAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        await StatsOps.EnsureAsync(_db, _clock, userId, ct);
        var stats = await _db.ContributionStats.AsNoTracking().FirstAsync(s => s.UserId == userId, ct);
        var quota = DownloadQuotaService.BuildStatus(settings, stats, exempt: false);
        var now = _clock.UtcNow;

        var open = await OpenTaskAsync(userId, now, ct);
        if (open is not null)
        {
            var remaining = open.Assignments.Count(a => a.Status == AssignmentStatus.Assigned);
            return new ClassificationPromptDto(false, null, true, remaining, settings.DocumentsPerTask, 0, quota);
        }

        var available = await CandidatesAsync(userId, settings, now, settings.DocumentsPerTask, ct);
        var reason = PromptReason(settings, stats, now);

        return new ClassificationPromptDto(
            reason is not null && available.Count > 0,
            available.Count > 0 ? reason : null,
            false, 0, settings.DocumentsPerTask, available.Count, quota);
    }

    public async Task<ClassificationTaskDto?> AcknowledgePromptAsync(
        Guid userId, string action, CancellationToken ct = default)
    {
        await ActiveUser.EnsureAsync(_db, userId, ct);
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        await StatsOps.EnsureAsync(_db, _clock, userId, ct);
        var stats = await _db.ContributionStats.AsNoTracking().FirstAsync(s => s.UserId == userId, ct);
        var now = _clock.UtcNow;
        var reason = PromptReason(settings, stats, now);
        var trigger = reason switch
        {
            "login" => ContributionTrigger.Login,
            "downloads" => ContributionTrigger.Downloads,
            _ => ContributionTrigger.Manual,
        };

        // Consume the trigger (login stamp and download counter) whichever way the user answers, so the
        // same prompt is never shown twice and triggers stay coordinated.
        var snoozeUntil = string.Equals(action, "later", StringComparison.OrdinalIgnoreCase)
            ? now.AddMinutes(settings.PromptSnoozeMinutes)
            : (DateTime?)null;
        await _db.ContributionStats.Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.LastPromptAt, now)
                .SetProperty(s => s.DownloadsAtLastPrompt, s => s.TotalDownloads)
                .SetProperty(s => s.SnoozedUntil, snoozeUntil), ct);

        if (snoozeUntil is not null)
        {
            return null;
        }

        return await CreateOrResumeTaskAsync(userId, trigger, ct);
    }

    private static string? PromptReason(ClassificationSettings s, UserContributionStats stats, DateTime now)
    {
        if (stats.SnoozedUntil is { } until && until > now)
        {
            return null;
        }

        if (s.LoginTriggerEnabled && stats.LastLoginAt is { } login && (stats.LastPromptAt is null || login > stats.LastPromptAt))
        {
            return "login";
        }

        if (s.DownloadTriggerEnabled && stats.TotalDownloads - stats.DownloadsAtLastPrompt >= s.DownloadsPerPrompt)
        {
            return "downloads";
        }

        return null;
    }

    // ------------------------------------------------------------------ tasks

    public Task<ClassificationTaskDto?> GetOrCreateTaskAsync(Guid userId, CancellationToken ct = default) =>
        CreateOrResumeTaskAsync(userId, ContributionTrigger.Manual, ct);

    private async Task<ClassificationTaskDto?> CreateOrResumeTaskAsync(
        Guid userId, ContributionTrigger trigger, CancellationToken ct)
    {
        await ActiveUser.EnsureAsync(_db, userId, ct);
        return await ConcurrencyRetry.RunAsync(_db, async () =>
        {
            var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
            var now = _clock.UtcNow;

            var open = await OpenTaskAsync(userId, now, ct);
            if (open is not null)
            {
                return await MapTaskAsync(open, ct);
            }

            var candidates = await CandidatesAsync(userId, settings, now, settings.DocumentsPerTask, ct);
            if (candidates.Count == 0)
            {
                return null;
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var task = new ClassificationTask
            {
                UserId = userId,
                Trigger = trigger,
                CreatedAt = now,
                ExpiresAt = now.AddHours(settings.AssignmentExpiryHours),
            };
            _db.ClassificationTasks.Add(task);

            var documents = await _db.Documents.Where(d => candidates.Contains(d.Id)).ToListAsync(ct);
            foreach (var doc in documents)
            {
                task.Assignments.Add(new ClassificationAssignment
                {
                    DocumentId = doc.Id,
                    UserId = userId,
                    Round = doc.VotingRound,
                    AssignedAt = now,
                    ExpiresAt = task.ExpiresAt,
                });
                // Touching the token makes a concurrent assignment/vote on the same document conflict.
                doc.ClassificationVersion++;
            }

            await _db.SaveChangesAsync(ct);

            var assigned = documents.Count;
            await _db.ContributionStats.Where(s => s.UserId == userId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.TasksAssigned, s => s.TasksAssigned + 1)
                    .SetProperty(s => s.DocumentsAssigned, s => s.DocumentsAssigned + assigned), ct);

            await tx.CommitAsync(ct);

            var reloaded = await _db.ClassificationTasks.Include(t => t.Assignments).AsNoTracking()
                .FirstAsync(t => t.Id == task.Id, ct);
            return await MapTaskAsync(reloaded, ct);
        });
    }

    /// <summary>The user's live open task; stale tasks are expired (and their slots freed) on the way.</summary>
    private async Task<ClassificationTask?> OpenTaskAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var stale = await _db.ClassificationTasks
            .Where(t => t.UserId == userId && t.Status == ClassificationTaskStatus.Open && t.ExpiresAt <= now)
            .ToListAsync(ct);
        if (stale.Count > 0)
        {
            var staleIds = stale.Select(t => t.Id).ToList();
            foreach (var task in stale)
            {
                task.Status = ClassificationTaskStatus.Expired;
            }

            await _db.ClassificationAssignments
                .Where(a => staleIds.Contains(a.TaskId) && a.Status == AssignmentStatus.Assigned)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(a => a.Status, AssignmentStatus.Expired)
                    .SetProperty(a => a.ResolvedAt, now), ct);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        return await _db.ClassificationTasks.AsNoTracking()
            .Include(t => t.Assignments)
            .Where(t => t.UserId == userId && t.Status == ClassificationTaskStatus.Open && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Documents this user may be asked to classify: unclassified, in voting, with a free slot in the current
    /// round, never previously assigned to or voted on by this user, and not uploaded by them. Documents
    /// closest to reaching quorum come first so they resolve sooner.
    /// </summary>
    private async Task<List<Guid>> CandidatesAsync(
        Guid userId, ClassificationSettings settings, DateTime now, int take, CancellationToken ct)
    {
        var required = settings.RequiredVoters;

        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.ClassificationStatus == ClassificationStatus.Unclassified
                        && d.VerificationStatus == VerificationStatus.Pending
                        && d.Status != DocumentStatus.Rejected
                        && d.Status != DocumentStatus.Archived
                        && d.UploadedById != userId)
            .Where(d => !_db.ClassificationAssignments.Any(a =>
                a.DocumentId == d.Id && a.UserId == userId && a.Round == d.VotingRound))
            .Select(d => new
            {
                d.Id,
                d.CreatedAt,
                Taken = _db.ClassificationAssignments.Count(a =>
                    a.DocumentId == d.Id && a.Round == d.VotingRound
                    && (a.Status == AssignmentStatus.Completed
                        || (a.Status == AssignmentStatus.Assigned && a.ExpiresAt > now))),
            })
            .Where(x => x.Taken < required)
            .OrderByDescending(x => x.Taken).ThenBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(r => r.Id).ToList();
    }

    private async Task<ClassificationTaskDto> MapTaskAsync(ClassificationTask task, CancellationToken ct)
    {
        var docIds = task.Assignments.Select(a => a.DocumentId).ToList();
        var docs = await _db.Documents.AsNoTracking().Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        var items = task.Assignments
            .OrderBy(a => a.AssignedAt).ThenBy(a => a.Id)
            .Where(a => docs.ContainsKey(a.DocumentId))
            .Select(a =>
            {
                var d = docs[a.DocumentId];
                return new TaskItemDto(
                    a.Id, d.Id, a.Status.ToString(), d.Title, d.FileName, d.FileSize, d.PageCount, d.MimeType,
                    d.PreviewStorageKey is not null, d.Description, d.Source, d.CreatedAt);
            })
            .ToArray();

        return new ClassificationTaskDto(
            task.Id, task.Trigger.ToString(), task.ExpiresAt, items.Length,
            items.Count(i => i.Status != nameof(AssignmentStatus.Assigned)), items);
    }

    // ------------------------------------------------------------------ voting

    public async Task<VoteResultDto> SubmitVoteAsync(
        Guid userId, Guid assignmentId, SubmitVoteRequest request, CancellationToken ct = default)
    {
        await ActiveUser.EnsureAsync(_db, userId, ct);
        ConsensusAudit? audit = null;
        VoteResultDto result;

        try
        {
            (result, audit) = await ConcurrencyRetry.RunAsync(_db, () => SubmitOnceAsync(userId, assignmentId, request, ct));
        }
        catch (DbUpdateException)
        {
            // A unique index fired: the same user already holds a vote for this document/round (double
            // submit, or two racing requests). Never surface provider errors.
            _db.ChangeTracker.Clear();
            throw new ConflictException("You have already classified this document.");
        }

        if (audit is not null)
        {
            await _audit.WriteAsync(audit.Action, "Document", audit.DocumentId.ToString(), audit.Metadata, ct);
        }

        return result;
    }

    private async Task<(VoteResultDto, ConsensusAudit?)> SubmitOnceAsync(
        Guid userId, Guid assignmentId, SubmitVoteRequest request, CancellationToken ct)
    {
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        var now = _clock.UtcNow;

        var assignment = await _db.ClassificationAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
        if (assignment is null || assignment.UserId != userId)
        {
            // Same answer for "not yours" and "does not exist" — no probing of other users' assignments.
            throw new NotFoundException("Assignment", assignmentId);
        }

        if (assignment.Status != AssignmentStatus.Assigned)
        {
            throw new ConflictException(assignment.Status == AssignmentStatus.Completed
                ? "You have already classified this document."
                : "This assignment is no longer active.");
        }

        if (assignment.ExpiresAt <= now)
        {
            throw new ConflictException("This assignment has expired. Request a new task.");
        }

        if ((now - assignment.AssignedAt).TotalSeconds < settings.MinSecondsBeforeVote)
        {
            throw new BadRequestException("Please look at the document before answering.");
        }

        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == assignment.DocumentId, ct)
                       ?? throw new NotFoundException("Document", assignment.DocumentId);

        if (document.VotingRound != assignment.Round
            || document.ClassificationStatus != ClassificationStatus.Unclassified
            || document.VerificationStatus != VerificationStatus.Pending)
        {
            assignment.Status = AssignmentStatus.Expired;
            assignment.ResolvedAt = now;
            await _db.SaveChangesAsync(ct);
            throw new ConflictException("This document is no longer open for classification.");
        }

        if (document.UploadedById == userId)
        {
            throw new ForbiddenAppException("You cannot classify a document you uploaded.");
        }

        var decision = Enum.Parse<VoteDecision>(request.Decision, true);
        var vote = new ClassificationVote
        {
            DocumentId = document.Id,
            UserId = userId,
            Round = assignment.Round,
            AssignmentId = assignment.Id,
            Decision = decision,
            CreatedAt = now,
        };

        if (decision == VoteDecision.Classify)
        {
            await ApplyFieldsAsync(vote, request, ct);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Reward bookkeeping first (atomic, inside the transaction): a rolled-back vote rolls these back too.
        await StatsOps.EnsureAsync(_db, _clock, userId, ct);
        await StatsOps.ResetRewardDayIfDueAsync(_db, userId, now.Date, ct);
        vote.Rewarded = await StatsOps.TryClaimDailyRewardAsync(_db, userId, settings.MaxRewardedContributionsPerDay, ct);

        _db.ClassificationVotes.Add(vote);
        assignment.Status = AssignmentStatus.Completed;
        assignment.ResolvedAt = now;
        document.ClassificationVersion++;

        // Evaluate consensus over every vote of this round (existing + the one being added).
        var existing = await _db.ClassificationVotes
            .Where(v => v.DocumentId == document.Id && v.Round == assignment.Round)
            .ToListAsync(ct);
        var allVotes = existing.Append(vote).ToList();

        ConsensusAudit? audit = null;
        var outcome = ConsensusEvaluator.Evaluate(settings, allVotes.Select(ToSnapshot).ToList());
        if (outcome.Kind != ConsensusKind.Pending)
        {
            audit = await ConsensusApplier.ApplyOutcomeAsync(_db, document, allVotes, outcome, settings, now, ct);
        }

        await TryCompleteTaskAsync(assignment.TaskId, assignment.Id, now, ct);
        await _db.SaveChangesAsync(ct);

        var bonus = 0;
        if (vote.Rewarded)
        {
            await _db.ContributionStats.Where(s => s.UserId == userId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ValidContributions, s => s.ValidContributions + 1), ct);

            if (settings.QuotaEnabled)
            {
                await StatsOps.ResetWindowIfDueAsync(_db, userId, now, settings.QuotaWindowDays, ct);
                await StatsOps.AddBonusAsync(_db, userId, settings.BonusDownloadsPerContribution, settings.MaxBonusPerWindow, ct);
                bonus = settings.BonusDownloadsPerContribution;
            }
        }

        await tx.CommitAsync(ct);

        var task = await _db.ClassificationTasks.AsNoTracking().Include(t => t.Assignments)
            .FirstAsync(t => t.Id == assignment.TaskId, ct);
        var remaining = task.Assignments.Count(a => a.Status == AssignmentStatus.Assigned);
        var stats = await _db.ContributionStats.AsNoTracking().FirstAsync(s => s.UserId == userId, ct);

        // The voter is told their answer was recorded — never the running tally — to avoid bandwagoning.
        return (new VoteResultDto(
            document.Id, "Recorded", vote.Rewarded, bonus, remaining,
            task.Status == ClassificationTaskStatus.Completed,
            DownloadQuotaService.BuildStatus(settings, stats, exempt: false)), audit);
    }

    private async Task ApplyFieldsAsync(ClassificationVote vote, SubmitVoteRequest r, CancellationToken ct)
    {
        if (r.SpecialtyId is null && r.SpecialtyProposalId is null
            && r.DepartmentId is null && r.DepartmentProposalId is null
            && string.IsNullOrWhiteSpace(r.DocumentType) && r.DocumentTypeProposalId is null
            && r.AcademicYearId is null && r.AcademicYearProposalId is null
            && r.SessionId is null && r.SessionProposalId is null)
        {
            throw new BadRequestException("Choose at least one value, mark the document as not educational, or skip it.");
        }

        EnsureExclusive(r.SpecialtyId, r.SpecialtyProposalId, "specialty");
        EnsureExclusive(r.DepartmentId, r.DepartmentProposalId, "department");
        EnsureExclusive(r.AcademicYearId, r.AcademicYearProposalId, "academic year");
        EnsureExclusive(r.SessionId, r.SessionProposalId, "exam type");
        if (!string.IsNullOrWhiteSpace(r.DocumentType) && r.DocumentTypeProposalId is not null)
        {
            throw new BadRequestException("Choose either an existing document type or a proposal, not both.");
        }

        if (r.SpecialtyId is { } specialtyId)
        {
            var specialty = await _db.Specialties.AsNoTracking().FirstOrDefaultAsync(s => s.Id == specialtyId && s.IsActive, ct)
                            ?? throw new NotFoundException("Specialty", specialtyId);
            if (r.DepartmentId is { } d && d != specialty.DepartmentId)
            {
                throw new BadRequestException("The selected specialty does not belong to the selected department.");
            }

            vote.SpecialtyId = specialtyId;
        }

        if (r.DepartmentId is { } departmentId)
        {
            if (!await _db.Departments.AnyAsync(d => d.Id == departmentId && d.IsActive, ct))
            {
                throw new NotFoundException("Department", departmentId);
            }

            vote.DepartmentId = departmentId;
        }

        if (r.AcademicYearId is { } yearId)
        {
            if (!await _db.AcademicYears.AnyAsync(y => y.Id == yearId && y.IsActive, ct))
            {
                throw new NotFoundException("AcademicYear", yearId);
            }

            vote.AcademicYearId = yearId;
        }

        if (r.SessionId is { } sessionId)
        {
            if (!await _db.Sessions.AnyAsync(s => s.Id == sessionId && s.IsActive, ct))
            {
                throw new NotFoundException("Session", sessionId);
            }

            vote.SessionId = sessionId;
        }

        if (!string.IsNullOrWhiteSpace(r.DocumentType))
        {
            if (!Enum.TryParse<DocumentType>(r.DocumentType, true, out var type) || !Enum.IsDefined(type))
            {
                throw new BadRequestException("Unknown document type.");
            }

            vote.DocumentType = type;
        }

        vote.SpecialtyProposalId = await RequireProposalAsync(r.SpecialtyProposalId, ProposalCategory.Specialty, ct);
        vote.DepartmentProposalId = await RequireProposalAsync(r.DepartmentProposalId, ProposalCategory.Department, ct);
        vote.DocumentTypeProposalId = await RequireProposalAsync(r.DocumentTypeProposalId, ProposalCategory.DocumentType, ct);
        vote.AcademicYearProposalId = await RequireProposalAsync(r.AcademicYearProposalId, ProposalCategory.AcademicYear, ct);
        vote.SessionProposalId = await RequireProposalAsync(r.SessionProposalId, ProposalCategory.Session, ct);
    }

    private static void EnsureExclusive(Guid? id, Guid? proposalId, string label)
    {
        if (id is not null && proposalId is not null)
        {
            throw new BadRequestException($"Choose either an existing {label} or a proposal, not both.");
        }
    }

    private async Task<Guid?> RequireProposalAsync(Guid? proposalId, ProposalCategory category, CancellationToken ct)
    {
        if (proposalId is not { } id)
        {
            return null;
        }

        var ok = await _db.TaxonomyProposals.AnyAsync(
            p => p.Id == id && p.Category == category && p.Status == ProposalStatus.Pending, ct);
        return ok ? id : throw new BadRequestException("That proposed value is no longer pending.");
    }

    internal static VoteSnapshot ToSnapshot(ClassificationVote v) => new(
        v.Decision,
        Value(v.SpecialtyId, v.SpecialtyProposalId),
        Value(v.DepartmentId, v.DepartmentProposalId),
        v.DocumentType is { } t
            ? new FieldValue(t.ToString(), false)
            : v.DocumentTypeProposalId is { } p ? new FieldValue(p.ToString(), true) : null,
        Value(v.AcademicYearId, v.AcademicYearProposalId),
        Value(v.SessionId, v.SessionProposalId));

    private static FieldValue? Value(Guid? id, Guid? proposalId) =>
        id is { } i ? new FieldValue(i.ToString(), false)
        : proposalId is { } p ? new FieldValue(p.ToString(), true)
        : null;

    /// <summary>
    /// Marks the task completed once nothing else in it is still waiting for an answer.
    /// <paramref name="resolvingAssignmentId"/> is the assignment being resolved in the current (unsaved) change.
    /// </summary>
    private async Task TryCompleteTaskAsync(Guid taskId, Guid resolvingAssignmentId, DateTime now, CancellationToken ct)
    {
        var stillPending = await _db.ClassificationAssignments
            .CountAsync(a => a.TaskId == taskId && a.Id != resolvingAssignmentId && a.Status == AssignmentStatus.Assigned, ct);
        if (stillPending > 0)
        {
            return;
        }

        var task = await _db.ClassificationTasks.FirstAsync(t => t.Id == taskId, ct);
        if (task.Status != ClassificationTaskStatus.Open)
        {
            return;
        }

        task.Status = ClassificationTaskStatus.Completed;
        task.CompletedAt = now;
        await _db.ContributionStats.Where(s => s.UserId == task.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.TasksCompleted, s => s.TasksCompleted + 1), ct);
    }

    // ------------------------------------------------------------------ skip

    public async Task<SkipResultDto> SkipAsync(Guid userId, Guid assignmentId, CancellationToken ct = default)
    {
        await ActiveUser.EnsureAsync(_db, userId, ct);
        return await ConcurrencyRetry.RunAsync(_db, async () =>
        {
            var now = _clock.UtcNow;
            var assignment = await _db.ClassificationAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
            if (assignment is null || assignment.UserId != userId)
            {
                throw new NotFoundException("Assignment", assignmentId);
            }

            if (assignment.Status != AssignmentStatus.Assigned)
            {
                throw new ConflictException("This assignment has already been answered.");
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // A skip frees the slot for another voter, carries no reward and no penalty, and the same user
            // is never offered this document again in this round (the assignment row remains).
            assignment.Status = AssignmentStatus.Skipped;
            assignment.ResolvedAt = now;
            await TryCompleteTaskAsync(assignment.TaskId, assignment.Id, now, ct);
            await _db.SaveChangesAsync(ct);
            await _db.ContributionStats.Where(s => s.UserId == userId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.SkippedCount, s => s.SkippedCount + 1), ct);
            await tx.CommitAsync(ct);

            var task = await _db.ClassificationTasks.AsNoTracking().Include(t => t.Assignments)
                .FirstAsync(t => t.Id == assignment.TaskId, ct);
            return new SkipResultDto(
                assignment.DocumentId,
                task.Assignments.Count(a => a.Status == AssignmentStatus.Assigned),
                task.Status == ClassificationTaskStatus.Completed);
        });
    }

    // ------------------------------------------------------------------ options / stats / preview

    public async Task<ClassificationOptionsDto> GetOptionsAsync(Guid userId, CancellationToken ct = default)
    {
        const int cap = 2000;
        var departments = await _db.Departments.AsNoTracking().Where(d => d.IsActive)
            .OrderBy(d => d.Name).Take(cap).Select(d => new OptionDto(d.Id, d.Name, d.FacultyId, false, null)).ToListAsync(ct);
        var specialties = await _db.Specialties.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Name).Take(cap).Select(s => new OptionDto(s.Id, s.Name, s.DepartmentId, false, null)).ToListAsync(ct);
        var years = await _db.AcademicYears.AsNoTracking().Where(y => y.IsActive)
            .OrderByDescending(y => y.StartYear).Take(cap).Select(y => new OptionDto(y.Id, y.Name, null, false, null)).ToListAsync(ct);
        var sessions = await _db.Sessions.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Order).ThenBy(s => s.Name).Take(cap).Select(s => new OptionDto(s.Id, s.Name, null, false, null)).ToListAsync(ct);

        // Only the caller's own pending proposals are echoed back — others' stay invisible until approved.
        var mine = await _db.TaxonomyProposals.AsNoTracking()
            .Where(p => p.SubmittedById == userId && p.Status == ProposalStatus.Pending)
            .OrderByDescending(p => p.SubmittedAt).Take(100)
            .Select(p => new OptionDto(p.Id, p.Value, p.ParentId, true, p.Id))
            .ToListAsync(ct);

        return new ClassificationOptionsDto(departments, specialties, years, sessions, DocumentTypeLabels.All, mine);
    }

    public async Task<MyContributionsDto> GetMyContributionsAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        await StatsOps.EnsureAsync(_db, _clock, userId, ct);
        if (settings.QuotaEnabled)
        {
            await StatsOps.ResetWindowIfDueAsync(_db, userId, _clock.UtcNow, settings.QuotaWindowDays, ct);
        }

        var s = await _db.ContributionStats.AsNoTracking().FirstAsync(x => x.UserId == userId, ct);
        return new MyContributionsDto(
            s.TasksAssigned, s.TasksCompleted, s.ValidContributions, s.SkippedCount, s.ResolvedVotes, s.AgreedVotes,
            DownloadQuotaService.BuildStatus(settings, s, exempt: false));
    }

    public async Task<DocumentContent> OpenAssignmentPreviewAsync(Guid userId, Guid assignmentId, CancellationToken ct = default)
    {
        await ActiveUser.EnsureAsync(_db, userId, ct);
        var now = _clock.UtcNow;
        var row = await _db.ClassificationAssignments.AsNoTracking()
            .Where(a => a.Id == assignmentId && a.UserId == userId
                        && a.Status == AssignmentStatus.Assigned && a.ExpiresAt > now)
            .Select(a => new { a.Document!.PreviewStorageKey, a.Document.Slug })
            .FirstOrDefaultAsync(ct);

        if (row?.PreviewStorageKey is null)
        {
            throw new NotFoundException("Preview not available.");
        }

        var stream = await _storage.OpenReadAsync(row.PreviewStorageKey, ct);
        return new DocumentContent(stream, "image/png", $"{row.Slug}-preview.png");
    }
}
