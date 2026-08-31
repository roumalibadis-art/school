namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

public class StudentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StudentTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement.GetProperty("data");

    [Fact]
    public async Task Profile_starts_empty_then_accepts_an_academic_profile_and_powers_the_dashboard()
    {
        var admin = await _factory.AdminClientAsync();
        var tree = await admin.CreateAcademicTreeAsync();
        var (student, email) = await _factory.NewStudentClientAsync();

        var initial = Data(await student.GetStringAsync("/api/me"));
        initial.GetProperty("email").GetString().Should().Be(email);
        initial.GetProperty("specialty").ValueKind.Should().Be(JsonValueKind.Null);

        var update = await student.PutAsJsonAsync("/api/me", new
        {
            firstName = "Sami",
            lastName = "Étudiant",
            universityId = tree.UniversityId,
            facultyId = tree.FacultyId,
            departmentId = tree.DepartmentId,
            specialtyId = tree.SpecialtyId,
            levelId = tree.LevelId,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = Data(await student.GetStringAsync("/api/me"));
        profile.GetProperty("specialty").GetProperty("id").GetGuid().Should().Be(tree.SpecialtyId);
        profile.GetProperty("level").GetProperty("id").GetGuid().Should().Be(tree.LevelId);

        var dashboard = Data(await student.GetStringAsync("/api/me/dashboard"));
        dashboard.GetProperty("firstName").GetString().Should().Be("Sami");
        dashboard.GetProperty("myModules").EnumerateArray().Select(m => m.GetProperty("id").GetGuid())
            .Should().Contain(tree.ModuleId);
        dashboard.GetProperty("subscription").GetProperty("state").GetString().Should().Be("none");
    }

    [Fact]
    public async Task Update_profile_rejects_a_specialty_that_does_not_exist()
    {
        var (student, _) = await _factory.NewStudentClientAsync();

        var response = await student.PutAsJsonAsync("/api/me", new
        {
            firstName = "X",
            lastName = "Y",
            specialtyId = Guid.NewGuid(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Favorites_are_idempotent_and_removable()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();
        var (student, _) = await _factory.NewStudentClientAsync();

        (await student.PostAsJsonAsync("/api/favorites", new { kind = "Module", entityId = moduleId })).EnsureSuccessStatusCode();
        (await student.PostAsJsonAsync("/api/favorites", new { kind = "Module", entityId = moduleId })).EnsureSuccessStatusCode();

        var list = Data(await student.GetStringAsync("/api/favorites"));
        list.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("entityId").GetGuid().Should().Be(moduleId);

        var remove = await student.DeleteAsync($"/api/favorites?kind=Module&entityId={moduleId}");
        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        Data(await student.GetStringAsync("/api/favorites")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Favoriting_a_missing_document_returns_404()
    {
        var (student, _) = await _factory.NewStudentClientAsync();

        var response = await student.PostAsJsonAsync("/api/favorites", new { kind = "Document", entityId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewing_a_published_document_shows_up_in_history()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();

        var upload = await admin.PostAsync("/api/documents", MakeUpload(moduleId));
        upload.EnsureSuccessStatusCode();
        var doc = Data(await upload.Content.ReadAsStringAsync()).GetProperty("document");
        var docId = doc.GetProperty("id").GetGuid();
        var slug = doc.GetProperty("slug").GetString()!;
        (await admin.PostAsJsonAsync($"/api/documents/{docId}/status", new { status = "Published" })).EnsureSuccessStatusCode();

        var (student, _) = await _factory.NewStudentClientAsync();
        (await student.GetAsync($"/api/documents/{slug}")).EnsureSuccessStatusCode();

        var history = Data(await student.GetStringAsync("/api/me/history"));
        history.GetProperty("recentDocuments").EnumerateArray().Select(e => e.GetProperty("entityId").GetGuid())
            .Should().Contain(docId);
    }

    private static MultipartFormDataContent MakeUpload(Guid moduleId) => new()
    {
        { new ByteArrayContent(SamplePdf.OnePage()), "File", "d.pdf" },
        { new StringContent($"Doc {Guid.NewGuid():N}"), "Title" },
        { new StringContent("Course"), "Type" },
        { new StringContent(moduleId.ToString()), "ModuleId" },
    };
}
