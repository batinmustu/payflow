using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Application.Deliveries;
using PayFlow.Webhooks.Domain.Deliveries;
using PayFlow.Webhooks.Domain.Subscriptions;
using PayFlow.Webhooks.Infrastructure.Persistence;

namespace PayFlow.Webhooks.IntegrationTests;

[Collection("webhooks-db")]
public class WebhookFlowTests
{
    private readonly WebhooksDbFixture _fx;
    public WebhookFlowTests(WebhooksDbFixture fx)
    {
        _fx = fx;
        // Shared fixture across tests — clear recorder state between scenarios.
        _fx.HttpMock.Reset();
    }

    [Fact]
    public async Task Fanout_creates_one_delivery_per_active_subscription_and_marks_sent_on_2xx()
    {
        var tenantId = Guid.NewGuid();
        const string EventType = "payflow.transaction.captured.v1";

        await CreateSubscriptionAsync(tenantId, EventType, "https://merchant-a.example/hook");
        await CreateSubscriptionAsync(tenantId, EventType, "https://merchant-b.example/hook");

        var sourceMessageId = Guid.NewGuid();
        await EnqueueAsync(tenantId, EventType, sourceMessageId, new { transactionId = Guid.NewGuid() });

        _fx.HttpMock.Calls.Should().HaveCount(2);
        _fx.HttpMock.Calls.Select(c => c.Url).Should().BeEquivalentTo(
            ["https://merchant-a.example/hook", "https://merchant-b.example/hook"]);

        var deliveries = await ListDeliveriesAsync(tenantId);
        deliveries.Should().HaveCount(2);
        deliveries.Should().OnlyContain(d => d.State == WebhookDeliveryState.Sent);
        deliveries.Should().OnlyContain(d => d.AttemptCount == 1);
        deliveries.Should().OnlyContain(d => d.LastStatusCode == 200);
        deliveries.Should().OnlyContain(d => d.NextAttemptAt == null);
    }

    [Fact]
    public async Task Signature_header_is_HMAC_SHA256_of_body_with_subscription_secret()
    {
        var tenantId = Guid.NewGuid();
        const string EventType = "payflow.refund.completed.v1";
        var subscription = await CreateSubscriptionAsync(tenantId, EventType, "https://m.example/hook");

        await EnqueueAsync(tenantId, EventType, Guid.NewGuid(), new { refundId = Guid.NewGuid() });

        var call = _fx.HttpMock.Calls.Should().ContainSingle().Subject;
        var expected = ComputeExpectedSignature(call.Payload, subscription.Secret);
        call.Signature.Should().Be(expected);
    }

    [Fact]
    public async Task Transient_5xx_keeps_delivery_pending_and_sets_next_attempt_at()
    {
        var tenantId = Guid.NewGuid();
        const string EventType = "payflow.transaction.captured.v1";
        await CreateSubscriptionAsync(tenantId, EventType, "https://flaky.example/hook");

        _fx.HttpMock.Responses.Enqueue(WebhookHttpResult.NonSuccess(503, "HTTP_503"));

        await EnqueueAsync(tenantId, EventType, Guid.NewGuid(), new { transactionId = Guid.NewGuid() });

        var delivery = (await ListDeliveriesAsync(tenantId)).Should().ContainSingle().Subject;
        delivery.State.Should().Be(WebhookDeliveryState.Pending);
        delivery.AttemptCount.Should().Be(1);
        delivery.LastStatusCode.Should().Be(503);
        delivery.NextAttemptAt.Should().NotBeNull()
            .And.BeAfter(DateTimeOffset.UtcNow, "transient failure schedules a future retry");
        delivery.FailedAt.Should().BeNull();
    }

    [Fact]
    public async Task Terminal_4xx_marks_delivery_failed_immediately()
    {
        var tenantId = Guid.NewGuid();
        const string EventType = "payflow.transaction.captured.v1";
        await CreateSubscriptionAsync(tenantId, EventType, "https://gone.example/hook");

        _fx.HttpMock.Responses.Enqueue(WebhookHttpResult.NonSuccess(410, "HTTP_410"));

        await EnqueueAsync(tenantId, EventType, Guid.NewGuid(), new { transactionId = Guid.NewGuid() });

        var delivery = (await ListDeliveriesAsync(tenantId)).Should().ContainSingle().Subject;
        delivery.State.Should().Be(WebhookDeliveryState.Failed);
        delivery.AttemptCount.Should().Be(1);
        delivery.LastStatusCode.Should().Be(410);
        delivery.NextAttemptAt.Should().BeNull();
        delivery.FailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Duplicate_source_message_for_same_subscription_is_skipped()
    {
        var tenantId = Guid.NewGuid();
        const string EventType = "payflow.transaction.captured.v1";
        await CreateSubscriptionAsync(tenantId, EventType, "https://m.example/hook");

        var sourceMessageId = Guid.NewGuid();
        await EnqueueAsync(tenantId, EventType, sourceMessageId, new { transactionId = Guid.NewGuid() });
        await EnqueueAsync(tenantId, EventType, sourceMessageId, new { transactionId = Guid.NewGuid() });

        var deliveries = await ListDeliveriesAsync(tenantId);
        deliveries.Should().HaveCount(1, "second enqueue should hit the existence check");
        _fx.HttpMock.Calls.Should().HaveCount(1, "no second POST either");
    }

    [Fact]
    public async Task Deactivated_subscription_does_not_receive_fanout()
    {
        var tenantId = Guid.NewGuid();
        const string EventType = "payflow.transaction.captured.v1";
        var sub = await CreateSubscriptionAsync(tenantId, EventType, "https://m.example/hook");

        await DeactivateAsync(tenantId, sub.Id);

        await EnqueueAsync(tenantId, EventType, Guid.NewGuid(), new { transactionId = Guid.NewGuid() });

        _fx.HttpMock.Calls.Should().BeEmpty();
        (await ListDeliveriesAsync(tenantId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Redispatch_completes_a_previously_transient_delivery()
    {
        var tenantId = Guid.NewGuid();
        const string EventType = "payflow.transaction.captured.v1";
        await CreateSubscriptionAsync(tenantId, EventType, "https://m.example/hook");

        // First attempt: transient → Pending with attempt_count=1.
        _fx.HttpMock.Responses.Enqueue(WebhookHttpResult.NonSuccess(503, "HTTP_503"));
        await EnqueueAsync(tenantId, EventType, Guid.NewGuid(), new { transactionId = Guid.NewGuid() });
        var deliveryId = (await ListDeliveriesAsync(tenantId)).Single().Id;

        // Recovery sweeper would call Redispatch — simulate it. Mock now
        // returns 200; expect terminal Sent.
        _fx.HttpMock.Responses.Enqueue(WebhookHttpResult.Ok(200));
        await RedispatchAsync(deliveryId);

        var delivery = (await ListDeliveriesAsync(tenantId)).Single();
        delivery.State.Should().Be(WebhookDeliveryState.Sent);
        delivery.AttemptCount.Should().Be(2);
        delivery.LastStatusCode.Should().Be(200);
        delivery.NextAttemptAt.Should().BeNull();
        _fx.HttpMock.Calls.Should().HaveCount(2);
    }

    // ---- helpers --------------------------------------------------------

    private async Task<WebhookSubscription> CreateSubscriptionAsync(
        Guid tenantId, string eventType, string url)
    {
        // Bypass the MediatR command — we want a deterministic secret per
        // test for signature assertions and we don't need the validation
        // pipeline here (covered by the domain unit tests).
        var secret = new string('s', 40);
        var sub = WebhookSubscription.Create(tenantId, eventType, url, secret).Value;

        await using var scope = _fx.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repo.AddAsync(sub, default);
        await uow.SaveChangesAsync(default);
        return sub;
    }

    private async Task EnqueueAsync(Guid tenantId, string eventType, Guid sourceMessageId, object payload)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<WebhookDispatcher>();
        await dispatcher.EnqueueAsync(tenantId, eventType, sourceMessageId, payload, default);
    }

    private async Task RedispatchAsync(Guid deliveryId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<WebhookDispatcher>();
        await dispatcher.RedispatchAsync(deliveryId, default);
    }

    private async Task DeactivateAsync(Guid tenantId, Guid subscriptionId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sub = await repo.GetAsync(tenantId, subscriptionId, default);
        sub!.Deactivate();
        await uow.SaveChangesAsync(default);
    }

    private async Task<List<WebhookDelivery>> ListDeliveriesAsync(Guid tenantId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebhooksDbContext>();
        return await db.Deliveries
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();
    }

    private static string ComputeExpectedSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

[CollectionDefinition("webhooks-db")]
#pragma warning disable CA1711
public sealed class WebhooksDbCollection : ICollectionFixture<WebhooksDbFixture> { }
#pragma warning restore CA1711
