using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PayFlow.Reconciliation.Infrastructure.Payments;

/// <summary>
/// Mints short-lived JWTs that Reconciliation can present to Payment when
/// the saga calls the refund endpoint. Saga work is event-driven so there
/// is no end-user token to forward — instead Reconciliation signs its own,
/// using the same Identity:Jwt config every PayFlow service trusts. The
/// <c>tid</c> claim carries the saga's tenant id so Payment's
/// multitenancy middleware sees the correct scope.
/// </summary>
public interface IServiceTokenIssuer
{
    string IssueForTenant(Guid tenantId);
}

internal sealed class ServiceTokenIssuer : IServiceTokenIssuer
{
    private const string ServiceSubject = "service:payflow-reconciliation";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _credentials;

    public ServiceTokenIssuer(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var jwt = configuration.GetSection("Identity:Jwt");
        _issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Identity:Jwt:Issuer not configured.");
        _audience = jwt["Audience"] ?? throw new InvalidOperationException("Identity:Jwt:Audience not configured.");
        var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Identity:Jwt:SigningKey not configured.");

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Identity:Jwt:SigningKey must be at least 256 bits (32 bytes) for HS256.");
        }

        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public string IssueForTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId must not be empty.", nameof(tenantId));
        }

        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, ServiceSubject),
                new Claim("tid", tenantId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            }),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(Lifetime).UtcDateTime,
            SigningCredentials = _credentials,
        };

        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return handler.CreateToken(descriptor);
    }
}
