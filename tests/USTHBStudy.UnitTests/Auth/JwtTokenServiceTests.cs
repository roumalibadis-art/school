namespace USTHBStudy.UnitTests.Auth;

using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using USTHBStudy.Application.Auth;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Infrastructure.Auth;
using USTHBStudy.UnitTests.TestSupport;

public class JwtTokenServiceTests
{
    private readonly FakeClock _clock = new();

    private JwtTokenService Sut => new(
        Options.Create(new JwtOptions
        {
            Secret = new string('k', 40),
            Issuer = "usthb-test",
            Audience = "usthb-test",
            AccessTokenMinutes = 15,
        }),
        _clock);

    private static TokenUser SampleUser(params string[] permissions) => new(
        Guid.NewGuid(),
        "sami@example.local",
        "Sami",
        "Student",
        Roles: new[] { "Student" },
        Permissions: permissions);

    [Fact]
    public void CreateAccessToken_sets_expiry_from_clock_plus_configured_minutes()
    {
        var token = Sut.CreateAccessToken(SampleUser());

        token.ExpiresAtUtc.Should().BeCloseTo(_clock.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateAccessToken_includes_sub_email_role_and_permission_claims()
    {
        var user = SampleUser("Document.View", "Document.Publish");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Sut.CreateAccessToken(user).Value);

        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Student");
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "Document.View");
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "Document.Publish");
        jwt.Issuer.Should().Be("usthb-test");
    }

    [Fact]
    public void HashRefreshToken_is_deterministic_uppercase_hex()
    {
        const string raw = "some-refresh-token-value";

        var first = Sut.HashRefreshToken(raw);
        var second = Sut.HashRefreshToken(raw);

        first.Should().Be(second);
        first.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void CreateRefreshToken_returns_unique_url_safe_values()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => Sut.CreateRefreshToken()).ToList();

        tokens.Should().OnlyHaveUniqueItems();
        tokens.Should().OnlyContain(t => t.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
    }
}
