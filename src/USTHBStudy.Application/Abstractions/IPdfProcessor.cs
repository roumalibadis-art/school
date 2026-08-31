namespace USTHBStudy.Application.Abstractions;

/// <summary>Renders a preview image + thumbnail and reads the page count from a PDF (PRD §31).</summary>
public interface IPdfProcessor
{
    bool CanProcess(string mimeType);

    Task<PdfRenderResult> RenderAsync(Stream pdf, CancellationToken ct = default);
}

/// <summary>PNG bytes for the first-page preview and thumbnail, plus the document's page count.</summary>
public sealed record PdfRenderResult(int PageCount, byte[] PreviewPng, byte[] ThumbnailPng);
