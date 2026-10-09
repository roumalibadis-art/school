namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.Application.Search;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>University is optional and invisible to search; the academic hierarchy and permissions are untouched.</summary>
public class UniversityAndHierarchyTests
{
    private static JsonElement Data(string json) => ClassificationHarness.Data(json);

    [Fact]
    public async Task A_student_can_save_a_profile_without_any_university()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();

        var save = await voter.Client.PutAsJsonAsync("/api/me", new
        {
            firstName = "Nour",
            lastName = "Sans-Univ",
            facultyId = h.Tree.FacultyId,
            departmentId = h.Tree.DepartmentId,
            specialtyId = h.Tree.SpecialtyId,
            levelId = h.Tree.LevelId,
        });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = Data(await voter.Client.GetStringAsync("/api/me"));
        profile.GetProperty("university").ValueKind.Should().Be(JsonValueKind.Null);
        profile.GetProperty("faculty").GetProperty("id").GetGuid().Should().Be(h.Tree.FacultyId);

        // …and the dashboard still works from specialty/level alone.
        Data(await voter.Client.GetStringAsync("/api/me/dashboard")).GetProperty("myModules").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Existing_university_links_and_data_are_preserved_when_present()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        (await voter.Client.PutAsJsonAsync("/api/me", new
        {
            firstName = "A", lastName = "B", universityId = h.Tree.UniversityId, facultyId = h.Tree.FacultyId,
        })).EnsureSuccessStatusCode();

        Data(await voter.Client.GetStringAsync("/api/me")).GetProperty("university").GetProperty("id").GetGuid()
            .Should().Be(h.Tree.UniversityId);
        (await h.Admin.GetStringAsync("/api/universities")).Should().Contain(h.Tree.UniversityId.ToString());
    }

    [Fact]
    public void Search_has_no_university_input_or_output()
    {
        typeof(SearchQuery).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("University"));
        typeof(SearchHit).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("University"));
    }

    [Fact]
    public async Task A_university_parameter_does_not_change_search_results()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var (_, slug) = await h.PublishDocumentAsync();
        var title = Data(await h.Admin.GetStringAsync($"/api/documents/{slug}")).GetProperty("title").GetString()!;

        var plain = Data(await h.Admin.GetStringAsync($"/api/search?q={Uri.EscapeDataString(title)}"));
        var withUniversity = Data(await h.Admin.GetStringAsync(
            $"/api/search?q={Uri.EscapeDataString(title)}&universityId={Guid.NewGuid()}&university=anything"));

        plain.GetArrayLength().Should().Be(1);
        withUniversity.GetRawText().Should().Be(plain.GetRawText());
        plain.GetRawText().ToLowerInvariant().Should().NotContain("universit");
    }

    [Fact]
    public async Task Classification_never_depends_on_university_for_eligibility_or_options()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        await h.UploadUnclassifiedAsync();
        var voter = await h.NewVoterAsync(); // no university, no faculty, no profile at all

        (await voter.TaskAsync()).Should().HaveCount(1);
        var options = Data(await voter.Client.GetStringAsync("/api/classification/options"));
        options.GetRawText().ToLowerInvariant().Should().NotContain("universit");
    }

    [Fact]
    public async Task The_academic_hierarchy_is_unchanged_a_faculty_still_belongs_to_a_university()
    {
        await using var h = await ClassificationHarness.CreateAsync();

        var orphan = await h.Admin.PostAsJsonAsync("/api/faculties", new { name = "Orphan", isActive = true });
        orphan.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var faculties = Data(await h.Admin.GetStringAsync($"/api/faculties?parentId={h.Tree.UniversityId}"));
        faculties.EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).Should().Contain(h.Tree.FacultyId);
        var specialties = Data(await h.Admin.GetStringAsync($"/api/specialties?parentId={h.Tree.DepartmentId}"));
        specialties.EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).Should().Contain(h.Tree.SpecialtyId);
    }

    [Fact]
    public async Task Published_module_classified_documents_are_still_found_by_specialty_and_faculty_filters()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var (_, slug) = await h.PublishDocumentAsync();
        var title = Data(await h.Admin.GetStringAsync($"/api/documents/{slug}")).GetProperty("title").GetString()!;

        Data(await h.Admin.GetStringAsync($"/api/search?q={Uri.EscapeDataString(title)}&specialtyId={h.Tree.SpecialtyId}")).GetArrayLength().Should().Be(1);
        Data(await h.Admin.GetStringAsync($"/api/search?q={Uri.EscapeDataString(title)}&facultyId={h.Tree.FacultyId}")).GetArrayLength().Should().Be(1);
        Data(await h.Admin.GetStringAsync($"/api/search?q={Uri.EscapeDataString(title)}&specialtyId={Guid.NewGuid()}")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_verified_unclassified_document_is_searchable_by_its_community_specialty_once_published()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync($"Community {Guid.NewGuid():N}");
        var spec = await h.CreateSpecialtyAsync("Community Spec");
        foreach (var v in await h.NewVotersAsync(3))
        {
            var a = (await v.TaskAsync()).Single().AssignmentId;
            (await v.VoteAsync(a, new { decision = "Classify", specialtyId = spec, documentType = "Exam" })).EnsureSuccessStatusCode();
        }

        // Verified but still needs a module before it can be published (the existing publishing rule).
        (await h.Admin.PostAsJsonAsync($"/api/documents/{docId}/status", new { status = "Published" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        var doc = await h.GetDocumentAsync(docId);
        (await h.Admin.PutAsJsonAsync($"/api/documents/{docId}", new
        {
            title = doc.GetProperty("title").GetString(),
            type = "Exam",
            moduleId = h.Tree.ModuleId,
            isPremium = false,
        })).EnsureSuccessStatusCode();
        (await h.Admin.PostAsJsonAsync($"/api/documents/{docId}/status", new { status = "Published" })).EnsureSuccessStatusCode();

        var hits = Data(await h.Admin.GetStringAsync($"/api/search?q={Uri.EscapeDataString(doc.GetProperty("title").GetString()!)}"));
        hits.GetArrayLength().Should().Be(1);
    }

    // ------------------------------------------------------------------ roles and permissions intact

    [Fact]
    public async Task Role_permissions_are_unchanged_for_existing_permissions_and_extended_only_for_staff()
    {
        await using var h = await ClassificationHarness.CreateAsync();

        var student = Data(await (await h.Factory.StudentClientAsync()).GetStringAsync("/api/me"));
        student.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("Student");
        student.GetProperty("permissions").EnumerateArray().Select(r => r.GetString()).Should().Equal("Document.View");

        var moderatorClient = await h.Factory.LoggedInClientAsync("moderator@example.local", "Moderator#2026!");
        var mod = Data(await moderatorClient.GetStringAsync("/api/me")).GetProperty("permissions").EnumerateArray()
            .Select(r => r.GetString()).ToList();
        mod.Should().Contain(new[] { "Document.Publish", "Contribution.Moderate", "Classification.Review", "Taxonomy.Review" });
        mod.Should().NotContain(new[] { "Classification.Settings", "Subscription.Manage", "AcademicData.Manage", "User.Suspend" });

        var admin = Data(await h.Admin.GetStringAsync("/api/me")).GetProperty("permissions").EnumerateArray()
            .Select(r => r.GetString()).ToList();
        admin.Should().Contain(new[] { "Classification.Settings", "Classification.Review", "Taxonomy.Review", "Subscription.Manage" });
    }

    [Fact]
    public async Task Students_remain_locked_out_of_every_existing_admin_surface()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var student = await h.NewVoterAsync();
        foreach (var path in new[]
                 {
                     "/api/admin/users", "/api/admin/audit", "/api/admin/contributions", "/api/admin/reports",
                     "/api/admin/dashboard", "/api/admin/classification/documents", "/api/admin/taxonomy/proposals",
                 })
        {
            (await student.Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden, path);
        }

        (await student.Client.PostAsJsonAsync("/api/universities", new { name = "Hack U" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await h.Factory.CreateClient().GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
