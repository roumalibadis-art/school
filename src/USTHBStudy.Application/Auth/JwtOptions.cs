namespace USTHBStudy.Application.Auth;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. <see cref="Secret"/> comes from user-secrets
/// (dev) or environment variables (prod) — never from a committed file (PRD §44).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "usthbstudy";
    public string Audience { get; set; } = "usthbstudy";
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
