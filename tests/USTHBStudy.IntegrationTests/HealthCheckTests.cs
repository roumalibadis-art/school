namespace USTHBStudy.IntegrationTests;

using System.Net;
using FluentAssertions;

public class HealthCheckTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(ApiFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Swagger_is_not_served_outside_development()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
    }
}
