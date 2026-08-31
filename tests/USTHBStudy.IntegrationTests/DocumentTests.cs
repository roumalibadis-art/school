namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

public class DocumentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DocumentTests(ApiFactory factory) => _factory = factory;

    private static MultipartFormDataContent UploadContent(byte[] file, string fileName, Guid moduleId, string type = "Exam", bool premium = false)
    {
        var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(file);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(filePart, "File", fileName);
        content.Add(new StringContent($"Doc {Guid.NewGuid():N}"), "Title");
        content.Add(new StringContent(type), "Type");
        content.Add(new StringContent(moduleId.ToString()), "ModuleId");
        content.Add(new StringContent(premium ? "true" : "false"), "IsPremium");
        return content;
    }

    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task Upload_renders_a_pdf_preview_and_starts_as_draft_then_publish_makes_it_public()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();

        var upload = await admin.PostAsync("/api/documents", UploadContent(SamplePdf.OnePage(), "algo-exam-2025.pdf", moduleId));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = Root(await upload.Content.ReadAsStringAsync()).GetProperty("data").GetProperty("document");
        created.GetProperty("status").GetString().Should().Be("Draft");
        created.GetProperty("pageCount").GetInt32().Should().Be(1);
        created.GetProperty("hasPreview").GetBoolean().Should().BeTrue();
        var slug = created.GetProperty("slug").GetString()!;

        // Not visible to the public while it is a draft.
        var anon = _factory.CreateClient();
        (await anon.GetAsync($"/api/documents/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var anonList = Root(await anon.GetStringAsync("/api/documents"));
        anonList.GetProperty("data").EnumerateArray().Select(d => d.GetProperty("slug").GetString())
            .Should().NotContain(slug);

        // Publish.
        var docId = created.GetProperty("id").GetGuid();
        var publish = await admin.PostAsJsonAsync($"/api/documents/{docId}/status", new { status = "Published" });
        publish.StatusCode.Should().Be(HttpStatusCode.OK);

        // Now public, and viewing it bumps the counter.
        (await anon.GetAsync($"/api/documents/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var viewed = Root(await anon.GetStringAsync($"/api/documents/{slug}")).GetProperty("data");
        viewed.GetProperty("viewCount").GetInt64().Should().BeGreaterThan(0);

        // Preview image is served.
        var preview = await anon.GetAsync($"/api/documents/{slug}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await preview.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task Upload_rejects_a_non_pdf_disguised_as_pdf()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();

        var fakePdf = Encoding.ASCII.GetBytes("MZ this is actually an executable");
        var response = await admin.PostAsync("/api/documents", UploadContent(fakePdf, "sneaky.pdf", moduleId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Uploading_the_same_file_twice_warns_about_the_duplicate()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();
        var bytes = SamplePdf.OnePage("duplicate check");

        (await admin.PostAsync("/api/documents", UploadContent(bytes, "first.pdf", moduleId))).EnsureSuccessStatusCode();
        var second = await admin.PostAsync("/api/documents", UploadContent(bytes, "second.pdf", moduleId));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = Root(await second.Content.ReadAsStringAsync());
        body.GetProperty("data").GetProperty("possibleDuplicate").ValueKind.Should().NotBe(JsonValueKind.Null);
        body.GetProperty("message").GetString().Should().Contain("already exists");
    }

    [Fact]
    public async Task Students_cannot_upload_documents()
    {
        var student = await _factory.StudentClientAsync();
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();

        var response = await student.PostAsync("/api/documents", UploadContent(SamplePdf.OnePage(), "x.pdf", moduleId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Exam_and_solution_can_be_linked()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();

        var examId = Root(await (await admin.PostAsync("/api/documents",
            UploadContent(SamplePdf.OnePage("exam"), "exam.pdf", moduleId, "Exam"))).Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("document").GetProperty("id").GetGuid();
        var solutionId = Root(await (await admin.PostAsync("/api/documents",
            UploadContent(SamplePdf.OnePage("solution"), "solution.pdf", moduleId, "ExamSolution"))).Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("document").GetProperty("id").GetGuid();

        var link = await admin.PostAsync($"/api/documents/{examId}/solutions/{solutionId}", null);
        link.StatusCode.Should().Be(HttpStatusCode.OK);

        var exam = Root(await admin.GetStringAsync($"/api/documents/id/{examId}")).GetProperty("data");
        exam.GetProperty("solutionDocumentIds").EnumerateArray().Select(e => e.GetGuid()).Should().Contain(solutionId);
    }
}
