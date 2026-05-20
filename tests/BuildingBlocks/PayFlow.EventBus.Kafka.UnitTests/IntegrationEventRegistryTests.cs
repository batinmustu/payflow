using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.EventBus.Kafka.UnitTests;

public class IntegrationEventRegistryTests
{
    public sealed record OrderPayload(string OrderId);
    public sealed record RefundPayload(string RefundId);

    public sealed class OrderConsumer : IIntegrationEventConsumer<OrderPayload>
    {
        public List<IntegrationEventEnvelope<OrderPayload>> Received { get; } = [];
        public Task HandleAsync(IntegrationEventEnvelope<OrderPayload> envelope, CancellationToken ct)
        {
            Received.Add(envelope);
            return Task.CompletedTask;
        }
    }

    public sealed class RefundConsumer : IIntegrationEventConsumer<RefundPayload>
    {
        public Task HandleAsync(IntegrationEventEnvelope<RefundPayload> envelope, CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact]
    public void Register_adds_topic_to_EventTypes_list()
    {
        var registry = new IntegrationEventRegistry();
        registry.Register<OrderPayload, OrderConsumer>("payflow.orders.placed.v1");

        registry.EventTypes.Should().BeEquivalentTo(["payflow.orders.placed.v1"]);
    }

    [Fact]
    public void TryGet_returns_null_for_an_unknown_event_type()
    {
        var registry = new IntegrationEventRegistry();
        registry.Register<OrderPayload, OrderConsumer>("payflow.orders.placed.v1");

        registry.TryGet("payflow.refunds.requested.v1").Should().BeNull();
    }

    [Fact]
    public async Task DispatchAsync_resolves_the_consumer_and_invokes_HandleAsync()
    {
        var registry = new IntegrationEventRegistry();
        registry.Register<OrderPayload, OrderConsumer>("payflow.orders.placed.v1");
        var registration = registry.TryGet("payflow.orders.placed.v1");
        registration.Should().NotBeNull();

        var consumer = new OrderConsumer();
        var sp = new ServiceCollection().AddSingleton(consumer).BuildServiceProvider();

        var envelope = new IntegrationEventEnvelope<OrderPayload>(
            EventType: "payflow.orders.placed.v1",
            MessageId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            AggregateId: Guid.NewGuid(),
            AggregateType: "Order",
            CreatedAt: DateTimeOffset.UtcNow,
            Payload: new OrderPayload("ORD-1"),
            Headers: new Dictionary<string, string>());

        await registration!.DispatchAsync(sp, envelope, CancellationToken.None);

        consumer.Received.Should().ContainSingle()
            .Which.Payload.OrderId.Should().Be("ORD-1");
    }

    [Fact]
    public void Last_registration_for_the_same_event_type_wins()
    {
        var registry = new IntegrationEventRegistry();
        registry.Register<OrderPayload, OrderConsumer>("payflow.shared.topic.v1");
        registry.Register<RefundPayload, RefundConsumer>("payflow.shared.topic.v1");

        registry.TryGet("payflow.shared.topic.v1")!.PayloadType.Should().Be<RefundPayload>();
    }
}
