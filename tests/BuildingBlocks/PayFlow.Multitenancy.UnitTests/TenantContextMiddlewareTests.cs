using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PayFlow.Multitenancy.UnitTests;

public class TenantContextMiddlewareTests
{
    [Fact]
    public async Task Sets_tenant_id_from_the_tid_claim()
    {
        var tenantId = Guid.NewGuid();
        var (middleware, tenantContext) = Build();
        var http = HttpWith(new Claim("tid", tenantId.ToString()));

        await middleware.InvokeAsync(http, tenantContext);

        tenantContext.TenantId.Should().Be(tenantId);
        tenantContext.IsResolved.Should().BeTrue();
    }

    [Fact]
    public async Task Leaves_context_unresolved_when_no_tid_claim()
    {
        var (middleware, tenantContext) = Build();
        var http = HttpWith(); // anonymous

        await middleware.InvokeAsync(http, tenantContext);

        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Ignores_a_malformed_tid_claim_rather_than_crashing()
    {
        var (middleware, tenantContext) = Build();
        var http = HttpWith(new Claim("tid", "not-a-guid"));

        await middleware.InvokeAsync(http, tenantContext);

        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Always_invokes_the_next_delegate()
    {
        var nextCalled = false;
        var middleware = new TenantContextMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var http = HttpWith();

        await middleware.InvokeAsync(http, new TenantContext());

        nextCalled.Should().BeTrue();
    }

    private static (TenantContextMiddleware, TenantContext) Build()
    {
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);
        return (middleware, new TenantContext());
    }

    private static DefaultHttpContext HttpWith(params Claim[] claims)
    {
        var http = new DefaultHttpContext
        {
            User = claims.Length == 0
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
        };
        return http;
    }
}
