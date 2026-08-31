namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

public class AcademicCrudTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AcademicCrudTests(ApiFactory factory) => _factory = factory;

    private static async Task<(HttpStatusCode Status, JsonElement Body)> PostJson(HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        var raw = await response.Content.ReadAsStringAsync();
        var body = string.IsNullOrWhiteSpace(raw) ? default : JsonDocument.Parse(raw).RootElement.Clone();
        return (response.StatusCode, body);
    }

    private static Guid DataId(JsonElement body) => body.GetProperty("data").GetProperty("id").GetGuid();

    private static string DataSlug(JsonElement body) => body.GetProperty("data").GetProperty("slug").GetString()!;

    [Fact]
    public async Task Academic_reads_are_public_but_writes_need_the_manage_permission()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/faculties")).StatusCode.Should().Be(HttpStatusCode.OK);

        var student = await _factory.StudentClientAsync();
        var (studentStatus, _) = await PostJson(student, "/api/universities",
            new { name = "Blocked University", isActive = true });
        studentStatus.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_build_a_full_hierarchy_down_to_a_module()
    {
        var admin = await _factory.AdminClientAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];

        var (_, uni) = await PostJson(admin, "/api/universities", new { name = $"University {tag}", code = "U", isActive = true });
        var universityId = DataId(uni);

        var (_, fac) = await PostJson(admin, "/api/faculties", new { name = $"Faculty {tag}", universityId, isActive = true });
        var facultyId = DataId(fac);

        var (_, dep) = await PostJson(admin, "/api/departments", new { name = $"Dept {tag}", facultyId, isActive = true });
        var departmentId = DataId(dep);

        var (_, spec) = await PostJson(admin, "/api/specialties", new { name = $"Specialty {tag}", departmentId, isActive = true });
        var specialtyId = DataId(spec);

        var (_, lvl) = await PostJson(admin, "/api/levels",
            new { name = $"Licence 1 {tag}", shortName = "L1", cycle = "Licence", order = 1, specialtyId, isActive = true });
        var levelId = DataId(lvl);

        var (_, sem) = await PostJson(admin, "/api/semesters",
            new { name = $"Semestre 1 {tag}", shortName = "S1", order = 1, levelId, isActive = true });
        var semesterId = DataId(sem);

        var (moduleStatus, mod) = await PostJson(admin, "/api/modules", new
        {
            name = $"Algorithmique {tag}",
            coefficient = 3.0,
            credits = 6,
            semesterId,
            specialtyId,
            isActive = true,
        });

        moduleStatus.Should().Be(HttpStatusCode.OK);
        DataSlug(mod).Should().StartWith("algorithmique");

        // Dependent-dropdown filter (PRD §34).
        var faculties = await admin.GetFromJsonAsync<JsonElement>($"/api/faculties?parentId={universityId}");
        faculties.GetProperty("data").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("id").GetGuid().Should().Be(facultyId);
    }

    [Fact]
    public async Task Creating_a_faculty_under_a_missing_university_returns_404()
    {
        var admin = await _factory.AdminClientAsync();

        var (status, _) = await PostJson(admin, "/api/faculties",
            new { name = "Orphan", universityId = Guid.NewGuid(), isActive = true });

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_names_get_distinct_slugs()
    {
        var admin = await _factory.AdminClientAsync();
        var (_, uni) = await PostJson(admin, "/api/universities", new { name = $"Dup Univ {Guid.NewGuid():N}", isActive = true });
        var universityId = DataId(uni);

        var (_, first) = await PostJson(admin, "/api/faculties", new { name = "Faculté d'Informatique", universityId, isActive = true });
        var (_, second) = await PostJson(admin, "/api/faculties", new { name = "Faculté d'Informatique", universityId, isActive = true });

        DataSlug(first).Should().Be("faculte-d-informatique");
        DataSlug(second).Should().Be("faculte-d-informatique-2");
    }

    [Fact]
    public async Task Update_changes_the_slug_when_the_name_changes_and_delete_is_soft()
    {
        var admin = await _factory.AdminClientAsync();
        var (_, uni) = await PostJson(admin, "/api/universities", new { name = $"Rename Me {Guid.NewGuid():N}", isActive = true });
        var id = DataId(uni);

        var put = await admin.PutAsJsonAsync($"/api/universities/{id}", new { name = "Renamed University", isActive = true });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = JsonDocument.Parse(await put.Content.ReadAsStringAsync()).RootElement;
        DataSlug(renamed).Should().Be("renamed-university");

        (await admin.DeleteAsync($"/api/universities/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/universities/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Module_validation_rejects_an_out_of_range_coefficient()
    {
        var admin = await _factory.AdminClientAsync();

        var (status, body) = await PostJson(admin, "/api/modules", new
        {
            name = "Bad Module",
            coefficient = 999,
            credits = 6,
            semesterId = Guid.NewGuid(),
            specialtyId = Guid.NewGuid(),
        });

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("errors").EnumerateArray().Should().NotBeEmpty();
    }
}
