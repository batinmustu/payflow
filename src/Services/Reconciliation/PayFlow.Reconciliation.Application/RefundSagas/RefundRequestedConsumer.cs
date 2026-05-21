using Microsoft.Extensions.Logging;
using PayFlow.EventBus;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Domain.RefundSagas;
using PayFlow.SharedKernel;

namespace PayFlow.Reconciliation.Application.RefundSagas;

/// <summary>
/// Reconciliation's entry point into the refund saga. Consumes
/// <c>payflow.refund.requested.v1</c>, persists the saga in <c>Started</c>
/// (emitting RefundProcessing), bumps to <c>ProviderCalled</c>, then hands
/// the provider-call leg off to <see cref="RefundSagaProcessor"/> — the
/// same processor the recovery worker uses for stuck sagas.
///
/// Idempotency: <c>(tenant_id, refund_id)</c> is unique on the saga table,
/// so a re-delivered Kafka message no-ops cleanly.
/// </summary>
public sealed class RefundRequestedConsumer
    : IIntegrationEventConsumer<RefundRequestedIntegrationEvent>
{
    private readonly IRefundSagaRepository _sagas;
    private readonly IUnitOfWork _uow;
    private readonly RefundSagaProcessor _processor;
    private readonly ILogger<RefundRequestedConsumer> _logger;

    public RefundRequestedConsumer(
        IRefundSagaRepository sagas,
        IUnitOfWork uow,
        RefundSagaProcessor processor,
        ILogger<RefundRequestedConsumer> logger)
    {
        _sagas = sagas;
        _uow = uow;
        _processor = processor;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<RefundRequestedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var payload = envelope.Payload;

        var existing = await _sagas.FindByRefundIdAsync(payload.TenantId, payload.RefundId, ct);
        if (existing is not null)
        {
            _logger.LogInformation(
                "RefundRequested for refund {RefundId} already has a saga in {State}; skipping.",
                payload.RefundId, existing.State);
            return;
        }

        var startResult = RefundSaga.Start(
            tenantId: payload.TenantId,
            refundId: payload.RefundId,
            transactionId: payload.TransactionId,
            amountMinor: payload.AmountMinor,
            currency: payload.Currency,
            providerCode: payload.FinalProviderCode,
            providerReference: payload.FinalProviderReference);

        if (startResult.IsFailure)
        {
            _logger.LogError(
                "Refused to start saga for refund {RefundId}: {Error}",
                payload.RefundId, startResult.ErrorCode);
            return;
        }
        var saga = startResult.Value;
        await _sagas.AddAsync(saga, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
            // SaveChanges #1: Started row in DB + payflow.refund.processing.v1
            // in outbox. Transaction's consumer sees the latter and flips its
            // Refund record into Processing.
        }
        catch (UniqueConstraintViolationException ex) when (
            ex.ConstraintName == "ux_refund_sagas_tenant_refund")
        {
            _logger.LogInformation(
                "Saga for refund {RefundId} already exists (unique violation race); idempotent skip.",
                payload.RefundId);
            return;
        }

        saga.MarkProviderCalled();
        await _uow.SaveChangesAsync(ct);
        // SaveChanges #2: ProviderCalled + AttemptCount++ in DB before the
        // HTTP call. If the process dies mid-flight, RefundSagaRecoveryService
        // picks the row up on its next tick and retries via the same Processor.

        await _processor.ProcessAsync(saga.TenantId, saga.Id, ct);
        // Processor runs the provider call, classifies the result, and does
        // SaveChanges #3 itself.
    }
}
