namespace USTHBStudy.Application.Documents;

/// <summary>
/// Server-side upload checks (PRD §36): extension allow-list plus a magic-byte sniff. The client's
/// declared content type is never trusted.
/// </summary>
public static class FileValidation
{
    /// <summary>Allowed extension → canonical MIME type.</summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
        };

    public static FileInspection Inspect(string fileName, ReadOnlySpan<byte> header)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !AllowedTypes.TryGetValue(extension, out var expectedMime))
        {
            return FileInspection.Invalid(
                $"File type '{extension}' is not allowed. Accepted: {string.Join(", ", AllowedTypes.Keys)}.");
        }

        var sniffed = Sniff(header);
        if (sniffed is null)
        {
            return FileInspection.Invalid("The file content is not a recognised PDF or image.");
        }

        if (!string.Equals(sniffed, expectedMime, StringComparison.Ordinal))
        {
            return FileInspection.Invalid(
                $"File content ({sniffed}) does not match its extension ({extension}).");
        }

        return FileInspection.Valid(sniffed);
    }

    private static string? Sniff(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 5 && header[..5].SequenceEqual("%PDF-"u8))
        {
            return "application/pdf";
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return "image/png";
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return null;
    }
}

public readonly record struct FileInspection(bool IsValid, string MimeType, string? Error)
{
    public static FileInspection Valid(string mime) => new(true, mime, null);

    public static FileInspection Invalid(string error) => new(false, string.Empty, error);
}
