using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using USTHBStudy.Application.Abstractions;

namespace USTHBStudy.Infrastructure.Storage;

/// <summary>S3-compatible storage (AWS or MinIO). Downloads use short-lived presigned GET URLs (PRD §29).</summary>
public sealed class S3FileStorageService : IFileStorageService, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3FileStorageService(IOptions<FileStorageOptions> options)
    {
        var s3 = options.Value.S3;
        _bucket = s3.Bucket;
        var config = new AmazonS3Config { ForcePathStyle = s3.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(s3.ServiceUrl))
        {
            config.ServiceURL = s3.ServiceUrl;
        }
        else if (!string.IsNullOrWhiteSpace(s3.Region))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3.Region);
        }

        _client = new AmazonS3Client(s3.AccessKey, s3.SecretKey, config);
    }

    public async Task<StoredFile> SaveAsync(
        Stream content, string keyPrefix, string fileName, string contentType, CancellationToken ct = default)
    {
        var key = $"{keyPrefix.Trim('/')}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        await _client.PutObjectAsync(
            new PutObjectRequest { BucketName = _bucket, Key = key, InputStream = buffer, ContentType = contentType }, ct);
        return new StoredFile(key, fileName, buffer.Length, contentType);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(_bucket, storageKey, ct);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, storageKey, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default) =>
        _client.DeleteObjectAsync(_bucket, storageKey, ct);

    public Task<Uri?> TryCreatePresignedUrlAsync(
        string storageKey, TimeSpan lifetime, string? downloadFileName, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = storageKey,
            Expires = DateTime.UtcNow.Add(lifetime),
            Verb = HttpVerb.GET,
        };
        if (!string.IsNullOrWhiteSpace(downloadFileName))
        {
            request.ResponseHeaderOverrides.ContentDisposition =
                $"attachment; filename=\"{downloadFileName.Replace("\"", string.Empty)}\"";
        }

        return Task.FromResult<Uri?>(new Uri(_client.GetPreSignedURL(request)));
    }

    public void Dispose() => _client.Dispose();
}
