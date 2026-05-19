namespace PayFlow.Identity.Infrastructure.Security;

/// <summary>
/// Configuration for the JWT access token issuer. Bound from the
/// <c>Identity:Jwt</c> section of IConfiguration.
///
/// Dev uses HS256 with a symmetric signing key from configuration. A real
/// production rollout would switch to RS256 with a private key held by
/// Identity and a JWKS endpoint other services can fetch — that's a
/// follow-up; the abstraction makes either choice transparent to callers.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Identity:Jwt";

    public string Issuer { get; set; } = "PayFlow.Identity";
    public string Audience { get; set; } = "PayFlow.Services";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
