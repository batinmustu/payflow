namespace PayFlow.SharedKernel.UnitTests;

public class BaseEntityTests
{
    private sealed class FakeEntity : BaseEntity<Guid>
    {
        public FakeEntity(Guid id) => Id = id;
    }

    private sealed class OtherEntity : BaseEntity<Guid>
    {
        public OtherEntity(Guid id) => Id = id;
    }

    [Fact]
    public void Two_entities_with_same_id_and_type_are_equal()
    {
        var id = Guid.NewGuid();
        var a = new FakeEntity(id);
        var b = new FakeEntity(id);

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var a = new FakeEntity(Guid.NewGuid());
        var b = new FakeEntity(Guid.NewGuid());

        a.Equals(b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Different_entity_types_with_the_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();
        var a = new FakeEntity(id);
        var b = new OtherEntity(id);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Null_comparison_is_handled_through_operators()
    {
        FakeEntity? a = null;
        FakeEntity? b = null;
        var c = new FakeEntity(Guid.NewGuid());

        (a == b).Should().BeTrue();
        (a == c).Should().BeFalse();
        (c == a).Should().BeFalse();
    }
}
