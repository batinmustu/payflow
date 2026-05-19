namespace PayFlow.Multitenancy.UnitTests;

public class TenantContextTests
{
    [Fact]
    public void New_context_is_unresolved_and_has_no_tenant_id()
    {
        var ctx = new TenantContext();

        ctx.IsResolved.Should().BeFalse();
        ctx.TenantId.Should().BeNull();
    }

    [Fact]
    public void Set_records_the_tenant_id_and_marks_resolved()
    {
        var ctx = new TenantContext();
        var id = Guid.NewGuid();

        ctx.Set(id);

        ctx.TenantId.Should().Be(id);
        ctx.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void Set_rejects_an_empty_guid()
    {
        var ctx = new TenantContext();

        var act = () => ctx.Set(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Set_is_idempotent_for_the_same_tenant_id()
    {
        var ctx = new TenantContext();
        var id = Guid.NewGuid();
        ctx.Set(id);

        var act = () => ctx.Set(id);

        act.Should().NotThrow();
        ctx.TenantId.Should().Be(id);
    }

    [Fact]
    public void Set_throws_when_reassigning_a_different_tenant_id()
    {
        var ctx = new TenantContext();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        ctx.Set(a);

        var act = () => ctx.Set(b);

        act.Should().Throw<InvalidOperationException>();
    }
}
