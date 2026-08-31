namespace USTHBStudy.Infrastructure.Documents;

using PDFtoImage;
using SkiaSharp;
using USTHBStudy.Application.Abstractions;

/// <summary>
/// Renders PDF previews with pdfium (via PDFtoImage). Native binaries ship with the package for
/// Windows/Linux/macOS, so no external install is required (PRD §31).
/// </summary>
public sealed class PdfiumPdfProcessor : IPdfProcessor
{
    private const int PreviewWidthPx = 1240;
    private const int ThumbnailWidthPx = 320;

    public bool CanProcess(string mimeType) =>
        string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<PdfRenderResult> RenderAsync(Stream pdf, CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await pdf.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        var pageCount = Conversion.GetPageCount(bytes);

        var preview = Render(bytes, PreviewWidthPx);
        var thumbnail = Render(bytes, ThumbnailWidthPx);

        return new PdfRenderResult(pageCount, preview, thumbnail);
    }

    private static byte[] Render(byte[] pdf, int width)
    {
        using var bitmap = Conversion.ToImage(pdf, options: new RenderOptions
        {
            Width = width,
            WithAspectRatio = true,
        });

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
