namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

public class SearchTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SearchTests(ApiFactory factory) => _factory = factory;

    private static MultipartFormDataContent Upload(byte[] file, string title, Guid moduleId, string type, Guid? yearId)
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "File", "d.pdf" },
            { new StringContent(title), "Title" },
            { new StringContent(type), "Type" },
            { new StringContent(moduleId.ToString()), "ModuleId" },
        };
        if (yearId is { } y)
        {
            content.Add(new StringContent(y.ToString()), "AcademicYearId");
        }

        return content;
    }

    private static async Task<(Guid Id, string Slug)> UploadAndPublish(
        HttpClient admin, byte[] file, string title, Guid moduleId, string type, Guid? yearId)
    {
        var up = await admin.PostAsync("/api/documents", Upload(file, title, moduleId, type, yearId));
        up.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await up.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("document");
        var id = doc.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/documents/{id}/status", new { status = "Published" })).EnsureSuccessStatusCode();
        return (id, doc.GetProperty("slug").GetString()!);
    }

    [Fact]
    public async Task Search_matches_free_text_year_and_type_and_hides_drafts()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();

        var year = await admin.PostAsJsonAsync("/api/academic-years", new { startYear = 2025 });
        var yearId = JsonDocument.Parse(await year.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var pdf = SamplePdf.OnePage();
        await UploadAndPublish(admin, pdf, "Examen Algorithmique 2025", moduleId, "Exam", yearId);
        await UploadAndPublish(admin, SamplePdf.OnePage("course"), "Cours Bases de données", moduleId, "Course", null);

        // draft (not published) — must not surface
        var draft = await admin.PostAsync("/api/documents", Upload(SamplePdf.OnePage("draft"), "Examen secret", moduleId, "Exam", null));
        draft.EnsureSuccessStatusCode();

        var anon = _factory.CreateClient();

        var byText = await Search(anon, "?q=algorithmique");
        byText.Select(h => h.GetProperty("title").GetString()).Should().ContainSingle().Which.Should().Contain("Algorithmique");

        var byYear = await Search(anon, "?q=2025");
        byYear.Should().NotBeEmpty();
        byYear.Should().OnlyContain(h => h.GetProperty("year").GetInt32() == 2025);

        var byType = await Search(anon, $"?type=Course&moduleId={moduleId}");
        byType.Select(h => h.GetProperty("type").GetString()).Should().OnlyContain(t => t == "Course");

        var noHits = await Search(anon, "?q=zzzznotarealterm");
        noHits.Should().BeEmpty();

        var secret = await Search(anon, "?q=secret");
        secret.Should().BeEmpty();
    }

    private static async Task<List<JsonElement>> Search(HttpClient client, string queryString)
    {
        var json = await client.GetStringAsync("/api/search" + queryString);
        return JsonDocument.Parse(json).RootElement.GetProperty("data").EnumerateArray().ToList();
    }
}

public class AcademicCacheTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AcademicCacheTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Faculty_list_reflects_writes_immediately_despite_caching()
    {
        var admin = await _factory.AdminClientAsync();
        var uni = JsonDocument.Parse(await (await admin.PostAsJsonAsync("/api/universities",
            new { name = $"CacheU {Guid.NewGuid():N}", isActive = true })).Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        async Task<int> FacultyCount() =>
            JsonDocument.Parse(await admin.GetStringAsync($"/api/faculties?parentId={uni}"))
                .RootElement.GetProperty("data").GetArrayLength();

        (await FacultyCount()).Should().Be(0); // primes the cache

        (await admin.PostAsJsonAsync("/api/faculties", new { name = "Cached Faculty", universityId = uni, isActive = true }))
            .EnsureSuccessStatusCode();

        (await FacultyCount()).Should().Be(1); // cache was invalidated on write
    }
}
