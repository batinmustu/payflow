using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.EventBus;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Application.RefundSagas;
using PayFlow.Reconciliation.Domain.RefundSagas;
using PayFlow.Reconciliation.Infrastructure.Persistence;

namespace PayFlow.Reconciliation.IntegrationTests;

[Collection("reconciliation-db")]
public class RefundSagaFlowTests
{
    private readonly ReconciliationDbFixture _fx;
    public RefundSagaFlowTests(ReconciliationDbFixture fx)
    {
        _fx = fx;
        // Collection fixture is shared across tests — reset the mock so
        // Calls / Responses don't leak between scenarios.
        _fx.PaymentMock.Reset();
    }

    [Fact]
    public async Task Happy_path_completes_saga_and_writes_outbox_completed_event()
    {
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        _fx.PaymentMock.Responses.Enqueue(new PaymentRefundResponse(
            ProviderCode: "stripe",
            Status: "Refunded",
            ProviderReference: "rf_happy_1",
            DeclineCode: null,
            LatencyMilliseconds: 25));

        await HandleAsync(EnvelopeFor(tenantId, refundId, transactionId));

        var saga = await LoadByRefundAsync(tenantId, refundId);
        saga.Should().NotBeNull();
        saga!.State.Should().Be(RefundSagaState.Completed);
        saga.ProviderRefundReference.Should().Be("rf_happy_1");
        saga.AttemptCount.Should().Be(1);

        var outboxTypes = await OutboxEventTypesAsync(saga.Id);
        outboxTypes.Should().Contain("payflow.refund.processing.v1");
        outboxTypes.Should().Contain("payflow.refund.completed.v1");
    }

    [Fact]
    public async Task Deterministic_decline_lands_terminal_failed_with_decline_code()
    {
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        _fx.PaymentMock.Responses.Enqueue(new PaymentRefundResponse(
            ProviderCode: "paypal",
            Status: "Declined",
            ProviderReference: null,
            DeclineCode: "TRANSACTION_TOO_OLD",
            LatencyMilliseconds: 20));

        await HandleAsync(EnvelopeFor(tenantId, refundId, Guid.NewGuid()));

        var saga = await LoadByRefundAsync(tenantId, refundId);
        saga!.State.Should().Be(RefundSagaState.Failed);
        saga.FailureReason.Should().Be("TRANSACTION_TOO_OLD");
        saga.AttemptCount.Should().Be(1);

        (await OutboxEventTypesAsync(saga.Id)).Should().Contain("payflow.refund.failed.v1");
    }

    [Fact]
    public async Task Transient_provider_unavailable_leaves_saga_in_ProviderCalled_for_retry()
    {
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        _fx.PaymentMock.Responses.Enqueue(new PaymentRefundResponse(
            ProviderCode: "stripe",
            Status: "ProviderUnavailable",
            ProviderReference: null,
            DeclineCode: "GATEWAY_TIMEOUT",
            LatencyMilliseconds: 100));

        await HandleAsync(EnvelopeFor(tenantId, refundId, Guid.NewGuid()));

        var saga = await LoadByRefundAsync(tenantId, refundId);
        saga!.State.Should().Be(RefundSagaState.ProviderCalled);
        saga.FailureReason.Should().Be("GATEWAY_TIMEOUT");
        saga.AttemptCount.Should().Be(1);

        // No terminal event yet — only the RefundProcessing kickoff.
        var outboxTypes = await OutboxEventTypesAsync(saga.Id);
        outboxTypes.Should().Contain("payflow.refund.processing.v1");
        outboxTypes.Should().NotContain("payflow.refund.completed.v1");
        outboxTypes.Should().NotContain("payflow.refund.failed.v1");
    }

    [Fact]
    public async Task Recovery_processor_completes_a_saga_left_in_ProviderCalled()
    {
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        // First attempt: transient → saga stays in ProviderCalled with
        // attempt_count = 1.
        _fx.PaymentMock.Responses.Enqueue(new PaymentRefundResponse(
            "stripe", "ProviderUnavailable", null, "TRANSIENT", 50));
        await HandleAsync(EnvelopeFor(tenantId, refundId, Guid.NewGuid()));

        // Recovery worker simulation: bump attempt + re-run the provider call,
        // this time the mock returns Refunded.
        _fx.PaymentMock.Responses.Enqueue(new PaymentRefundResponse(
            "stripe", "Refunded", "rf_after_recovery", null, 30));

        await SimulateRecoveryTickAsync(tenantId, refundId);

        var saga = await LoadByRefundAsync(tenantId, refundId);
        saga!.State.Should().Be(RefundSagaState.Completed);
        saga.ProviderRefundReference.Should().Be("rf_after_recovery");
        saga.AttemptCount.Should().Be(2);
        _fx.PaymentMock.Calls.Should().HaveCount(2,
            "first attempt was transient, recovery is the second call");
    }

    [Fact]
    public async Task Duplicate_RefundRequested_envelope_is_idempotent_skip()
    {
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var envelope = EnvelopeFor(tenantId, refundId, Guid.NewGuid());

        _fx.PaymentMock.Default = new PaymentRefundResponse(
            "stripe", "Refunded", "rf_dedup", null, 10);

        await HandleAsync(envelope);
        await HandleAsync(envelope);  // second delivery of the same message

        var rows = await ListByRefundAsync(tenantId, refundId);
        rows.Should().HaveCount(1, "the second handle should hit the existence check + idempotent skip");
        _fx.PaymentMock.Calls.Should().HaveCount(1, "no second provider call either");
    }

    [Fact]
    public async Task ListStuckProviderCalledAsync_returns_aged_pending_sagas_only()
    {
        // Land one saga in ProviderCalled by way of a transient first call.
        var tenantId = Guid.NewGuid();
        var oldRefund = Guid.NewGuid();
        _fx.PaymentMock.Responses.Enqueue(new PaymentRefundResponse(
            "stripe", "ProviderUnavailable", null, "TRANSIENT", 50));
        await HandleAsync(EnvelopeFor(tenantId, oldRefund, Guid.NewGuid()));

        // Backdate it so the grace window stops shielding it.
        var nowFrozen = DateTimeOffset.UtcNow;
        await using (var scope = _fx.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();
            var row = await db.RefundSagas.FirstAsync(r => r.RefundId == oldRefund);
            await db.RefundSagas
                .Where(r => r.Id == row.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    x => x.StartedAt,
                    _ => nowFrozen.AddMinutes(-2)));
        }

        // Land a second saga ProviderCalled with StartedAt = now — should be
        // shielded by the grace window.
        var freshRefund = Guid.NewGuid();
        _fx.PaymentMock.Responses.Enqueue(new PaymentRefundResponse(
            "stripe", "ProviderUnavailable", null, "TRANSIENT", 50));
        await HandleAsync(EnvelopeFor(tenantId, freshRefund, Guid.NewGuid()));

        await using var queryScope = _fx.Services.CreateAsyncScope();
        var sagas = queryScope.ServiceProvider.GetRequiredService<IRefundSagaRepository>();
        var stuck = await sagas.ListStuckProviderCalledAsync(
            staleSince: nowFrozen.AddSeconds(-30),
            maxAttempts: RefundSaga.MaxAttempts,
            take: 10,
            ct: CancellationToken.None);

        stuck.Should().Contain(s => s.RefundId == oldRefund);
        stuck.Should().NotContain(s => s.RefundId == freshRefund,
            "fresh saga is inside the grace window and shouldn't be picked up yet");
    }

    // ---- helpers --------------------------------------------------------

    private async Task HandleAsync(IntegrationEventEnvelope<RefundRequestedIntegrationEvent> envelope)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        // Build the consumer from DI so the unique-violation translation +
        // unit-of-work + processor wiring matches the production path.
        var sagas = scope.ServiceProvider.GetRequiredService<IRefundSagaRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var processor = scope.ServiceProvider.GetRequiredService<RefundSagaProcessor>();
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILogger<RefundRequestedConsumer>>();
        var consumer = new RefundRequestedConsumer(sagas, uow, processor, logger);
        await consumer.HandleAsync(envelope, CancellationToken.None);
    }

    private async Task SimulateRecoveryTickAsync(Guid tenantId, Guid refundId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var sagas = scope.ServiceProvider.GetRequiredService<IRefundSagaRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var processor = scope.ServiceProvider.GetRequiredService<RefundSagaProcessor>();

        var saga = (await sagas.FindByRefundIdAsync(tenantId, refundId, default))!;
        saga.MarkProviderCalled();
        await uow.SaveChangesAsync(default);
        await processor.ProcessAsync(saga.TenantId, saga.Id, default);
    }

    private async Task<RefundSaga?> LoadByRefundAsync(Guid tenantId, Guid refundId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var sagas = scope.ServiceProvider.GetRequiredService<IRefundSagaRepository>();
        return await sagas.FindByRefundIdAsync(tenantId, refundId, default);
    }

    private async Task<List<RefundSaga>> ListByRefundAsync(Guid tenantId, Guid refundId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();
        return await db.RefundSagas
            .Where(s => s.TenantId == tenantId && s.RefundId == refundId)
            .ToListAsync();
    }

    private async Task<List<string>> OutboxEventTypesAsync(Guid sagaId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();
        return await db.OutboxMessages
            .Where(m => m.AggregateId == sagaId)
            .Select(m => m.EventType)
            .ToListAsync();
    }

    private static IntegrationEventEnvelope<RefundRequestedIntegrationEvent> EnvelopeFor(
        Guid tenantId, Guid refundId, Guid transactionId)
    {
        var payload = new RefundRequestedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = tenantId,
            RefundId = refundId,
            TransactionId = transactionId,
            AmountMinor = 5000,
            Currency = "TRY",
            RequestedBy = Guid.NewGuid().ToString(),
            FinalProviderCode = "stripe",
            FinalProviderReference = "ch_test_origin",
        };
        return new IntegrationEventEnvelope<RefundRequestedIntegrationEvent>(
            EventType: "payflow.refund.requested.v1",
            MessageId: Guid.NewGuid(),
            TenantId: tenantId,
            AggregateId: refundId,
            AggregateType: "Refund",
            CreatedAt: DateTimeOffset.UtcNow,
            Payload: payload,
            Headers: new Dictionary<string, string>());
    }
}

[CollectionDefinition("reconciliation-db")]
#pragma warning disable CA1711
public sealed class ReconciliationDbCollection : ICollectionFixture<ReconciliationDbFixture> { }
#pragma warning restore CA1711
