using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PayFlow.Reporting.API.Auth;

/// <summary>
/// Validates JWTs that Identity issues. Same shape as Transaction /
/// Reconciliation — kept inline until a fourth service needs it and the
/// helper graduates into a shared building block.
/// </summary>
internal static class JwtAuthExtensions
{
    public static IServiceCollection AddPayFlowJwtAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var jwt = configuration.GetSection("Identity:Jwt");
                var issuer = jwt["Issuer"]
                    ?? throw new InvalidOperationException("Identity:Jwt:Issuer is not configured.");
                var audience = jwt["Audience"]
                    ?? throw new InvalidOperationException("Identity:Jwt:Audience is not configured.");
                var signingKey = jwt["SigningKey"]
                    ?? throw new InvalidOperationException("Identity:Jwt:SigningKey is not configured.");

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
