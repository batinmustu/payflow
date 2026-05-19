using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PayFlow.Identity.API.Auth;

internal static class JwtAuthExtensions
{
    /// <summary>
    /// Wires JwtBearer validation against the same Identity:Jwt section the
    /// issuer reads. Identity issues and validates its own tokens for now;
    /// downstream services will later validate the same audience using either
    /// the shared symmetric key (dev) or a JWKS endpoint (prod).
    /// </summary>
    public static IServiceCollection AddPayFlowJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var jwt = configuration.GetSection("Identity:Jwt");
        var issuer = jwt["Issuer"]
            ?? throw new InvalidOperationException("Identity:Jwt:Issuer is not configured.");
        var audience = jwt["Audience"]
            ?? throw new InvalidOperationException("Identity:Jwt:Audience is not configured.");
        var signingKey = jwt["SigningKey"]
            ?? throw new InvalidOperationException("Identity:Jwt:SigningKey is not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep short claim names ("sub", "role", "tid")

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

        services.AddAuthorization();
        return services;
    }
}
