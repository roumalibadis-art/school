namespace USTHBStudy.Application.Abstractions;

/// <summary>
/// Storage abstraction for binary content (PRD §7). Database holds only metadata; PDFs, images,
/// thumbnails and previews live behind this port. The local-filesystem implementation backs
/// development; an S3-compatible implementation is added in Phase 3. Callers never turn a
/// storage key into a public URL — downloads are brokered by the API (PRD §29).
/// </summary>
public interface IFileStorageService
{
    /// <summary>Persists <paramref name="content"/> and returns the opaque storage key to save in the DB.</summary>
    Task<StoredFile> SaveAsync(
        Stream content,
        string keyPrefix,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// For providers that natively issue time-boxed URLs (S3): a presigned GET URL for
    /// <paramref name="storageKey"/>. Returns <c>null</c> for the local provider — the caller then
    /// brokers the download through the API with a signed token (PRD §29).
    /// </summary>
    Task<Uri?> TryCreatePresignedUrlAsync(
        string storageKey, TimeSpan lifetime, string? downloadFileName, CancellationToken ct = default);
}

/// <summary>Result of a successful save: the key to persist plus basic size/type facts.</summary>
public sealed record StoredFile(string StorageKey, string FileName, long SizeBytes, string ContentType);
