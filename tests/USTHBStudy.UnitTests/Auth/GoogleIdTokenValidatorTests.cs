namespace USTHBStudy.UnitTests.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using USTHBStudy.Application.Common;
using USTHBStudy.Infrastructure.Auth.Google;

public class GoogleIdTokenValidatorTests : IDisposable
{
    private const string ClientId = "client-123.apps.googleusercontent.com";
    private const string Nonce = "expected-nonce-value";
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RsaSecurityKey _key;
    private readonly GoogleIdTokenValidator _sut = new(ClientId, () => Now);

    public GoogleIdTokenValidatorTests() => _key = new RsaSecurityKey(_rsa) { KeyId = "k1" };

    public void Dispose() => _rsa.Dispose();

    private string Token(
        string issuer = "https://accounts.google.com",
        string audience = ClientId,
        string? nonce = Nonce,
        DateTime? expires = null,
        DateTime? notBefore = null,
        string? email = "gina@example.test",
        string emailVerified = "true",
        string? sub = "1234567890",
        SecurityKey? signWith = null,
        string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var claims = new List<Claim>();
        void Add(string type, string? value)
        {
            if (value is not null)
            {
                claims.Add(new Claim(type, value));
            }
        }

        Add("sub", sub);
        Add("email", email);
        Add("email_verified", emailVerified);
        Add("nonce", nonce);
        Add("given_name", "Gina");
        Add("family_name", "Googler");

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? Now.AddMinutes(-1),
            Expires = expires ?? Now.AddMinutes(30),
            IssuedAt = Now.AddMinutes(-1),
            SigningCredentials = new SigningCredentials(signWith ?? _key, algorithm),
        };
        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private SecurityKey[] Keys => new SecurityKey[] { _key };

    [Fact]
    public void A_well_formed_token_yields_the_identity()
    {
        var identity = _sut.Validate(Token(), Keys, Nonce);

        identity.Subject.Should().Be("1234567890");
        identity.Email.Should().Be("gina@example.test");
        identity.EmailVerified.Should().BeTrue();
        identity.GivenName.Should().Be("Gina");
        identity.FamilyName.Should().Be("Googler");
    }

    [Fact]
    public void Both_google_issuer_spellings_are_accepted()
    {
        _sut.Validate(Token(issuer: "accounts.google.com"), Keys, Nonce).Subject.Should().Be("1234567890");
    }

    [Fact]
    public void An_unverified_email_is_reported_so_the_caller_can_refuse_it()
    {
        _sut.Validate(Token(emailVerified: "false"), Keys, Nonce).EmailVerified.Should().BeFalse();
    }

    public static TheoryData<string, Func<GoogleIdTokenValidatorTests, string>> Bad => new()
    {
        { "wrong audience", t => t.Token(audience: "someone-elses-client") },
        { "wrong issuer", t => t.Token(issuer: "https://evil.example") },
        { "expired", t => t.Token(expires: Now.AddMinutes(-10), notBefore: Now.AddHours(-2)) },
        { "not yet valid", t => t.Token(notBefore: Now.AddHours(1), expires: Now.AddHours(2)) },
        { "wrong nonce", t => t.Token(nonce: "attacker-nonce") },
        { "missing nonce", t => t.Token(nonce: null) },
        { "missing subject", t => t.Token(sub: null) },
        { "missing email", t => t.Token(email: null) },
        { "signed by an unknown key", t => t.Token(signWith: new RsaSecurityKey(RSA.Create(2048)) { KeyId = "k1" }) },
        { "garbage", _ => "not.a.jwt" },
        { "empty", _ => "" },
    };

    [Theory]
    [MemberData(nameof(Bad))]
    public void Invalid_tokens_are_rejected(string _, Func<GoogleIdTokenValidatorTests, string> make)
    {
        var act = () => _sut.Validate(make(this), Keys, Nonce);
        act.Should().Throw<UnauthorizedAppException>();
    }

    [Fact]
    public void A_token_signed_with_a_symmetric_key_cannot_pass_as_google_algorithm_confusion()
    {
        var secret = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('s', 64))) { KeyId = "k1" };
        var forged = Token(signWith: secret, algorithm: SecurityAlgorithms.HmacSha256);

        var act = () => _sut.Validate(forged, new SecurityKey[] { secret }, Nonce);
        act.Should().Throw<UnauthorizedAppException>();
    }

    [Fact]
    public void An_unsigned_alg_none_token_is_rejected()
    {
        string B64(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        var unsigned = $"{B64("{\"alg\":\"none\",\"typ\":\"JWT\"}")}." +
                       $"{B64($"{{\"iss\":\"https://accounts.google.com\",\"aud\":\"{ClientId}\",\"sub\":\"1\",\"email\":\"a@b.c\",\"nonce\":\"{Nonce}\",\"exp\":{new DateTimeOffset(Now.AddMinutes(5)).ToUnixTimeSeconds()}}}")}.";

        var act = () => _sut.Validate(unsigned, Keys, Nonce);
        act.Should().Throw<UnauthorizedAppException>();
    }
}
