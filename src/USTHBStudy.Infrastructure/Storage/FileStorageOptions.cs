namespace USTHBStudy.Infrastructure.Storage;

/// <summary>Binds the <c>Storage</c> configuration section (PRD §7).</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary><c>Local</c> (default) or <c>S3</c>.</summary>
    public string Provider { get; set; } = "Local";

    public string LocalRootPath { get; set; } = "./_storage";

    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class S3StorageOptions
{
    public string? ServiceUrl { get; set; }
    public string? Region { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; } = true;
}
