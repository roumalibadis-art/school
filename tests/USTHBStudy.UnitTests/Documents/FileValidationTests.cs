namespace USTHBStudy.UnitTests.Documents;

using System.Text;
using FluentAssertions;
using USTHBStudy.Application.Documents;

public class FileValidationTests
{
    private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ");
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 };
    private static readonly byte[] ExeHeader = { 0x4D, 0x5A, 0x90, 0x00 };

    [Fact]
    public void Accepts_a_pdf_with_a_pdf_extension()
    {
        var result = FileValidation.Inspect("exam-2025.pdf", PdfHeader);

        result.IsValid.Should().BeTrue();
        result.MimeType.Should().Be("application/pdf");
    }

    [Theory]
    [InlineData("cover.png")]
    [InlineData("cover.PNG")]
    public void Accepts_a_png(string name)
    {
        FileValidation.Inspect(name, PngHeader).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepts_a_jpeg_under_either_extension()
    {
        FileValidation.Inspect("scan.jpg", JpegHeader).IsValid.Should().BeTrue();
        FileValidation.Inspect("scan.jpeg", JpegHeader).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_disallowed_extension()
    {
        var result = FileValidation.Inspect("malware.exe", ExeHeader);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public void Rejects_an_executable_renamed_to_pdf()
    {
        // PRD §36: do not trust the extension — sniff the content.
        var result = FileValidation.Inspect("totally-a.pdf", ExeHeader);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not a recognised");
    }

    [Fact]
    public void Rejects_a_png_body_with_a_pdf_extension()
    {
        var result = FileValidation.Inspect("mismatch.pdf", PngHeader);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("does not match");
    }
}
