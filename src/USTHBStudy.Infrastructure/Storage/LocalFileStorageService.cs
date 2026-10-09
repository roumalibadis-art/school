using Microsoft.Extensions.Options;
using USTHBStudy.Application.Abstractions;

namespace USTHBStudy.Infrastructure.Storage;

/// <summary>
/// Filesystem-backed storage for development. Every resolved path is verified to stay inside the
/// configured root so a hostile storage key can never escape it. Keys are
/// <c>{prefix}/{guid}{extension}</c>; the original file name is never used on disk.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;

    public LocalFileStorageService(IOptions<FileStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.LocalRootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(
        Stream content, string keyPrefix, string fileName, string contentType, CancellationToken ct = default)
    {
        var prefix = string.Join('/', keyPrefix.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p != "." && p != ".."));
        var extension = Path.GetExtension(fileName);
        var key = $"{prefix}/{Guid.NewGuid():N}{extension}".TrimStart('/');
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, ct);
        return new StoredFile(key, fileName, target.Length, contentType);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Stored file not found.", storageKey);
        }

        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(Resolve(storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<Uri?> TryCreatePresignedUrlAsync(
        string storageKey, TimeSpan lifetime, string? downloadFileName, CancellationToken ct = default) =>
        Task.FromResult<Uri?>(null);

    private string Resolve(string storageKey)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        return full;
    }
}
