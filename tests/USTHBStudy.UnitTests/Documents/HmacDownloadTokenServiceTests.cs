namespace USTHBStudy.UnitTests.Documents;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using USTHBStudy.Application.Documents;
using USTHBStudy.Infrastructure.Documents;
using USTHBStudy.UnitTests.TestSupport;

public class HmacDownloadTokenServiceTests
{
    private readonly FakeClock _clock = new();

    private HmacDownloadTokenService Sut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Downloads:SigningKey"] = new string('s', 40) })
            .Build();
        return new HmacDownloadTokenService(config, _clock);
    }

    private DownloadGrant Grant(DateTime expires) =>
        new("documents/exam/abc/file.pdf", Guid.NewGuid(), Guid.NewGuid(), expires);

    [Fact]
    public void Issue_then_verify_round_trips_the_grant()
    {
        var sut = Sut();
        var grant = Grant(_clock.UtcNow.AddMinutes(2));

        var verified = sut.Verify(sut.Issue(grant));

        verified.Should().NotBeNull();
        verified!.StorageKey.Should().Be(grant.StorageKey);
        verified.DocumentId.Should().Be(grant.DocumentId);
        verified.UserId.Should().Be(grant.UserId);
    }

    [Fact]
    public void Verify_rejects_an_expired_token()
    {
        var sut = Sut();

        sut.Verify(sut.Issue(Grant(_clock.UtcNow.AddSeconds(-1)))).Should().BeNull();
    }

    [Fact]
    public void Verify_rejects_a_tampered_token()
    {
        var sut = Sut();
        var token = sut.Issue(Grant(_clock.UtcNow.AddMinutes(2)));
        var tampered = token[..^2] + (token[^1] == 'a' ? "bb" : "aa");

        sut.Verify(tampered).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("no-dot-here")]
    [InlineData(".")]
    public void Verify_rejects_malformed_tokens(string token)
    {
        Sut().Verify(token).Should().BeNull();
    }

    [Fact]
    public void A_token_signed_with_a_different_key_does_not_verify()
    {
        var issued = Sut().Issue(Grant(_clock.UtcNow.AddMinutes(2)));

        var otherConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Downloads:SigningKey"] = new string('x', 40) })
            .Build();
        var other = new HmacDownloadTokenService(otherConfig, _clock);

        other.Verify(issued).Should().BeNull();
    }
}
