using PayFlow.Identity.Domain.Tenants;

namespace PayFlow.Identity.UnitTests.Tenants;

public class TenantTests
{
    [Fact]
    public void Create_succeeds_with_valid_name_and_slug()
    {
        var result = Tenant.Create("Acme Payments", "acme");

        result.IsSuccess.Should().BeTrue();
        var tenant = result.Value;
        tenant.Name.Should().Be("Acme Payments");
        tenant.Slug.Should().Be("acme");
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.Id.Should().NotBe(Guid.Empty);
        tenant.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_normalises_slug_to_lowercase_and_trims_name()
    {
        var result = Tenant.Create("  Acme  ", "  ACME-Payments  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Acme");
        result.Value.Slug.Should().Be("acme-payments");
    }

    [Fact]
    public void Create_raises_TenantCreatedDomainEvent_once()
    {
        var tenant = Tenant.Create("Acme", "acme").Value;

        tenant.DomainEvents.Should().ContainSingle();
        tenant.DomainEvents.OfType<TenantCreatedDomainEvent>().Single()
            .Should().BeEquivalentTo(new
            {
                TenantId = tenant.Id,
                Name = "Acme",
                Slug = "acme",
            });
    }

    [Theory]
    [InlineData(null, "TENANT_NAME_REQUIRED")]
    [InlineData("", "TENANT_NAME_REQUIRED")]
    [InlineData("   ", "TENANT_NAME_REQUIRED")]
    public void Create_rejects_blank_name(string? name, string expectedCode)
    {
        var result = Tenant.Create(name!, "acme");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(null, "TENANT_SLUG_REQUIRED")]
    [InlineData("", "TENANT_SLUG_REQUIRED")]
    [InlineData("-leading", "TENANT_SLUG_INVALID")]
    [InlineData("trailing-", "TENANT_SLUG_INVALID")]
    [InlineData("has spaces", "TENANT_SLUG_INVALID")]
    [InlineData("Üppercase-Türkçe", "TENANT_SLUG_INVALID")]
    [InlineData("dot.in.slug", "TENANT_SLUG_INVALID")]
    public void Create_validates_slug_shape(string? slug, string expectedCode)
    {
        var result = Tenant.Create("Acme", slug!);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(expectedCode);
    }

    [Fact]
    public void Deactivate_moves_active_tenant_to_inactive()
    {
        var tenant = Tenant.Create("Acme", "acme").Value;

        tenant.Deactivate();

        tenant.Status.Should().Be(TenantStatus.Inactive);
    }

    [Fact]
    public void Deactivate_is_idempotent()
    {
        var tenant = Tenant.Create("Acme", "acme").Value;
        tenant.Deactivate();

        var act = tenant.Deactivate;

        act.Should().NotThrow();
        tenant.Status.Should().Be(TenantStatus.Inactive);
    }
}
