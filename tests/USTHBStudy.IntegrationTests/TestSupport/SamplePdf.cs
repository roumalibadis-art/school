namespace USTHBStudy.IntegrationTests.TestSupport;

using System.Text;

/// <summary>Builds a minimal but well-formed single-page PDF (valid xref) that pdfium can render.</summary>
public static class SamplePdf
{
    public static byte[] OnePage(string text = "USTHB sample document")
    {
        var content = $"BT /F1 24 Tf 72 700 Td ({Escape(text)}) Tj ET";

        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R "
                + "/Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");

        var offsets = new int[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = sb.Length;
            sb.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xrefOffset = sb.Length;
        sb.Append("xref\n0 ").Append(objects.Length + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        sb.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\n");
        sb.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string Escape(string value) => value.Replace("(", "\\(").Replace(")", "\\)");
}
