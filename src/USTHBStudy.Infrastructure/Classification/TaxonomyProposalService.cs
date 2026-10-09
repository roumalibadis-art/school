namespace USTHBStudy.Infrastructure.Classification;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Classification;
using USTHBStudy.Application.Common;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Domain.Common;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Academic;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>
/// User-submitted taxonomy values. A proposal is only a pending record: it never becomes a specialty,
/// department, year or exam type (and is never searchable) until an administrator approves it.
/// </summary>
public sealed class TaxonomyProposalService : ITaxonomyProposalService
{
    private const int SimilarLimit = 5;
    private const int CatalogueCap = 5000;

    // Common French/English spellings that should resolve to an existing DocumentType instead of a proposal.
    private static readonly Dictionary<string, DocumentType> DocumentTypeAliases = new()
    {
        ["cours"] = DocumentType.Course, ["lecon"] = DocumentType.Course, ["lecture"] = DocumentType.Course,
        ["travauxdiriges"] = DocumentType.TD, ["travauxpratiques"] = DocumentType.TP,
        ["examen"] = DocumentType.Exam, ["examenfinal"] = DocumentType.Exam,
        ["corrigeexamen"] = DocumentType.ExamSolution, ["corrigedexamen"] = DocumentType.ExamSolution,
        ["controle"] = DocumentType.Test, ["controlecontinu"] = DocumentType.Test, ["devoir"] = DocumentType.Test,
        ["corrigecontrole"] = DocumentType.TestSolution,
        ["exercice"] = DocumentType.Exercise, ["exercices"] = DocumentType.Exercise, ["serie"] = DocumentType.Exercise,
        ["corrigeexercice"] = DocumentType.ExerciseSolution,
        ["resume"] = DocumentType.Summary, ["synthese"] = DocumentType.Summary, ["fiche"] = DocumentType.Summary,
    };

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly AcademicCacheSignal _cacheSignal;

    public TaxonomyProposalService(
        AppDbContext db, IDateTimeProvider clock, ICurrentUser currentUser, IAuditLogger audit, AcademicCacheSignal cacheSignal)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _cacheSignal = cacheSignal;
    }

    // ------------------------------------------------------------------ propose

    public async Task<ProposeResultDto> ProposeAsync(Guid userId, ProposeRequest request, CancellationToken ct = default)
    {
        await ActiveUser.EnsureAsync(_db, userId, ct);
        var category = Enum.Parse<ProposalCategory>(request.Category, true);
        var value = TaxonomyText.Normalize(request.Value);
        if (TaxonomyText.Validate(category, value) is { } error)
        {
            throw new ValidationAppException(new[] { $"value: {error}" });
        }

        var key = TaxonomyText.Key(value);
        var similar = await FindSimilarCoreAsync(category, value, request.ParentId, ct);

        // 1. Already a real value? Point the user at it instead of creating noise.
        var exact = await FindExactAsync(category, value, request.ParentId, ct);
        if (exact is not null)
        {
            return new ProposeResultDto("ExistingValue", null, exact, similar);
        }

        var parentId = await ValidateParentAsync(category, request.ParentId, ct);
        var dedupe = DedupeKey(category, parentId, key);

        // 2. Already proposed? Share the proposal so independent voters can agree on it.
        var existingProposal = await _db.TaxonomyProposals.AsNoTracking().FirstOrDefaultAsync(p => p.DedupeKey == dedupe, ct);
        if (existingProposal is not null)
        {
            return await ExistingProposalResultAsync(userId, existingProposal, similar, ct);
        }

        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        var pendingCount = await _db.TaxonomyProposals.CountAsync(
            p => p.SubmittedById == userId && p.Status == ProposalStatus.Pending, ct);
        if (pendingCount >= settings.MaxPendingProposalsPerUser)
        {
            throw new BadRequestException(
                $"You already have {pendingCount} proposals awaiting review. Please wait for them to be reviewed.");
        }

        // The document is context for the reviewer — only accept one the user is actually classifying.
        Guid? documentId = null;
        if (request.DocumentId is { } docId
            && await _db.ClassificationAssignments.AnyAsync(a => a.DocumentId == docId && a.UserId == userId, ct))
        {
            documentId = docId;
        }

        var proposal = new TaxonomyProposal
        {
            Category = category,
            Value = value,
            DedupeKey = dedupe,
            ParentId = parentId,
            DocumentId = documentId,
            SubmittedById = userId,
            SubmittedAt = _clock.UtcNow,
        };
        _db.TaxonomyProposals.Add(proposal);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with an identical proposal — share that one.
            _db.Entry(proposal).State = EntityState.Detached;
            var winner = await _db.TaxonomyProposals.AsNoTracking().FirstAsync(p => p.DedupeKey == dedupe, ct);
            return await ExistingProposalResultAsync(userId, winner, similar, ct);
        }

        await _audit.WriteAsync("taxonomy.proposed", "TaxonomyProposal", proposal.Id.ToString(),
            new { category = category.ToString(), value, parentId, documentId }, ct);

        return new ProposeResultDto("Created", (await MapAsync(new[] { proposal }, ct))[0], null, similar);
    }

    /// <summary>Students see a shared proposal, never who else submitted it (that is for reviewers only).</summary>
    private static ProposalDto Anonymise(ProposalDto dto, Guid viewerId) =>
        dto.SubmittedById == viewerId ? dto : dto with { SubmittedById = Guid.Empty, SubmittedByEmail = null };

    private async Task<ProposeResultDto> ExistingProposalResultAsync(
        Guid viewerId, TaxonomyProposal existing, IReadOnlyList<SimilarValueDto> similar, CancellationToken ct)
    {
        if (existing.Status == ProposalStatus.Rejected)
        {
            throw new BadRequestException("This value was already reviewed and rejected by an administrator.");
        }

        if (existing.Status is ProposalStatus.Approved or ProposalStatus.Merged)
        {
            var resolved = await ResolvedValueAsync(existing, ct);
            return new ProposeResultDto("ExistingValue", null, resolved, similar);
        }

        return new ProposeResultDto("ExistingProposal", Anonymise((await MapAsync(new[] { existing }, ct))[0], viewerId), null, similar);
    }

    private static string DedupeKey(ProposalCategory category, Guid? parentId, string key) =>
        $"{(int)category}|{parentId?.ToString("N") ?? string.Empty}|{key}";

    private async Task<Guid?> ValidateParentAsync(ProposalCategory category, Guid? parentId, CancellationToken ct)
    {
        if (parentId is not { } id)
        {
            return null;
        }

        return category switch
        {
            ProposalCategory.Specialty when await _db.Departments.AnyAsync(d => d.Id == id, ct) => id,
            ProposalCategory.Department when await _db.Faculties.AnyAsync(f => f.Id == id, ct) => id,
            ProposalCategory.Specialty or ProposalCategory.Department =>
                throw new NotFoundException(category == ProposalCategory.Specialty ? "Department" : "Faculty", id),
            _ => null,
        };
    }

    // ------------------------------------------------------------------ duplicate / similarity

    public async Task<IReadOnlyList<SimilarValueDto>> FindSimilarAsync(
        string category, string value, Guid? parentId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ProposalCategory>(category, true, out var parsed))
        {
            throw new BadRequestException("Unknown taxonomy category.");
        }

        return await FindSimilarCoreAsync(parsed, TaxonomyText.Normalize(value), parentId, ct);
    }

    private async Task<SimilarValueDto?> FindExactAsync(
        ProposalCategory category, string value, Guid? parentId, CancellationToken ct)
    {
        var key = TaxonomyText.Key(value);
        return (await CatalogueAsync(category, parentId, ct)).FirstOrDefault(c => c.Key == key)?.Dto;
    }

    private async Task<IReadOnlyList<SimilarValueDto>> FindSimilarCoreAsync(
        ProposalCategory category, string value, Guid? parentId, CancellationToken ct)
    {
        var key = TaxonomyText.Key(value);
        if (key.Length == 0)
        {
            return Array.Empty<SimilarValueDto>();
        }

        var catalogue = await CatalogueAsync(category, parentId, ct);
        var pending = await _db.TaxonomyProposals.AsNoTracking()
            .Where(p => p.Category == category && p.Status == ProposalStatus.Pending)
            .Take(CatalogueCap)
            .Select(p => new { p.Id, p.Value })
            .ToListAsync(ct);

        return catalogue
            .Select(c => (c.Key, c.Dto))
            .Concat(pending.Select(p => (TaxonomyText.Key(p.Value), new SimilarValueDto(p.Value, p.Id, null, true))))
            .Where(c => TaxonomyText.AreSimilar(key, c.Item1))
            .OrderBy(c => TaxonomyText.Levenshtein(key, c.Item1))
            .Select(c => c.Item2)
            .Take(SimilarLimit)
            .ToArray();
    }

    private sealed record CatalogueEntry(string Key, SimilarValueDto Dto);

    /// <summary>Existing approved values for a category (scoped to the parent when one is given).</summary>
    private async Task<List<CatalogueEntry>> CatalogueAsync(ProposalCategory category, Guid? parentId, CancellationToken ct)
    {
        switch (category)
        {
            case ProposalCategory.Specialty:
            {
                var q = _db.Specialties.AsNoTracking().Where(s => s.IsActive);
                if (parentId is { } d)
                {
                    q = q.Where(s => s.DepartmentId == d);
                }

                return (await q.Take(CatalogueCap).Select(s => new { s.Id, s.Name }).ToListAsync(ct))
                    .Select(s => new CatalogueEntry(TaxonomyText.Key(s.Name), new SimilarValueDto(s.Name, s.Id, null, false))).ToList();
            }

            case ProposalCategory.Department:
            {
                var q = _db.Departments.AsNoTracking().Where(s => s.IsActive);
                if (parentId is { } f)
                {
                    q = q.Where(s => s.FacultyId == f);
                }

                return (await q.Take(CatalogueCap).Select(s => new { s.Id, s.Name }).ToListAsync(ct))
                    .Select(s => new CatalogueEntry(TaxonomyText.Key(s.Name), new SimilarValueDto(s.Name, s.Id, null, false))).ToList();
            }

            case ProposalCategory.AcademicYear:
                return (await _db.AcademicYears.AsNoTracking().Where(y => y.IsActive).Take(CatalogueCap)
                        .Select(y => new { y.Id, y.Name, y.StartYear, y.EndYear }).ToListAsync(ct))
                    .Select(y => new CatalogueEntry(
                        TaxonomyText.Key(TaxonomyText.AcademicYearName(y.StartYear, y.EndYear)),
                        new SimilarValueDto(y.Name, y.Id, null, false))).ToList();

            case ProposalCategory.Session:
                return (await _db.Sessions.AsNoTracking().Where(s => s.IsActive).Take(CatalogueCap)
                        .Select(s => new { s.Id, s.Name }).ToListAsync(ct))
                    .Select(s => new CatalogueEntry(TaxonomyText.Key(s.Name), new SimilarValueDto(s.Name, s.Id, null, false))).ToList();

            default:
            {
                var entries = Enum.GetValues<DocumentType>()
                    .Select(t => new CatalogueEntry(TaxonomyText.Key(t.ToString()), new SimilarValueDto(t.ToString(), null, t.ToString(), false)))
                    .ToList();
                entries.AddRange(DocumentTypeAliases.Select(a =>
                    new CatalogueEntry(a.Key, new SimilarValueDto(a.Value.ToString(), null, a.Value.ToString(), false))));
                return entries;
            }
        }
    }

    private async Task<SimilarValueDto?> ResolvedValueAsync(TaxonomyProposal p, CancellationToken ct)
    {
        if (p.ResolvedDocumentType is { } type)
        {
            return new SimilarValueDto(type.ToString(), null, type.ToString(), false);
        }

        if (p.ResolvedEntityId is not { } id)
        {
            return null;
        }

        var name = await EntityNameAsync(p.Category, id, ct);
        return name is null ? null : new SimilarValueDto(name, id, null, false);
    }

    private async Task<string?> EntityNameAsync(ProposalCategory category, Guid id, CancellationToken ct) => category switch
    {
        ProposalCategory.Specialty => await _db.Specialties.IgnoreQueryFilters().Where(e => e.Id == id).Select(e => e.Name).FirstOrDefaultAsync(ct),
        ProposalCategory.Department => await _db.Departments.IgnoreQueryFilters().Where(e => e.Id == id).Select(e => e.Name).FirstOrDefaultAsync(ct),
        ProposalCategory.AcademicYear => await _db.AcademicYears.IgnoreQueryFilters().Where(e => e.Id == id).Select(e => e.Name).FirstOrDefaultAsync(ct),
        ProposalCategory.Session => await _db.Sessions.IgnoreQueryFilters().Where(e => e.Id == id).Select(e => e.Name).FirstOrDefaultAsync(ct),
        _ => null,
    };

    // ------------------------------------------------------------------ admin: list / detail

    public async Task<PagedResult<ProposalDto>> ListAsync(ProposalQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };
        var q = _db.TaxonomyProposals.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status) && !string.Equals(query.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<ProposalStatus>(query.Status, true, out var status))
            {
                throw new BadRequestException("Unknown proposal status.");
            }

            q = q.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            if (!Enum.TryParse<ProposalCategory>(query.Category, true, out var category))
            {
                throw new BadRequestException("Unknown taxonomy category.");
            }

            q = q.Where(p => p.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            q = q.Where(p => EF.Functions.Like(p.Value, term));
        }

        var total = await q.LongCountAsync(ct);
        var rows = await q.OrderBy(p => p.Status).ThenBy(p => p.SubmittedAt)
            .Skip(paging.Skip).Take(paging.Take).ToListAsync(ct);

        return new PagedResult<ProposalDto>(await MapAsync(rows, ct), paging.Page, paging.PageSize, total);
    }

    public async Task<ProposalDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var proposal = await _db.TaxonomyProposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                       ?? throw new NotFoundException("TaxonomyProposal", id);

        var voteDocIds = await _db.ClassificationVotes.AsNoTracking()
            .Where(v => v.SpecialtyProposalId == id || v.DepartmentProposalId == id || v.DocumentTypeProposalId == id
                        || v.AcademicYearProposalId == id || v.SessionProposalId == id)
            .Select(v => v.DocumentId).Distinct().ToListAsync(ct);
        if (proposal.DocumentId is { } contextDoc)
        {
            voteDocIds.Add(contextDoc);
        }

        var docs = await _db.Documents.AsNoTracking()
            .Where(d => voteDocIds.Contains(d.Id))
            .Select(d => new ProposalDocumentDto(d.Id, d.Title, d.Slug, d.VerificationStatus.ToString()))
            .Take(50).ToListAsync(ct);

        return new ProposalDetailDto((await MapAsync(new[] { proposal }, ct))[0], docs);
    }

    // ------------------------------------------------------------------ admin: decisions

    public async Task<ProposalDto> ApproveAsync(Guid id, ApproveProposalRequest request, CancellationToken ct = default)
    {
        var proposal = await RequirePendingAsync(id, ct);

        if (proposal.Category == ProposalCategory.DocumentType)
        {
            throw new BadRequestException(
                "Document types are a fixed list. Merge this proposal into an existing type, or reject it.");
        }

        var name = TaxonomyText.Normalize(request.Name ?? proposal.ApprovedName ?? proposal.Value);
        if (TaxonomyText.Validate(proposal.Category, name) is { } error)
        {
            throw new ValidationAppException(new[] { $"name: {error}" });
        }

        var parentId = request.ParentId ?? proposal.ParentId;
        var duplicate = await FindExactAsync(proposal.Category, name, parentId, ct);
        if (duplicate is not null)
        {
            throw new ConflictException($"'{duplicate.Name}' already exists. Merge this proposal into it instead.");
        }

        var entityId = proposal.Category switch
        {
            ProposalCategory.Specialty => await CreateSpecialtyAsync(name, parentId, ct),
            ProposalCategory.Department => await CreateDepartmentAsync(name, parentId, ct),
            ProposalCategory.AcademicYear => await CreateAcademicYearAsync(name, ct),
            ProposalCategory.Session => await CreateSessionAsync(name, ct),
            _ => throw new BadRequestException("Unsupported category."),
        };

        proposal.Status = ProposalStatus.Approved;
        proposal.ApprovedName = name;
        proposal.ResolvedEntityId = entityId;
        Stamp(proposal, request.Note);

        var affected = await RedirectVotesAsync(proposal, entityId, null, ct);
        await _db.SaveChangesAsync(ct);
        _cacheSignal.Bump();

        await ReevaluateAsync(affected, ct);
        await _audit.WriteAsync("taxonomy.approved", "TaxonomyProposal", id.ToString(),
            new { category = proposal.Category.ToString(), value = proposal.Value, approvedName = name, entityId, parentId }, ct);
        return (await MapAsync(new[] { proposal }, ct))[0];
    }

    public async Task<ProposalDto> RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        var proposal = await RequirePendingAsync(id, ct);
        var normalized = TaxonomyText.Normalize(name);
        if (TaxonomyText.Validate(proposal.Category, normalized) is { } error)
        {
            throw new ValidationAppException(new[] { $"name: {error}" });
        }

        var duplicate = await FindExactAsync(proposal.Category, normalized, proposal.ParentId, ct);
        if (duplicate is not null)
        {
            throw new ConflictException($"'{duplicate.Name}' already exists. Merge this proposal into it instead.");
        }

        var before = proposal.ApprovedName ?? proposal.Value;
        proposal.ApprovedName = normalized;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("taxonomy.renamed", "TaxonomyProposal", id.ToString(), new { from = before, to = normalized }, ct);
        return (await MapAsync(new[] { proposal }, ct))[0];
    }

    public async Task<ProposalDto> MergeAsync(Guid id, MergeProposalRequest request, CancellationToken ct = default)
    {
        var proposal = await RequirePendingAsync(id, ct);
        Guid? targetId = null;
        DocumentType? targetType = null;
        string targetName;

        if (proposal.Category == ProposalCategory.DocumentType)
        {
            if (!Enum.TryParse<DocumentType>(request.TargetDocumentType, true, out var type) || !Enum.IsDefined(type))
            {
                throw new BadRequestException("Choose the existing document type to merge into.");
            }

            targetType = type;
            targetName = type.ToString();
        }
        else
        {
            targetId = request.TargetId ?? throw new BadRequestException("Choose the existing value to merge into.");
            targetName = await EntityNameAsync(proposal.Category, targetId.Value, ct)
                         ?? throw new NotFoundException(proposal.Category.ToString(), targetId.Value);
        }

        proposal.Status = ProposalStatus.Merged;
        proposal.ResolvedEntityId = targetId;
        proposal.ResolvedDocumentType = targetType;
        Stamp(proposal, request.Note);

        var affected = await RedirectVotesAsync(proposal, targetId, targetType, ct);
        await _db.SaveChangesAsync(ct);

        await ReevaluateAsync(affected, ct);
        await _audit.WriteAsync("taxonomy.merged", "TaxonomyProposal", id.ToString(),
            new { category = proposal.Category.ToString(), value = proposal.Value, into = targetName, targetId, targetType }, ct);
        return (await MapAsync(new[] { proposal }, ct))[0];
    }

    public async Task<ProposalDto> RejectAsync(Guid id, string? note, CancellationToken ct = default)
    {
        var proposal = await RequirePendingAsync(id, ct);
        proposal.Status = ProposalStatus.Rejected;
        Stamp(proposal, note);

        // Votes that relied on the rejected value simply lose that field.
        var affected = await RedirectVotesAsync(proposal, null, null, ct);
        await _db.SaveChangesAsync(ct);

        await ReevaluateAsync(affected, ct);
        await _audit.WriteAsync("taxonomy.rejected", "TaxonomyProposal", id.ToString(),
            new { category = proposal.Category.ToString(), value = proposal.Value, note }, ct);
        return (await MapAsync(new[] { proposal }, ct))[0];
    }

    private async Task<TaxonomyProposal> RequirePendingAsync(Guid id, CancellationToken ct)
    {
        var proposal = await _db.TaxonomyProposals.FirstOrDefaultAsync(p => p.Id == id, ct)
                       ?? throw new NotFoundException("TaxonomyProposal", id);
        if (proposal.Status != ProposalStatus.Pending)
        {
            throw new ConflictException($"This proposal was already {proposal.Status.ToString().ToLowerInvariant()}.");
        }

        return proposal;
    }

    private void Stamp(TaxonomyProposal proposal, string? note)
    {
        proposal.ReviewedById = _currentUser.UserId;
        proposal.ReviewedAt = _clock.UtcNow;
        proposal.AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>
    /// Re-points every vote that referenced the proposal at its resolution (or drops the field when there is
    /// none) and returns the affected documents so consensus can be re-evaluated.
    /// </summary>
    private async Task<List<Guid>> RedirectVotesAsync(
        TaxonomyProposal proposal, Guid? entityId, DocumentType? type, CancellationToken ct)
    {
        var id = proposal.Id;
        var votes = await _db.ClassificationVotes
            .Where(v => v.SpecialtyProposalId == id || v.DepartmentProposalId == id || v.DocumentTypeProposalId == id
                        || v.AcademicYearProposalId == id || v.SessionProposalId == id)
            .ToListAsync(ct);

        foreach (var v in votes)
        {
            switch (proposal.Category)
            {
                case ProposalCategory.Specialty:
                    v.SpecialtyProposalId = null;
                    v.SpecialtyId = entityId;
                    break;
                case ProposalCategory.Department:
                    v.DepartmentProposalId = null;
                    v.DepartmentId = entityId;
                    break;
                case ProposalCategory.AcademicYear:
                    v.AcademicYearProposalId = null;
                    v.AcademicYearId = entityId;
                    break;
                case ProposalCategory.Session:
                    v.SessionProposalId = null;
                    v.SessionId = entityId;
                    break;
                default:
                    v.DocumentTypeProposalId = null;
                    v.DocumentType = type;
                    break;
            }
        }

        return votes.Select(v => v.DocumentId).Distinct().ToList();
    }

    /// <summary>
    /// A resolved proposal can change what the votes on a document mean. Documents that were parked only because
    /// of pending taxonomy (or are still collecting votes) are evaluated again with the corrected votes.
    /// </summary>
    private async Task ReevaluateAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken ct)
    {
        if (documentIds.Count == 0)
        {
            return;
        }

        var settings = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct);
        var now = _clock.UtcNow;
        var audits = new List<ConsensusAudit>();

        foreach (var documentId in documentIds)
        {
            await ConcurrencyRetry.RunAsync(_db, async () =>
            {
                var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
                if (doc is null
                    || doc.ClassificationStatus != ClassificationStatus.Unclassified
                    || !(doc.VerificationStatus == VerificationStatus.Pending
                         || (doc.VerificationStatus == VerificationStatus.NeedsReview
                             && doc.ClassificationReviewReason == ReviewReasons.PendingTaxonomy)))
                {
                    return 0;
                }

                var votes = await _db.ClassificationVotes
                    .Where(v => v.DocumentId == documentId && v.Round == doc.VotingRound).ToListAsync(ct);
                var outcome = ConsensusEvaluator.Evaluate(settings, votes.Select(ClassificationService.ToSnapshot).ToList());
                if (outcome.Kind == ConsensusKind.Pending)
                {
                    return 0;
                }

                doc.ClassificationVersion++;
                audits.Add(await ConsensusApplier.ApplyOutcomeAsync(_db, doc, votes, outcome, settings, now, ct));
                await _db.SaveChangesAsync(ct);
                return 0;
            });
        }

        foreach (var audit in audits)
        {
            await _audit.WriteAsync(audit.Action, "Document", audit.DocumentId.ToString(), audit.Metadata, ct);
        }
    }

    // ------------------------------------------------------------------ entity creation on approval

    private async Task<Guid> CreateSpecialtyAsync(string name, Guid? departmentId, CancellationToken ct)
    {
        var parent = departmentId ?? throw new BadRequestException("Choose the department this specialty belongs to.");
        if (!await _db.Departments.AnyAsync(d => d.Id == parent, ct))
        {
            throw new NotFoundException("Department", parent);
        }

        var entity = new Specialty { Name = name, DepartmentId = parent, IsActive = true };
        entity.Slug = await UniqueSlugAsync(_db.Specialties, name, "specialty", ct);
        _db.Specialties.Add(entity);
        return entity.Id;
    }

    private async Task<Guid> CreateDepartmentAsync(string name, Guid? facultyId, CancellationToken ct)
    {
        var parent = facultyId ?? throw new BadRequestException("Choose the faculty this department belongs to.");
        if (!await _db.Faculties.AnyAsync(f => f.Id == parent, ct))
        {
            throw new NotFoundException("Faculty", parent);
        }

        var entity = new Department { Name = name, FacultyId = parent, IsActive = true };
        entity.Slug = await UniqueSlugAsync(_db.Departments, name, "department", ct);
        _db.Departments.Add(entity);
        return entity.Id;
    }

    private async Task<Guid> CreateAcademicYearAsync(string name, CancellationToken ct)
    {
        TaxonomyText.TryParseAcademicYear(name, out var start, out var end);
        var canonical = TaxonomyText.AcademicYearName(start, end);
        var entity = new AcademicYear { Name = canonical, StartYear = start, EndYear = end, IsActive = true };
        entity.Slug = await UniqueSlugAsync(_db.AcademicYears, canonical, "academic-year", ct);
        _db.AcademicYears.Add(entity);
        return entity.Id;
    }

    private async Task<Guid> CreateSessionAsync(string name, CancellationToken ct)
    {
        var order = (await _db.Sessions.IgnoreQueryFilters().MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1;
        var entity = new Session { Name = name, Kind = SessionKind.Other, Order = order, IsActive = true };
        entity.Slug = await UniqueSlugAsync(_db.Sessions, name, "session", ct);
        _db.Sessions.Add(entity);
        return entity.Id;
    }

    private static async Task<string> UniqueSlugAsync<T>(DbSet<T> set, string text, string fallback, CancellationToken ct)
        where T : AcademicEntity
    {
        var baseSlug = Slugifier.Slugify(text);
        if (baseSlug.Length == 0)
        {
            baseSlug = fallback;
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await set.IgnoreQueryFilters().AnyAsync(e => e.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    // ------------------------------------------------------------------ mapping

    private async Task<IReadOnlyList<ProposalDto>> MapAsync(IReadOnlyList<TaxonomyProposal> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<ProposalDto>();
        }

        var ids = rows.Select(r => r.Id).ToList();
        var userIds = rows.Select(r => r.SubmittedById).Distinct().ToList();
        var emails = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        var docIds = rows.Where(r => r.DocumentId != null).Select(r => r.DocumentId!.Value).Distinct().ToList();
        var docTitles = await _db.Documents.AsNoTracking().Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Title, ct);

        // Vote counts per proposal (any field).
        var voteCounts = new Dictionary<Guid, int>();
        var refs = await _db.ClassificationVotes.AsNoTracking()
            .Where(v => (v.SpecialtyProposalId != null && ids.Contains(v.SpecialtyProposalId.Value))
                        || (v.DepartmentProposalId != null && ids.Contains(v.DepartmentProposalId.Value))
                        || (v.DocumentTypeProposalId != null && ids.Contains(v.DocumentTypeProposalId.Value))
                        || (v.AcademicYearProposalId != null && ids.Contains(v.AcademicYearProposalId.Value))
                        || (v.SessionProposalId != null && ids.Contains(v.SessionProposalId.Value)))
            .Select(v => new { v.SpecialtyProposalId, v.DepartmentProposalId, v.DocumentTypeProposalId, v.AcademicYearProposalId, v.SessionProposalId })
            .ToListAsync(ct);
        foreach (var r in refs)
        {
            foreach (var pid in new[] { r.SpecialtyProposalId, r.DepartmentProposalId, r.DocumentTypeProposalId, r.AcademicYearProposalId, r.SessionProposalId })
            {
                if (pid is { } p && ids.Contains(p))
                {
                    voteCounts[p] = voteCounts.GetValueOrDefault(p) + 1;
                }
            }
        }

        var list = new List<ProposalDto>(rows.Count);
        foreach (var r in rows)
        {
            string? parentName = null;
            if (r.ParentId is { } parent)
            {
                parentName = r.Category == ProposalCategory.Specialty
                    ? await _db.Departments.IgnoreQueryFilters().Where(d => d.Id == parent).Select(d => d.Name).FirstOrDefaultAsync(ct)
                    : await _db.Faculties.IgnoreQueryFilters().Where(f => f.Id == parent).Select(f => f.Name).FirstOrDefaultAsync(ct);
            }

            string? resolvedName = r.ResolvedDocumentType?.ToString();
            if (resolvedName is null && r.ResolvedEntityId is { } entity)
            {
                resolvedName = await EntityNameAsync(r.Category, entity, ct);
            }

            list.Add(new ProposalDto(
                r.Id, r.Category.ToString(), r.Value, r.Status.ToString(), r.ParentId, parentName,
                r.DocumentId, r.DocumentId is { } d && docTitles.TryGetValue(d, out var title) ? title : null,
                r.SubmittedById, emails.GetValueOrDefault(r.SubmittedById), r.SubmittedAt,
                r.ReviewedById, r.ReviewedAt, r.AdminNote, r.ResolvedEntityId, resolvedName,
                r.ResolvedDocumentType?.ToString(), r.ApprovedName, voteCounts.GetValueOrDefault(r.Id)));
        }

        return list;
    }
}
