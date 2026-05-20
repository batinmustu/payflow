using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Domain.Users;
using PayFlow.Multitenancy;

namespace PayFlow.Identity.Infrastructure.Security;

internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtSettings _settings;

    public JwtAccessTokenIssuer(IOptions<JwtSettings> settings, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(env);
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.SigningKey))
        {
            throw new InvalidOperationException(
                $"{JwtSettings.SectionName}:SigningKey is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(_settings.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                $"{JwtSettings.SectionName}:SigningKey must be at least 256 bits (32 bytes) for HS256.");
        }

        JwtSigningKeyGuard.ThrowIfDevKeyInProduction(_settings.SigningKey, env);
    }

    public AccessToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_settings.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            // Tenant id as a private claim — `tid` matches the convention used
            // across PayFlow services (see docs/architecture/bounded-contexts.md).
            new("tid", user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        // Short JWT claim "role" — ClaimTypes.Role expands to the legacy
        // WS-* URI which clients shouldn't have to know about.
        claims.AddRange(user.Roles.Select(r => new Claim("role", r)));

        var keyBytes = Encoding.UTF8.GetBytes(_settings.SigningKey);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
        };

        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        var value = handler.CreateToken(descriptor);

        return new AccessToken(value, expiresAt);
    }
}
