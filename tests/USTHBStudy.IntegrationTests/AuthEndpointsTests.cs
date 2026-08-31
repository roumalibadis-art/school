namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

public class AuthEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Register_then_login_then_refresh_issues_and_rotates_tokens()
    {
        var email = $"user-{Guid.NewGuid():N}@example.local";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "passw0rd",
            confirmPassword = "passw0rd",
            firstName = "Reg",
            lastName = "Ister",
        });
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var registered = await register.ReadEnvelopeAsync<AuthResultDto>();
        registered.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        registered.Data.User.Roles.Should().Contain("Student");

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "passw0rd" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loggedIn = await login.ReadEnvelopeAsync<AuthResultDto>();

        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = loggedIn.Data!.RefreshToken,
        });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refresh.ReadEnvelopeAsync<AuthResultDto>();
        refreshed.Data!.RefreshToken.Should().NotBe(loggedIn.Data.RefreshToken);

        // The original refresh token is now revoked (single-use rotation — PRD §44).
        var reuse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = loggedIn.Data.RefreshToken,
        });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_with_error_envelope()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiFactory.AdminEmail,
            password = "not-the-password",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.ReadEnvelopeAsync<object>();
        body.Success.Should().BeFalse();
        body.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Register_with_invalid_body_returns_400_with_field_errors()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "short",
            confirmPassword = "mismatch",
            firstName = "",
            lastName = "",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.ReadEnvelopeAsync<object>();
        body.Success.Should().BeFalse();
        body.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Duplicate_registration_returns_409()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.local";
        object payload = new
        {
            email,
            password = "passw0rd",
            confirmPassword = "passw0rd",
            firstName = "Dup",
            lastName = "Licate",
        };

        (await _client.PostAsJsonAsync("/api/auth/register", payload)).EnsureSuccessStatusCode();
        var second = await _client.PostAsJsonAsync("/api/auth/register", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
