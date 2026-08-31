namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

public class AdminTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement.GetProperty("data");

    private static MultipartFormDataContent ContributionForm(Guid moduleId, string title = "Ma contribution")
    {
        return new MultipartFormDataContent
        {
            { new ByteArrayContent(SamplePdf.OnePage()), "File", "c.pdf" },
            { new StringContent(title), "Title" },
            { new StringContent("TD"), "Type" },
            { new StringContent(moduleId.ToString()), "ModuleId" },
            { new StringContent("Un TD corrigé"), "Description" },
        };
    }

    [Fact]
    public async Task Student_contributes_admin_approves_document_created_and_student_notified()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();
        var (student, _) = await _factory.NewStudentClientAsync();

        var submit = await student.PostAsync("/api/contributions", ContributionForm(moduleId));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        var contributionId = Data(await submit.Content.ReadAsStringAsync()).GetProperty("id").GetGuid();

        Data(await student.GetStringAsync("/api/me/contributions"))
            .EnumerateArray().Single().GetProperty("status").GetString().Should().Be("Pending");

        var moderation = Data(await admin.GetStringAsync("/api/admin/contributions?status=Pending"));
        moderation.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().Contain(contributionId);

        var approved = Data(await (await admin.PostAsJsonAsync(
            $"/api/admin/contributions/{contributionId}/approve", new { note = "Merci", publishNow = true })).Content.ReadAsStringAsync());
        approved.GetProperty("status").GetString().Should().Be("Approved");
        var createdDocId = approved.GetProperty("createdDocumentId").GetGuid();

        // Document exists and is published.
        Data(await admin.GetStringAsync($"/api/documents/id/{createdDocId}"))
            .GetProperty("status").GetString().Should().Be("Published");

        // Student got a notification.
        var notifs = Data(await student.GetStringAsync("/api/me/notifications"));
        notifs.EnumerateArray().Select(n => n.GetProperty("type").GetString()).Should().Contain("ContributionApproved");

        var unread = Data(await student.GetStringAsync("/api/me/notifications/unread-count")).GetInt32();
        unread.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Reporting_a_document_then_resolving_it_writes_an_audit_entry()
    {
        var admin = await _factory.AdminClientAsync();
        var moduleId = await admin.CreateModuleAsync();

        var up = await admin.PostAsync("/api/documents", new MultipartFormDataContent
        {
            { new ByteArrayContent(SamplePdf.OnePage()), "File", "d.pdf" },
            { new StringContent($"Doc {Guid.NewGuid():N}"), "Title" },
            { new StringContent("Course"), "Type" },
            { new StringContent(moduleId.ToString()), "ModuleId" },
        });
        var doc = Data(await up.Content.ReadAsStringAsync()).GetProperty("document");
        var docId = doc.GetProperty("id").GetGuid();
        var slug = doc.GetProperty("slug").GetString()!;
        (await admin.PostAsJsonAsync($"/api/documents/{docId}/status", new { status = "Published" })).EnsureSuccessStatusCode();

        var (student, _) = await _factory.NewStudentClientAsync();
        var report = await student.PostAsJsonAsync($"/api/documents/{slug}/report", new { reason = "WrongYear", comment = "C'est 2024" });
        report.StatusCode.Should().Be(HttpStatusCode.OK);
        var reportId = Data(await report.Content.ReadAsStringAsync()).GetProperty("id").GetGuid();

        var open = Data(await admin.GetStringAsync("/api/admin/reports?status=Open"));
        open.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).Should().Contain(reportId);

        (await admin.PostAsJsonAsync($"/api/admin/reports/{reportId}/resolve", new { status = "Resolved", note = "Corrigé" }))
            .EnsureSuccessStatusCode();

        var audit = Data(await admin.GetStringAsync("/api/admin/audit?entityType=DocumentReport"));
        audit.EnumerateArray().Select(a => a.GetProperty("action").GetString()).Should().Contain("report.resolved");
    }

    [Fact]
    public async Task Admin_dashboard_returns_stats()
    {
        var admin = await _factory.AdminClientAsync();

        var dash = Data(await admin.GetStringAsync("/api/admin/dashboard"));

        dash.GetProperty("stats").GetProperty("totalUsers").GetInt32().Should().BeGreaterThan(0);
        dash.GetProperty("signupsLast30Days").GetArrayLength().Should().Be(31);
        dash.TryGetProperty("topModules", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Admin_can_search_users_and_change_roles()
    {
        var admin = await _factory.AdminClientAsync();
        var (student, email) = await _factory.NewStudentClientAsync();
        var studentId = Data(await student.GetStringAsync("/api/me")).GetProperty("id").GetGuid();

        var found = Data(await admin.GetStringAsync($"/api/admin/users?search={Uri.EscapeDataString(email)}"));
        found.EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).Should().Contain(studentId);

        var updated = Data(await (await admin.PutAsJsonAsync(
            $"/api/admin/users/{studentId}/roles", new { roles = new[] { "Student", "Moderator" } })).Content.ReadAsStringAsync());
        updated.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Contain("Moderator");

        var audit = Data(await admin.GetStringAsync("/api/admin/audit?action=user.roles_changed"));
        audit.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Students_cannot_reach_moderation_endpoints()
    {
        var (student, _) = await _factory.NewStudentClientAsync();

        (await student.GetAsync("/api/admin/contributions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.GetAsync("/api/admin/reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.GetAsync("/api/admin/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.GetAsync("/api/admin/dashboard")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
