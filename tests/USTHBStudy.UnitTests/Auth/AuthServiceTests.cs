namespace USTHBStudy.UnitTests.Auth;

using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Auth;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Common;
using USTHBStudy.UnitTests.TestSupport;

public class AuthServiceTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IJwtTokenService _tokens = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenStore _refreshTokens = Substitute.For<IRefreshTokenStore>();
    private readonly FakeClock _clock = new();

    private static readonly AuthUser ActiveUser = new(
        Guid.NewGuid(), "sami@example.local", "Sami", "Student", IsActive: true, IsPremium: false, PremiumExpiresAt: null);

    private AuthService Sut()
    {
        _tokens.CreateAccessToken(Arg.Any<TokenUser>())
            .Returns(ci => new AccessToken("access-token", _clock.UtcNow.AddMinutes(15)));
        _tokens.CreateRefreshToken().Returns("new-refresh");
        _tokens.HashRefreshToken(Arg.Any<string>()).Returns(ci => $"hash::{ci.Arg<string>()}");
        _identity.GetRolesAsync(Arg.Any<Guid>()).Returns(new[] { "Student" });
        _identity.GetPermissionsAsync(Arg.Any<Guid>()).Returns(new[] { "Document.View" });

        return new AuthService(
            _identity, _tokens, _refreshTokens, _clock,
            Options.Create(new JwtOptions { Secret = new string('k', 40), RefreshTokenDays = 14 }));
    }

    [Fact]
    public async Task RegisterAsync_persists_a_refresh_token_and_returns_tokens()
    {
        _identity.CreateStudentAsync("sami@example.local", "passw0rd", "Sami", "Student")
            .Returns(Result.Success(ActiveUser));

        var result = await Sut().RegisterAsync(
            new RegisterRequest("sami@example.local", "passw0rd", "passw0rd", "Sami", "Student"), "127.0.0.1");

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("new-refresh");
        result.User.Email.Should().Be("sami@example.local");
        await _refreshTokens.Received(1).AddAsync(
            ActiveUser.Id, "hash::new-refresh", _clock.UtcNow.AddDays(14), "127.0.0.1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_throws_Conflict_when_identity_reports_failure()
    {
        _identity.CreateStudentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Failure<AuthUser>("An account with this email already exists."));

        var act = () => Sut().RegisterAsync(
            new RegisterRequest("dup@example.local", "passw0rd", "passw0rd", "A", "B"), null);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task LoginAsync_throws_Unauthorized_on_invalid_credentials()
    {
        _identity.ValidateCredentialsAsync("sami@example.local", "wrong")
            .Returns(Result.Failure<AuthUser>("Invalid credentials."));

        var act = () => Sut().LoginAsync(new LoginRequest("sami@example.local", "wrong"), null);

        await act.Should().ThrowAsync<UnauthorizedAppException>();
    }

    [Fact]
    public async Task LoginAsync_throws_Forbidden_for_disabled_account()
    {
        var disabled = ActiveUser with { IsActive = false };
        _identity.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Result.Success(disabled));

        var act = () => Sut().LoginAsync(new LoginRequest("sami@example.local", "passw0rd"), null);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task RefreshAsync_throws_Unauthorized_for_unknown_token()
    {
        _refreshTokens.FindActiveAsync(Arg.Any<string>()).Returns((StoredRefreshToken?)null);

        var act = () => Sut().RefreshAsync(new RefreshRequest("nope"), null);

        await act.Should().ThrowAsync<UnauthorizedAppException>();
    }

    [Fact]
    public async Task RefreshAsync_rotates_the_token_and_issues_a_new_pair()
    {
        _refreshTokens.FindActiveAsync("hash::old")
            .Returns(new StoredRefreshToken(Guid.NewGuid(), ActiveUser.Id, _clock.UtcNow.AddDays(3), null));
        _identity.FindByIdAsync(ActiveUser.Id).Returns(ActiveUser);

        var result = await Sut().RefreshAsync(new RefreshRequest("old"), "10.0.0.1");

        result.RefreshToken.Should().Be("new-refresh");
        await _refreshTokens.Received(1).RotateAsync(
            "hash::old", "hash::new-refresh", ActiveUser.Id,
            _clock.UtcNow.AddDays(14), "10.0.0.1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_revokes_all_and_forbids_when_account_disabled()
    {
        _refreshTokens.FindActiveAsync(Arg.Any<string>())
            .Returns(new StoredRefreshToken(Guid.NewGuid(), ActiveUser.Id, _clock.UtcNow.AddDays(3), null));
        _identity.FindByIdAsync(ActiveUser.Id).Returns(ActiveUser with { IsActive = false });

        var act = () => Sut().RefreshAsync(new RefreshRequest("old"), null);

        await act.Should().ThrowAsync<ForbiddenAppException>();
        await _refreshTokens.Received(1).RevokeAllForUserAsync(ActiveUser.Id, Arg.Any<CancellationToken>());
    }
}
