namespace PayFlow.SharedKernel.UnitTests;

public class AggregateRootTests
{
    private sealed record FakeEvent(string Note) : DomainEvent;

    private sealed class FakeAggregate : AggregateRoot<Guid>
    {
        public FakeAggregate(Guid id) => Id = id;

        public void DoSomething(string note) => Raise(new FakeEvent(note));
    }

    [Fact]
    public void New_aggregate_has_no_domain_events()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Raise_appends_to_domain_events_in_order()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.DoSomething("first");
        aggregate.DoSomething("second");

        aggregate.DomainEvents.Should().HaveCount(2);
        aggregate.DomainEvents.OfType<FakeEvent>()
            .Select(e => e.Note)
            .Should().Equal("first", "second");
    }

    [Fact]
    public void ClearDomainEvents_empties_the_list()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());
        aggregate.DoSomething("once");

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Each_domain_event_gets_a_unique_id_and_a_timestamp()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.DoSomething("a");
        aggregate.DoSomething("b");

        var events = aggregate.DomainEvents.OfType<FakeEvent>().ToList();
        events[0].EventId.Should().NotBe(events[1].EventId);
        events[0].OccurredAt.Should().BeOnOrBefore(events[1].OccurredAt);
    }
}
