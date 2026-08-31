namespace USTHBStudy.Infrastructure.Documents;

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Documents;
using USTHBStudy.Application.Students;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Persistence;

public sealed class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IPdfProcessor _pdf;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityService _activity;
    private readonly IAccessControlService _access;
    private readonly IDownloadTokenService _downloadTokens;
    private readonly IDateTimeProvider _clock;
    private readonly DocumentOptions _options;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        AppDbContext db,
        IFileStorageService storage,
        IPdfProcessor pdf,
        ICurrentUser currentUser,
        IActivityService activity,
        IAccessControlService access,
        IDownloadTokenService downloadTokens,
        IDateTimeProvider clock,
        IOptions<DocumentOptions> options,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _storage = storage;
        _pdf = pdf;
        _currentUser = currentUser;
        _activity = activity;
        _access = access;
        _downloadTokens = downloadTokens;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    private bool IsStaff => _currentUser.HasPermission(Permissions.Documents.Publish)
                            || _currentUser.HasPermission(Permissions.Documents.Update);

    public async Task<DocumentUploadResult> UploadAsync(
        DocumentUploadRequest request, DocumentFile file, CancellationToken ct = default)
    {
        var type = Enum.Parse<DocumentType>(request.Type, ignoreCase: true);

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw new BadRequestException($"The file exceeds the {_options.MaxFileSizeBytes / (1024 * 1024)} MB limit.");
        }

        await using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        if (bytes.LongLength > _options.MaxFileSizeBytes)
        {
            throw new BadRequestException($"The file exceeds the {_options.MaxFileSizeBytes / (1024 * 1024)} MB limit.");
        }

        var inspection = FileValidation.Inspect(file.FileName, bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        if (!inspection.IsValid)
        {
            throw new BadRequestException(inspection.Error!);
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var duplicate = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.FileHashSha256 == hash, ct);

        await EnsureClassificationExistsAsync(request.ModuleId, request.AcademicYearId, request.SessionId, ct);

        var id = Guid.NewGuid();
        var slug = await UniqueSlugAsync(request.Title, id, ct);
        var prefix = $"documents/{type.ToString().ToLowerInvariant()}";

        var stored = await _storage.SaveAsync(
            new MemoryStream(bytes, writable: false), prefix, file.FileName, inspection.MimeType, ct);

        var (previewKey, thumbnailKey, pageCount) = await TryRenderPreviewAsync(bytes, inspection.MimeType, prefix, slug, ct);

        var document = new Document
        {
            Id = id,
            Title = request.Title.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            Type = type,
            Status = DocumentStatus.Draft,
            ModuleId = request.ModuleId,
            AcademicYearId = request.AcademicYearId,
            SessionId = request.SessionId,
            FileStorageKey = stored.StorageKey,
            PreviewStorageKey = previewKey,
            ThumbnailStorageKey = thumbnailKey,
            FileName = stored.FileName,
            FileSize = stored.SizeBytes,
            PageCount = pageCount,
            MimeType = inspection.MimeType,
            FileHashSha256 = hash,
            IsPremium = request.IsPremium,
            Source = request.Source?.Trim(),
            RightsStatus = ParseRights(request.RightsStatus),
            PermissionNotes = request.PermissionNotes?.Trim(),
            UploadedById = _currentUser.UserId,
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        return new DocumentUploadResult(Map(document), duplicate is null ? null : Map(duplicate));
    }

    public async Task<PagedResult<DocumentDto>> ListAsync(DocumentQuery query, CancellationToken ct = default)
    {
        var q = _db.Documents.AsNoTracking();

        if (!IsStaff)
        {
            q = q.Where(d => d.Status == DocumentStatus.Published);
        }
        else if (query.Status is not null && Enum.TryParse<DocumentStatus>(query.Status, true, out var status))
        {
            q = q.Where(d => d.Status == status);
        }

        if (query.ModuleId is { } moduleId)
        {
            q = q.Where(d => d.ModuleId == moduleId);
        }

        if (query.SpecialtyId is { } specialtyId)
        {
            q = q.Where(d => d.Module!.SpecialtyId == specialtyId);
        }

        if (query.AcademicYearId is { } yearId)
        {
            q = q.Where(d => d.AcademicYearId == yearId);
        }

        if (query.SessionId is { } sessionId)
        {
            q = q.Where(d => d.SessionId == sessionId);
        }

        if (query.IsPremium is { } isPremium)
        {
            q = q.Where(d => d.IsPremium == isPremium);
        }

        if (query.Type is not null && Enum.TryParse<DocumentType>(query.Type, true, out var type))
        {
            q = q.Where(d => d.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(d => EF.Functions.Like(d.Title, $"%{term}%"));
        }

        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };
        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.PublishedAt ?? d.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Take)
            .ToListAsync(ct);

        return new PagedResult<DocumentDto>(items.Select(Map).ToArray(), paging.Page, paging.PageSize, total);
    }

    public async Task<DocumentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking()
            .Include(d => d.Solutions)
            .FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Document", id);

        EnsureVisible(document);
        return Map(document);
    }

    public async Task<DocumentDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking()
            .Include(d => d.Solutions)
            .FirstOrDefaultAsync(d => d.Slug == slug, ct)
            ?? throw new NotFoundException("Document", slug);

        EnsureVisible(document);

        if (document.Status == DocumentStatus.Published)
        {
            await _db.Documents.Where(d => d.Id == document.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ViewCount, d => d.ViewCount + 1), ct);

            if (_currentUser.UserId is { } viewerId)
            {
                await _activity.RecordDocumentViewAsync(viewerId, document.Id, ct);
            }
        }

        return Map(document);
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, DocumentMetadataUpdate update, CancellationToken ct = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct)
                       ?? throw new NotFoundException("Document", id);

        await EnsureClassificationExistsAsync(update.ModuleId, update.AcademicYearId, update.SessionId, ct);

        if (!string.Equals(document.Title, update.Title.Trim(), StringComparison.Ordinal))
        {
            document.Slug = await UniqueSlugAsync(update.Title, document.Id, ct);
        }

        document.Title = update.Title.Trim();
        document.Description = update.Description?.Trim();
        document.Type = Enum.Parse<DocumentType>(update.Type, ignoreCase: true);
        document.ModuleId = update.ModuleId;
        document.AcademicYearId = update.AcademicYearId;
        document.SessionId = update.SessionId;
        document.IsPremium = update.IsPremium;
        document.Source = update.Source?.Trim();
        document.RightsStatus = ParseRights(update.RightsStatus);
        document.PermissionNotes = update.PermissionNotes?.Trim();

        await _db.SaveChangesAsync(ct);
        return Map(document);
    }

    public async Task<DocumentDto> ChangeStatusAsync(Guid id, DocumentStatusChange change, CancellationToken ct = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct)
                       ?? throw new NotFoundException("Document", id);

        var target = Enum.Parse<DocumentStatus>(change.Status, ignoreCase: true);

        if (target == DocumentStatus.Published && string.IsNullOrWhiteSpace(document.FileStorageKey))
        {
            throw new BadRequestException("A document without a stored file cannot be published.");
        }

        document.Status = target;
        document.ReviewNote = change.Note?.Trim();

        if (target == DocumentStatus.Published)
        {
            document.PublishedAt ??= _clock.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Map(document);
    }

    public async Task LinkSolutionAsync(Guid parentDocumentId, Guid solutionDocumentId, CancellationToken ct = default)
    {
        if (parentDocumentId == solutionDocumentId)
        {
            throw new BadRequestException("A document cannot be its own solution.");
        }

        _ = await _db.Documents.FirstOrDefaultAsync(d => d.Id == parentDocumentId, ct)
            ?? throw new NotFoundException("Document", parentDocumentId);
        var solution = await _db.Documents.FirstOrDefaultAsync(d => d.Id == solutionDocumentId, ct)
                       ?? throw new NotFoundException("Document", solutionDocumentId);

        solution.SolutionForDocumentId = parentDocumentId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct)
                       ?? throw new NotFoundException("Document", id);

        document.IsDeleted = true;
        document.DeletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DocumentContent> OpenPreviewAsync(string slug, CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Slug == slug, ct)
                       ?? throw new NotFoundException("Document", slug);

        EnsureVisible(document);

        if (document.PreviewStorageKey is null)
        {
            throw new NotFoundException("This document has no preview image.");
        }

        var stream = await _storage.OpenReadAsync(document.PreviewStorageKey, ct);
        return new DocumentContent(stream, "image/png", $"{document.Slug}-preview.png");
    }

    public async Task<DownloadTicket> RequestDownloadAsync(string slug, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException("Sign in to download this document.");

        var document = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Slug == slug, ct)
                       ?? throw new NotFoundException("Document", slug);
        EnsureVisible(document);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new UnauthorizedAppException();

        _access.EnsureCanAccess(document.IsPremium, new AccessSubject(user.IsActive, user.IsPremium, user.PremiumExpiresAt));

        var lifetime = TimeSpan.FromSeconds(_options.DownloadLinkSeconds);
        var expiresAt = _clock.UtcNow.Add(lifetime);

        var presigned = await _storage.TryCreatePresignedUrlAsync(document.FileStorageKey, lifetime, document.FileName, ct);
        var url = presigned?.ToString()
                  ?? $"/api/files?t={Uri.EscapeDataString(_downloadTokens.Issue(
                      new DownloadGrant(document.FileStorageKey, document.Id, userId, expiresAt)))}";

        await _db.Documents.Where(d => d.Id == document.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.DownloadCount, d => d.DownloadCount + 1), ct);
        await _activity.RecordDocumentDownloadAsync(userId, document.Id, ct);

        return new DownloadTicket(url, expiresAt, document.FileName);
    }

    public async Task<DocumentContent> OpenDownloadAsync(string token, CancellationToken ct = default)
    {
        var grant = _downloadTokens.Verify(token)
                    ?? throw new UnauthorizedAppException("This download link is invalid or has expired.");

        var meta = await _db.Documents.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Id == grant.DocumentId)
            .Select(d => new { d.FileName, d.MimeType })
            .FirstOrDefaultAsync(ct);

        var stream = await _storage.OpenReadAsync(grant.StorageKey, ct);
        return new DocumentContent(stream, meta?.MimeType ?? "application/octet-stream", meta?.FileName ?? "document");
    }

    private async Task<(string? PreviewKey, string? ThumbnailKey, int? PageCount)> TryRenderPreviewAsync(
        byte[] bytes, string mimeType, string prefix, string slug, CancellationToken ct)
    {
        if (!_pdf.CanProcess(mimeType))
        {
            return (null, null, null);
        }

        try
        {
            var render = await _pdf.RenderAsync(new MemoryStream(bytes, writable: false), ct);

            var previewKey = (await _storage.SaveAsync(
                new MemoryStream(render.PreviewPng), $"{prefix}/previews", $"{slug}-preview.png", "image/png", ct)).StorageKey;
            var thumbnailKey = (await _storage.SaveAsync(
                new MemoryStream(render.ThumbnailPng), $"{prefix}/thumbnails", $"{slug}-thumb.png", "image/png", ct)).StorageKey;

            return (previewKey, thumbnailKey, render.PageCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF preview generation failed for {Slug}; the document is stored without a preview.", slug);
            return (null, null, null);
        }
    }

    private async Task EnsureClassificationExistsAsync(Guid moduleId, Guid? yearId, Guid? sessionId, CancellationToken ct)
    {
        if (!await _db.Modules.AnyAsync(m => m.Id == moduleId, ct))
        {
            throw new NotFoundException("Module", moduleId);
        }

        if (yearId is { } y && !await _db.AcademicYears.AnyAsync(a => a.Id == y, ct))
        {
            throw new NotFoundException("AcademicYear", y);
        }

        if (sessionId is { } s && !await _db.Sessions.AnyAsync(x => x.Id == s, ct))
        {
            throw new NotFoundException("Session", s);
        }
    }

    private void EnsureVisible(Document document)
    {
        if (document.Status != DocumentStatus.Published && !IsStaff)
        {
            // Do not disclose that a non-published document exists.
            throw new NotFoundException("Document", document.Slug);
        }
    }

    private async Task<string> UniqueSlugAsync(string title, Guid excludeId, CancellationToken ct)
    {
        var baseSlug = Slugifier.Slugify(title);
        if (baseSlug.Length == 0)
        {
            baseSlug = "document";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Documents.IgnoreQueryFilters().AnyAsync(d => d.Slug == slug && d.Id != excludeId, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private static RightsStatus ParseRights(string? value) =>
        value is not null && Enum.TryParse<RightsStatus>(value, ignoreCase: true, out var parsed)
            ? parsed
            : RightsStatus.Unknown;

    private static DocumentDto Map(Document d) => new(
        d.Id,
        d.Title,
        d.Slug,
        d.Description,
        d.Type.ToString(),
        d.Status.ToString(),
        d.ModuleId,
        d.AcademicYearId,
        d.SessionId,
        d.FileName,
        d.FileSize,
        d.PageCount,
        d.MimeType,
        d.IsPremium,
        d.Source,
        d.RightsStatus.ToString(),
        d.PreviewStorageKey is not null,
        d.ViewCount,
        d.DownloadCount,
        d.SolutionForDocumentId,
        d.Solutions.Select(s => s.Id).ToArray(),
        d.PublishedAt,
        d.CreatedAt);
}
