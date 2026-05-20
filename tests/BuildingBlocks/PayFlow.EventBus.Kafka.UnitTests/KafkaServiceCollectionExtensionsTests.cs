using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.EventBus.Kafka.UnitTests;

public class KafkaServiceCollectionExtensionsTests
{
    public sealed record RefundPayload(string RefundId);

    public sealed class RefundConsumer : IIntegrationEventConsumer<RefundPayload>
    {
        public Task HandleAsync(IntegrationEventEnvelope<RefundPayload> envelope, CancellationToken ct)
            => Task.CompletedTask;
    }

    // Empty configuration is fine — the registration uses GetSection which
    // returns a defaulted section when no providers are wired up. The tests
    // here exercise the registry/lifetime wiring, not the options binding.
    private static ConfigurationRoot BuildConfig() =>
        new(new List<IConfigurationProvider>());

    [Fact]
    public void Multiple_AddPayFlowKafkaConsumer_calls_share_one_registry_instance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPayFlowKafkaConsuming(BuildConfig());
        services.AddPayFlowKafkaConsumer<RefundPayload, RefundConsumer>("payflow.refund.requested.v1");
        services.AddPayFlowKafkaConsumer<RefundPayload, RefundConsumer>("payflow.refund.completed.v1");

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IntegrationEventRegistry>();

        registry.EventTypes.Should().BeEquivalentTo(
            ["payflow.refund.requested.v1", "payflow.refund.completed.v1"]);
    }

    [Fact]
    public void AddPayFlowKafkaConsumer_works_when_called_before_AddPayFlowKafkaConsuming()
    {
        // Service registration order shouldn't matter — both call paths must
        // end up pointing at the same registry instance.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPayFlowKafkaConsumer<RefundPayload, RefundConsumer>("payflow.refund.requested.v1");
        services.AddPayFlowKafkaConsuming(BuildConfig());

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IntegrationEventRegistry>()
            .EventTypes.Should().Contain("payflow.refund.requested.v1");
    }

    [Fact]
    public void Consumer_is_registered_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPayFlowKafkaConsuming(BuildConfig());
        services.AddPayFlowKafkaConsumer<RefundPayload, RefundConsumer>("payflow.refund.requested.v1");

        using var sp = services.BuildServiceProvider();
        using var s1 = sp.CreateScope();
        using var s2 = sp.CreateScope();

        var a = s1.ServiceProvider.GetRequiredService<RefundConsumer>();
        var b = s1.ServiceProvider.GetRequiredService<RefundConsumer>();
        var c = s2.ServiceProvider.GetRequiredService<RefundConsumer>();

        a.Should().BeSameAs(b);    // same scope → same instance
        a.Should().NotBeSameAs(c); // different scope → different instance
    }
}
