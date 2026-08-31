namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>Covers the always-green security cases from PRD §61 that Phase 1 can exercise.</summary>
public class AuthorizationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<HttpClient> ClientFor(string email, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var body = await login.ReadEnvelopeAsync<AuthResultDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_a_valid_token_returns_the_current_user()
    {
        var client = await ClientFor(ApiFactory.StudentEmail, ApiFactory.StudentPassword);

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadEnvelopeAsync<CurrentUserDto>();
        body.Data!.Email.Should().Be(ApiFactory.StudentEmail);
        body.Data.Roles.Should().Contain("Student");
    }

    [Fact]
    public async Task Student_calling_a_permission_gated_endpoint_gets_403()
    {
        // PRD §61-#3: a Student hitting an admin-only capability must be forbidden.
        var client = await ClientFor(ApiFactory.StudentEmail, ApiFactory.StudentPassword);

        var response = await client.GetAsync("/api/diagnostics/permission-probe");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_calling_the_permission_gated_endpoint_succeeds()
    {
        var client = await ClientFor(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

        var response = await client.GetAsync("/api/diagnostics/permission-probe");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        // PRD §61-#6: a forged/edited token must not authenticate.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJoYWNrZXIifQ.not-a-real-signature");

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
