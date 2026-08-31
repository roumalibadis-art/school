namespace USTHBStudy.UnitTests.Services;

using FluentAssertions;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Infrastructure.Services;
using USTHBStudy.UnitTests.TestSupport;

public class AccessControlServiceTests
{
    private readonly FakeClock _clock = new();

    private AccessControlService Sut => new(_clock);

    [Fact]
    public void IsPremiumActive_is_true_when_active_flagged_and_expiry_in_future()
    {
        var subject = new AccessSubject(IsActive: true, IsPremiumFlag: true, PremiumExpiresAt: _clock.UtcNow.AddDays(1));

        Sut.IsPremiumActive(subject).Should().BeTrue();
    }

    [Fact]
    public void IsPremiumActive_is_false_when_expiry_has_passed()
    {
        // PRD §24 / §61-#5: an expired Premium user is treated as free.
        var subject = new AccessSubject(true, true, _clock.UtcNow.AddSeconds(-1));

        Sut.IsPremiumActive(subject).Should().BeFalse();
    }

    [Fact]
    public void IsPremiumActive_is_false_when_no_expiry_is_set()
    {
        Sut.IsPremiumActive(new AccessSubject(true, true, null)).Should().BeFalse();
    }

    [Fact]
    public void IsPremiumActive_is_false_when_not_flagged_premium()
    {
        Sut.IsPremiumActive(new AccessSubject(true, false, _clock.UtcNow.AddDays(1))).Should().BeFalse();
    }

    [Fact]
    public void IsPremiumActive_is_false_when_account_is_disabled()
    {
        Sut.IsPremiumActive(new AccessSubject(false, true, _clock.UtcNow.AddDays(1))).Should().BeFalse();
    }

    [Fact]
    public void CanAccessPremiumContent_tracks_IsPremiumActive()
    {
        var active = new AccessSubject(true, true, _clock.UtcNow.AddDays(1));
        var expired = new AccessSubject(true, true, _clock.UtcNow.AddDays(-1));

        Sut.CanAccessPremiumContent(active).Should().BeTrue();
        Sut.CanAccessPremiumContent(expired).Should().BeFalse();
    }

    [Fact]
    public void EnsureAccountActive_throws_for_disabled_account()
    {
        var act = () => Sut.EnsureAccountActive(new AccessSubject(false, false, null));

        act.Should().Throw<ForbiddenAppException>();
    }

    [Fact]
    public void EnsureAccountActive_does_not_throw_for_active_account()
    {
        var act = () => Sut.EnsureAccountActive(new AccessSubject(true, false, null));

        act.Should().NotThrow();
    }
}
