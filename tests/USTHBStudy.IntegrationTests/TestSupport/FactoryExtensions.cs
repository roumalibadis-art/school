namespace USTHBStudy.IntegrationTests.TestSupport;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public static class FactoryExtensions
{
    /// <summary>A client whose Authorization header carries a fresh access token for the given account.</summary>
    public static async Task<HttpClient> LoggedInClientAsync(this ApiFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var body = await login.ReadEnvelopeAsync<AuthResultDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.AccessToken);
        return client;
    }

    public static Task<HttpClient> AdminClientAsync(this ApiFactory factory) =>
        factory.LoggedInClientAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

    public static Task<HttpClient> StudentClientAsync(this ApiFactory factory) =>
        factory.LoggedInClientAsync(ApiFactory.StudentEmail, ApiFactory.StudentPassword);

    /// <summary>Creates University → Faculty → Department → Specialty → Level → Semester → Module and returns the module id.</summary>
    public static async Task<Guid> CreateModuleAsync(this HttpClient admin)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        var university = await Post(admin, "/api/universities", new { name = $"Univ {tag}", isActive = true });
        var faculty = await Post(admin, "/api/faculties", new { name = $"Fac {tag}", universityId = university, isActive = true });
        var department = await Post(admin, "/api/departments", new { name = $"Dept {tag}", facultyId = faculty, isActive = true });
        var specialty = await Post(admin, "/api/specialties", new { name = $"Spec {tag}", departmentId = department, isActive = true });
        var level = await Post(admin, "/api/levels",
            new { name = $"L1 {tag}", shortName = "L1", cycle = "Licence", order = 1, specialtyId = specialty, isActive = true });
        var semester = await Post(admin, "/api/semesters",
            new { name = $"S1 {tag}", shortName = "S1", order = 1, levelId = level, isActive = true });
        return await Post(admin, "/api/modules",
            new { name = $"Module {tag}", coefficient = 2.0, credits = 6, semesterId = semester, specialtyId = specialty, isActive = true });
    }

    private static async Task<Guid> Post(HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }
}

