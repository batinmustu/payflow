using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PayFlow.Identity.Domain.Users;
using PayFlow.Identity.Infrastructure.Security;

namespace PayFlow.Identity.UnitTests.Infrastructure;

public class JwtAccessTokenIssuerTests
{
    private const string SigningKey = "test-only-signing-key-min-32-bytes-aaaa";

    private static JwtAccessTokenIssuer NewIssuer(int lifetimeMinutes = 15) =>
        new(Options.Create(new JwtSettings
        {
            Issuer = "PayFlow.Identity.Test",
            Audience = "PayFlow.Services.Test",
            SigningKey = SigningKey,
            AccessTokenLifetimeMinutes = lifetimeMinutes,
        }));

    private static User AUser(string email = "user@example.com", string display = "Display")
    {
        var u = User.Create(
            tenantId: Guid.NewGuid(),
            email: Email.Create(email).Value,
            passwordHash: "hash",
            displayName: display).Value;
        u.GrantRole("admin");
        return u;
    }

    [Fact]
    public async Task Issued_token_validates_against_the_same_signing_key()
    {
        var issuer = NewIssuer();
        var user = AUser();

        var token = issuer.Issue(user);

        var validation = new TokenValidationParameters
        {
            ValidIssuer = "PayFlow.Identity.Test",
            ValidAudience = "PayFlow.Services.Test",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token.Value, validation);

        result.IsValid.Should().BeTrue($"validation failed: {result.Exception?.Message}");
    }

    [Fact]
    public async Task Issued_token_carries_sub_tid_email_name_and_role_claims()
    {
        var issuer = NewIssuer();
        var user = AUser("admin@acme.test", "Acme Admin");

        var token = issuer.Issue(user);

        var validation = new TokenValidationParameters
        {
            ValidIssuer = "PayFlow.Identity.Test",
            ValidAudience = "PayFlow.Services.Test",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token.Value, validation);
        result.IsValid.Should().BeTrue();

        var claims = result.Claims;
        claims["sub"].Should().Be(user.Id.ToString());
        claims["tid"].Should().Be(user.TenantId.ToString());
        claims["email"].Should().Be("admin@acme.test");
        claims["name"].Should().Be("Acme Admin");
        // role may be deserialised as either a single string or a string array
        // depending on the underlying claim collection — accept either shape.
        var roleClaim = claims["role"];
        (roleClaim is string s ? s : ((string[])roleClaim)[0]).Should().Be("admin");
    }

    [Fact]
    public void Expires_at_matches_configured_lifetime_within_a_few_seconds()
    {
        var issuer = NewIssuer(lifetimeMinutes: 30);
        var user = AUser();

        var token = issuer.Issue(user);

        token.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_throws_when_signing_key_missing()
    {
        var act = () => new JwtAccessTokenIssuer(Options.Create(new JwtSettings { SigningKey = "" }));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SigningKey*not configured*");
    }

    [Fact]
    public void Constructor_throws_when_signing_key_too_short_for_hs256()
    {
        var act = () => new JwtAccessTokenIssuer(Options.Create(new JwtSettings { SigningKey = "tooshort" }));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*256 bits*");
    }

    [Fact]
    public async Task Each_token_has_a_unique_jti()
    {
        var issuer = NewIssuer();
        var user = AUser();

        var first = issuer.Issue(user);
        var second = issuer.Issue(user);

        var validation = new TokenValidationParameters
        {
            ValidIssuer = "PayFlow.Identity.Test",
            ValidAudience = "PayFlow.Services.Test",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
        };

        var handler = new JsonWebTokenHandler();
        var a = await handler.ValidateTokenAsync(first.Value, validation);
        var b = await handler.ValidateTokenAsync(second.Value, validation);

        a.Claims["jti"].ToString().Should().NotBeNullOrEmpty();
        b.Claims["jti"].ToString().Should().NotBe(a.Claims["jti"].ToString());
    }
}
