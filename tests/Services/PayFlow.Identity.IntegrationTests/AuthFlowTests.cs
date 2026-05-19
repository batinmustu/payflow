using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PayFlow.Identity.IntegrationTests;

/// <summary>
/// End-to-end exercise of M1's deliverable — register a tenant, log in as
/// its admin, call a protected endpoint with the issued JWT. Runs against
/// a real Postgres in Testcontainers and the actual Identity API host.
/// </summary>
public sealed class AuthFlowTests : IClassFixture<IdentityAppFixture>
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuthFlowTests(IdentityAppFixture fixture)
    {
        _http = fixture.CreateClient();
    }

    [Fact]
    public async Task Register_then_login_then_call_protected_endpoint_works_end_to_end()
    {
        var slug = $"acme-{Guid.NewGuid():N}".Substring(0, 20);

        // 1. Register tenant + admin
        var register = await _http.PostAsJsonAsync("/api/tenants", new
        {
            name = "Acme Test",
            slug,
            adminEmail = $"admin@{slug}.test",
            adminPassword = "supersecret",
            adminDisplayName = "Acme Admin",
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await register.Content.ReadFromJsonAsync<TenantResponse>(Json);
        created!.TenantId.Should().NotBe(Guid.Empty);
        created.AdminUserId.Should().NotBe(Guid.Empty);

        // 2. Login as that admin
        var login = await _http.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            email = $"admin@{slug}.test",
            password = "supersecret",
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);
        loginBody!.AccessToken.Should().NotBeNullOrEmpty();
        loginBody.UserId.Should().Be(created.AdminUserId);
        loginBody.TenantId.Should().Be(created.TenantId);

        // 3. Call /api/me with the issued token
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);
        var me = await _http.SendAsync(meRequest);

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(Json);
        meBody!.UserId.Should().Be(created.AdminUserId.ToString());
        meBody.TenantId.Should().Be(created.TenantId.ToString());
        meBody.Email.Should().Be($"admin@{slug}.test");
        meBody.DisplayName.Should().Be("Acme Admin");
        meBody.Roles.Should().Contain("admin");
    }

    [Fact]
    public async Task GET_me_without_token_returns_401()
    {
        var response = await _http.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_AUTH_INVALID_CREDENTIALS()
    {
        var slug = $"login-{Guid.NewGuid():N}".Substring(0, 20);
        var register = await _http.PostAsJsonAsync("/api/tenants", new
        {
            name = "Login Test",
            slug,
            adminEmail = $"admin@{slug}.test",
            adminPassword = "supersecret",
            adminDisplayName = "Admin",
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await _http.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            email = $"admin@{slug}.test",
            password = "WRONG",
        });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        problem.GetProperty("code").GetString().Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Duplicate_slug_returns_409_with_TENANT_SLUG_TAKEN_in_errors()
    {
        var slug = $"dup-{Guid.NewGuid():N}".Substring(0, 20);

        var first = await _http.PostAsJsonAsync("/api/tenants", new
        {
            name = "First Co",
            slug,
            adminEmail = $"admin@{slug}.test",
            adminPassword = "supersecret",
            adminDisplayName = "Admin",
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _http.PostAsJsonAsync("/api/tenants", new
        {
            name = "Second Co",
            slug,
            adminEmail = $"another@{slug}.test",
            adminPassword = "supersecret",
            adminDisplayName = "Admin",
        });

        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await dup.Content.ReadFromJsonAsync<JsonElement>(Json);
        body.GetProperty("errors").GetProperty("Slug")[0].GetString().Should().Be("TENANT_SLUG_TAKEN");
    }

    private sealed record TenantResponse(Guid TenantId, Guid AdminUserId, string Slug);
    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, Guid TenantId);
    private sealed record MeResponse(string UserId, string TenantId, string Email, string DisplayName, string[] Roles);
}
