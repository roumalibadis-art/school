namespace USTHBStudy.Application.Documents;

/// <summary>
/// Issues and verifies the short-lived, signed tokens used to broker downloads through the API when
/// the storage provider has no native presigned URLs (PRD §29). The token is the only credential the
/// <c>/api/files</c> endpoint needs.
/// </summary>
public interface IDownloadTokenService
{
    string Issue(DownloadGrant grant);

    DownloadGrant? Verify(string token);
}

public sealed record DownloadGrant(string StorageKey, Guid DocumentId, Guid? UserId, DateTime ExpiresAtUtc);

public sealed record DownloadTicket(string Url, DateTime ExpiresAtUtc, string FileName);
