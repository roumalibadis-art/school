namespace USTHBStudy.IntegrationTests.TestSupport;

using System.Net.Http.Headers;
using System.Net.Http.Json;

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
}
