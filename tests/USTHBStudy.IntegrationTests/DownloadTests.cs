namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using USTHBStudy.IntegrationTests.TestSupport;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>The download-authorization cases from PRD §29 and §61.</summary>
public class DownloadTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DownloadTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement.GetProperty("data");

    private static MultipartFormDataContent Upload(Guid moduleId, bool premium) => new()
    {
        { new ByteArrayContent(SamplePdf.OnePage()), "File", "d.pdf" },
        { new StringContent($"Doc {Guid.NewGuid():N}"), "Title" },
        { new StringContent("Exam"), "Type" },
        { new StringContent(moduleId.ToString()), "ModuleId" },
        { new StringContent(premium ? "true" : "false"), "IsPremium" },
    };

    private async Task<(Guid Id, string Slug)> PublishDoc(HttpClient admin, Guid moduleId, bool premium)
    {
        var up = await admin.PostAsync("/api/documents", Upload(moduleId, premium));
        up.EnsureSuccessStatusCode();
        var doc = Data(await up.Content.ReadAsStringAsync()).GetProperty("document");
        var id = doc.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/documents/{id}/status", new { status = "Published" })).EnsureSuccessStatusCode();
        return (id, doc.GetProperty("slug").GetString()!);
    }

    private static async Task<Guid> UserId(HttpClient client) =>
        Data(await client.GetStringAsync("/api/me")).GetProperty("id").GetGuid();

    [Fact]
    public async Task Anonymous_download_request_is_401()
    {
        var admin = await _factory.AdminClientAsync();
        var (_, slug) = await PublishDoc(admin, await admin.CreateModuleAsync(), premium: false);

        var response = await _factory.CreateClient().GetAsync($"/api/documents/{slug}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signed_in_student_downloads_a_free_document_and_the_link_serves_the_file()
    {
        var admin = await _factory.AdminClientAsync();
        var (docId, slug) = await PublishDoc(admin, await admin.CreateModuleAsync(), premium: false);
        var (student, _) = await _factory.NewStudentClientAsync();

        var ticket = Data(await student.GetStringAsync($"/api/documents/{slug}/download"));
        var url = ticket.GetProperty("url").GetString()!;
        url.Should().StartWith("/api/files?t=");

        var file = await _factory.CreateClient().GetAsync(url);
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync()).Should().StartWith("%PDF-"u8.ToArray());

        var detail = Data(await admin.GetStringAsync($"/api/documents/id/{docId}"));
        detail.GetProperty("downloadCount").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Free_user_requesting_a_premium_document_gets_403()
    {
        // PRD §61-#1
        var admin = await _factory.AdminClientAsync();
        var (_, slug) = await PublishDoc(admin, await admin.CreateModuleAsync(), premium: true);
        var (student, _) = await _factory.NewStudentClientAsync();

        var response = await student.GetAsync($"/api/documents/{slug}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Premium_user_can_download_a_premium_document()
    {
        var admin = await _factory.AdminClientAsync();
        var (_, slug) = await PublishDoc(admin, await admin.CreateModuleAsync(), premium: true);
        var (student, _) = await _factory.NewStudentClientAsync();

        (await admin.PostAsJsonAsync($"/api/admin/users/{await UserId(student)}/premium", new { months = 3 }))
            .EnsureSuccessStatusCode();

        var response = await student.GetAsync($"/api/documents/{slug}/download");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Expired_premium_user_cannot_download_a_premium_document()
    {
        // PRD §61-#5: IsPremium flag is true but the expiry has passed → treated as free.
        var admin = await _factory.AdminClientAsync();
        var (_, slug) = await PublishDoc(admin, await admin.CreateModuleAsync(), premium: true);
        var (student, _) = await _factory.NewStudentClientAsync();
        var studentId = await UserId(student);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(u => u.Id == studentId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.IsPremium, true)
                    .SetProperty(u => u.PremiumExpiresAt, DateTime.UtcNow.AddDays(-1)));
        }

        var response = await student.GetAsync($"/api/documents/{slug}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Suspended_user_cannot_download()
    {
        // PRD §61-#4
        var admin = await _factory.AdminClientAsync();
        var (_, slug) = await PublishDoc(admin, await admin.CreateModuleAsync(), premium: false);
        var (student, _) = await _factory.NewStudentClientAsync();

        (await admin.PostAsJsonAsync($"/api/admin/users/{await UserId(student)}/suspend", new { }))
            .EnsureSuccessStatusCode();

        var response = await student.GetAsync($"/api/documents/{slug}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_invalid_download_token_is_rejected()
    {
        var response = await _factory.CreateClient().GetAsync("/api/files?t=not-a-real-token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
