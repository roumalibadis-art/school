namespace USTHBStudy.IntegrationTests.TestSupport;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>
/// An isolated API instance (own in-memory database) for classification tests. The queue is global state, so
/// sharing one database between tests would let one test's documents leak into another's tasks.
/// </summary>
public sealed class ClassificationHarness : IAsyncDisposable
{
    private ClassificationHarness(WebApplicationFactory<Program> factory, HttpClient admin, FactoryExtensions.AcademicTree tree)
    {
        Factory = factory;
        Admin = admin;
        Tree = tree;
    }

    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient Admin { get; }

    public FactoryExtensions.AcademicTree Tree { get; }

    public static async Task<ClassificationHarness> CreateAsync(
        Action<Dictionary<string, object?>>? settings = null, WebApplicationFactory<Program>? factory = null)
    {
        factory ??= new ApiFactory();
        if (factory is IAsyncLifetime lifetime)
        {
            await lifetime.InitializeAsync();
        }

        var admin = await factory.AdminClientAsync();
        var tree = await admin.CreateAcademicTreeAsync();
        var harness = new ClassificationHarness(factory, admin, tree);

        // The default 3-second "look before you answer" gate would only slow the suite down.
        await harness.UpdateSettingsAsync(s =>
        {
            s["minSecondsBeforeVote"] = 0;
            settings?.Invoke(s);
        });
        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        Admin.Dispose();
        await ((IAsyncLifetime)Factory).DisposeAsync();
    }

    public static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement.GetProperty("data");

    public static async Task<JsonElement> DataAsync(HttpResponseMessage response) =>
        Data(await response.Content.ReadAsStringAsync());

    // ---------------------------------------------------------------- settings

    public async Task<Dictionary<string, object?>> GetSettingsAsync()
    {
        var json = await Admin.GetStringAsync("/api/admin/classification/settings");
        var element = Data(json);
        var dict = new Dictionary<string, object?>();
        foreach (var p in element.EnumerateObject())
        {
            if (p.Name == "updatedAt")
            {
                continue;
            }

            dict[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.Number => p.Value.GetInt32(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => p.Value.EnumerateArray().Select(e => e.GetString()).ToArray(),
                _ => p.Value.GetString(),
            };
        }

        return dict;
    }

    public async Task<HttpResponseMessage> TryUpdateSettingsAsync(Action<Dictionary<string, object?>> change)
    {
        var settings = await GetSettingsAsync();
        change(settings);
        return await Admin.PutAsJsonAsync("/api/admin/classification/settings", settings);
    }

    public async Task UpdateSettingsAsync(Action<Dictionary<string, object?>> change)
    {
        var response = await TryUpdateSettingsAsync(change);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Settings update failed: " + await response.Content.ReadAsStringAsync());
        }
    }

    // ---------------------------------------------------------------- documents

    /// <summary>Uploads a document with no module → it is unclassified and enters the community queue.</summary>
    public async Task<Guid> UploadUnclassifiedAsync(string? title = null, bool premium = false)
    {
        var response = await Admin.PostAsync("/api/documents", new MultipartFormDataContent
        {
            { new ByteArrayContent(SamplePdf.OnePage(title ?? "unclassified")), "File", "u.pdf" },
            { new StringContent(title ?? $"Unclassified {Guid.NewGuid():N}"), "Title" },
            { new StringContent("Other"), "Type" },
            { new StringContent(premium ? "true" : "false"), "IsPremium" },
        });
        response.EnsureSuccessStatusCode();
        return (await DataAsync(response)).GetProperty("document").GetProperty("id").GetGuid();
    }

    public async Task<List<Guid>> UploadUnclassifiedAsync(int count)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            ids.Add(await UploadUnclassifiedAsync());
        }

        return ids;
    }

    /// <summary>A classified, published document (the legacy path) — used by download tests.</summary>
    public async Task<(Guid Id, string Slug)> PublishDocumentAsync(bool premium = false)
    {
        var up = await Admin.PostAsync("/api/documents", new MultipartFormDataContent
        {
            { new ByteArrayContent(SamplePdf.OnePage()), "File", "d.pdf" },
            { new StringContent($"Doc {Guid.NewGuid():N}"), "Title" },
            { new StringContent("Exam"), "Type" },
            { new StringContent(Tree.ModuleId.ToString()), "ModuleId" },
            { new StringContent(premium ? "true" : "false"), "IsPremium" },
        });
        up.EnsureSuccessStatusCode();
        var doc = (await DataAsync(up)).GetProperty("document");
        var id = doc.GetProperty("id").GetGuid();
        (await Admin.PostAsJsonAsync($"/api/documents/{id}/status", new { status = "Published" })).EnsureSuccessStatusCode();
        return (id, doc.GetProperty("slug").GetString()!);
    }

    public async Task<JsonElement> GetDocumentAsync(Guid id) =>
        Data(await Admin.GetStringAsync($"/api/documents/id/{id}"));

    public async Task<JsonElement> GetClassificationDetailAsync(Guid id) =>
        Data(await Admin.GetStringAsync($"/api/admin/classification/documents/{id}"));

    // ---------------------------------------------------------------- taxonomy

    public async Task<Guid> CreateSpecialtyAsync(string name)
    {
        var response = await Admin.PostAsJsonAsync("/api/specialties",
            new { name, departmentId = Tree.DepartmentId, isActive = true });
        response.EnsureSuccessStatusCode();
        return (await DataAsync(response)).GetProperty("id").GetGuid();
    }

    public async Task<Guid> CreateAcademicYearAsync(int startYear)
    {
        var response = await Admin.PostAsJsonAsync("/api/academic-years", new { startYear, isCurrent = false, isActive = true });
        response.EnsureSuccessStatusCode();
        return (await DataAsync(response)).GetProperty("id").GetGuid();
    }

    public async Task<Guid> CreateSessionAsync(string name)
    {
        var response = await Admin.PostAsJsonAsync("/api/sessions", new { name, kind = "Normal", order = 1, isActive = true });
        response.EnsureSuccessStatusCode();
        return (await DataAsync(response)).GetProperty("id").GetGuid();
    }

    // ---------------------------------------------------------------- users

    public async Task<Voter> NewVoterAsync()
    {
        var (client, email) = await Factory.NewStudentClientAsync();
        var id = Data(await client.GetStringAsync("/api/me")).GetProperty("id").GetGuid();
        return new Voter(client, email, id);
    }

    public async Task<List<Voter>> NewVotersAsync(int count)
    {
        var voters = new List<Voter>();
        for (var i = 0; i < count; i++)
        {
            voters.Add(await NewVoterAsync());
        }

        return voters;
    }

    public Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action) => ScopeAsync(action);

    private async Task<T> ScopeAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

public sealed record Voter(HttpClient Client, string Email, Guid Id)
{
    /// <summary>The caller's open task items (assignment id + document id), or empty when there is nothing to do.</summary>
    public async Task<List<(Guid AssignmentId, Guid DocumentId)>> TaskAsync()
    {
        var response = await Client.PostAsync("/api/classification/tasks/next", null);
        response.EnsureSuccessStatusCode();
        var data = ClassificationHarness.Data(await response.Content.ReadAsStringAsync());
        if (data.ValueKind == JsonValueKind.Null)
        {
            return new();
        }

        return data.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("status").GetString() == "Assigned")
            .Select(i => (i.GetProperty("assignmentId").GetGuid(), i.GetProperty("documentId").GetGuid()))
            .ToList();
    }

    public Task<HttpResponseMessage> VoteAsync(Guid assignmentId, object body) =>
        Client.PostAsJsonAsync($"/api/classification/assignments/{assignmentId}/vote", body);

    public Task<HttpResponseMessage> SkipAsync(Guid assignmentId) =>
        Client.PostAsync($"/api/classification/assignments/{assignmentId}/skip", null);

    public async Task<JsonElement> PromptAsync() =>
        ClassificationHarness.Data(await Client.GetStringAsync("/api/classification/prompt"));

    public async Task<JsonElement> MeAsync() =>
        ClassificationHarness.Data(await Client.GetStringAsync("/api/classification/me"));

    public async Task<HttpResponseMessage> DownloadAsync(string slug) =>
        await Client.GetAsync($"/api/documents/{slug}/download");

    public async Task<HttpResponseMessage> LoginAgainAsync(string password = "passw0rd")
    {
        return await Client.PostAsJsonAsync("/api/auth/login", new { email = Email, password });
    }

    public void UseToken(string accessToken) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
