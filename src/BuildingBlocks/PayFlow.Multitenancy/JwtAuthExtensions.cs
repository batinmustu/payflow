using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace PayFlow.Multitenancy;

/// <summary>
/// One call wires up JwtBearer validation against the shared
/// <c>Identity:Jwt</c> section. Used by every PayFlow API except Identity
/// itself (Identity is the issuer, not a validator). Refuses to start
/// when the production environment is configured with the committed dev
/// signing key.
/// </summary>
public static class JwtAuthExtensions
{
    public static IServiceCollection AddPayFlowJwtAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration, IHostEnvironment>((options, configuration, env) =>
            {
                var jwt = configuration.GetSection("Identity:Jwt");
                var issuer = jwt["Issuer"]
                    ?? throw new InvalidOperationException("Identity:Jwt:Issuer is not configured.");
                var audience = jwt["Audience"]
                    ?? throw new InvalidOperationException("Identity:Jwt:Audience is not configured.");
                var signingKey = jwt["SigningKey"]
                    ?? throw new InvalidOperationException("Identity:Jwt:SigningKey is not configured.");

                JwtSigningKeyGuard.ThrowIfDevKeyInProduction(signingKey, env);

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                };
            });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();
        return services;
    }
}
