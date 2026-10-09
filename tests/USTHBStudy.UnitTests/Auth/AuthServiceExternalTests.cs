namespace USTHBStudy.UnitTests.Auth;

using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Auth;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Classification;
using USTHBStudy.Application.Common;
using USTHBStudy.UnitTests.TestSupport;

public class AuthServiceExternalTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IJwtTokenService _tokens = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenStore _refreshTokens = Substitute.For<IRefreshTokenStore>();
    private readonly IContributionTracker _tracker = Substitute.For<IContributionTracker>();
    private readonly FakeClock _clock = new();

    private static AuthUser User(bool active = true) =>
        new(Guid.NewGuid(), "u@example.local", "U", "V", active, false, null);

    private AuthService Sut()
    {
        _tokens.CreateAccessToken(Arg.Any<TokenUser>()).Returns(new AccessToken("access", _clock.UtcNow.AddMinutes(15)));
        _tokens.CreateRefreshToken().Returns("refresh");
        _tokens.HashRefreshToken(Arg.Any<string>()).Returns(ci => "hash:" + ci.Arg<string>());
        _identity.GetRolesAsync(Arg.Any<Guid>()).Returns(new[] { "Student" });
        _identity.GetPermissionsAsync(Arg.Any<Guid>()).Returns(new[] { "Document.View" });
        return new AuthService(_identity, _tokens, _refreshTokens, _clock,
            Options.Create(new JwtOptions { Secret = new string('k', 40), RefreshTokenDays = 14 }), _tracker);
    }

    [Fact]
    public async Task Password_login_records_the_login_for_the_contribution_trigger()
    {
        var user = User();
        _identity.ValidateCredentialsAsync("u@example.local", "pw").Returns(Result.Success(user));

        await Sut().LoginAsync(new LoginRequest("u@example.local", "pw"), null);

        await _tracker.Received(1).RecordLoginAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_login_is_not_recorded()
    {
        _identity.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Result.Failure<AuthUser>("bad"));

        var act = () => Sut().LoginAsync(new LoginRequest("u@example.local", "nope"), null);

        await act.Should().ThrowAsync<UnauthorizedAppException>();
        await _tracker.DidNotReceiveWithAnyArgs().RecordLoginAsync(default);
    }

    [Fact]
    public async Task External_sign_in_issues_the_same_token_pair_and_records_the_login()
    {
        var user = User();
        _identity.FindByIdAsync(user.Id).Returns(user);

        var result = await Sut().SignInExternalAsync(user.Id, "1.2.3.4");

        result.AccessToken.Should().Be("access");
        result.RefreshToken.Should().Be("refresh");
        result.User.Roles.Should().Equal("Student");
        await _refreshTokens.Received(1).AddAsync(user.Id, "hash:refresh", _clock.UtcNow.AddDays(14), "1.2.3.4", Arg.Any<CancellationToken>());
        await _tracker.Received(1).RecordLoginAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task External_sign_in_refuses_suspended_and_unknown_users()
    {
        var suspended = User(active: false);
        _identity.FindByIdAsync(suspended.Id).Returns(suspended);
        var sut = Sut();

        await ((Func<Task>)(() => sut.SignInExternalAsync(suspended.Id, null))).Should().ThrowAsync<ForbiddenAppException>();
        await ((Func<Task>)(() => sut.SignInExternalAsync(Guid.NewGuid(), null))).Should().ThrowAsync<UnauthorizedAppException>();
        await _refreshTokens.DidNotReceiveWithAnyArgs().AddAsync(default, default!, default, default, default);
    }
}
