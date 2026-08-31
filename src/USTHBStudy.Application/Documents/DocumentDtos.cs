namespace USTHBStudy.Application.Documents;

public sealed record DocumentDto(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string Type,
    string Status,
    Guid ModuleId,
    Guid? AcademicYearId,
    Guid? SessionId,
    string FileName,
    long FileSize,
    int? PageCount,
    string MimeType,
    bool IsPremium,
    string? Source,
    string RightsStatus,
    bool HasPreview,
    long ViewCount,
    long DownloadCount,
    Guid? SolutionForDocumentId,
    IReadOnlyList<Guid> SolutionDocumentIds,
    DateTime? PublishedAt,
    DateTime CreatedAt);

/// <summary>Metadata for an upload — the binary is carried separately as a <see cref="DocumentFile"/>.</summary>
public sealed record DocumentUploadRequest(
    string Title,
    string Type,
    Guid ModuleId,
    Guid? AcademicYearId,
    Guid? SessionId,
    string? Description,
    bool IsPremium,
    string? Source,
    string? RightsStatus,
    string? PermissionNotes);

public sealed record DocumentMetadataUpdate(
    string Title,
    string Type,
    Guid ModuleId,
    Guid? AcademicYearId,
    Guid? SessionId,
    string? Description,
    bool IsPremium,
    string? Source,
    string? RightsStatus,
    string? PermissionNotes);

public sealed record DocumentStatusChange(string Status, string? Note);

/// <summary>Streamed file plus what the client claimed about it (claims are validated, not trusted — §36).</summary>
public sealed record DocumentFile(Stream Content, string FileName, string? ClientContentType, long Length);

public sealed record DocumentContent(Stream Stream, string ContentType, string FileName);

/// <summary>Upload outcome: the created document, and any existing document with the same file hash (§75).</summary>
public sealed record DocumentUploadResult(DocumentDto Document, DocumentDto? PossibleDuplicate);

public sealed record DocumentQuery(
    Guid? ModuleId = null,
    Guid? SpecialtyId = null,
    Guid? AcademicYearId = null,
    Guid? SessionId = null,
    string? Type = null,
    string? Status = null,
    bool? IsPremium = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
