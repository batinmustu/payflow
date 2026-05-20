using Microsoft.Extensions.Logging;
using PayFlow.EventBus.Kafka.Consuming;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Application.RefundSagas;

/// <summary>
/// Reconciliation's entry point into the refund saga. Consumes
/// <c>payflow.refund.requested.v1</c> and walks the choreography:
///   1. start the saga (DB insert + RefundProcessing in outbox, one SaveChanges)
///   2. call Payment's refund endpoint
///   3. mark the saga Completed or Failed (DB update + RefundCompleted/Failed in outbox)
///
/// Idempotency: <c>(tenant_id, refund_id)</c> is unique on the saga table, and
/// we check before inserting so a re-delivered Kafka message no-ops cleanly.
/// </summary>
public sealed class RefundRequestedConsumer
    : IIntegrationEventConsumer<RefundRequestedIntegrationEvent>
{
    private readonly IRefundSagaRepository _sagas;
    private readonly IPaymentRefundClient _payments;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RefundRequestedConsumer> _logger;

    public RefundRequestedConsumer(
        IRefundSagaRepository sagas,
        IPaymentRefundClient payments,
        IUnitOfWork uow,
        ILogger<RefundRequestedConsumer> logger)
    {
        _sagas = sagas;
        _payments = payments;
        _uow = uow;
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
        await _uow.SaveChangesAsync(ct);
        // SaveChanges #1: Started row in DB + payflow.refund.processing.v1
        // in outbox. Transaction's consumer (M4.E) will see the latter and
        // flip its Refund record into Processing.

        saga.MarkProviderCalled();

        try
        {
            var response = await _payments.RefundAsync(
                new PaymentRefundRequest(
                    TenantId: saga.TenantId,
                    TransactionId: saga.TransactionId,
                    ProviderCode: saga.ProviderCode,
                    ProviderReference: saga.ProviderReference,
                    AmountMinor: saga.AmountMinor,
                    Currency: saga.Currency,
                    IdempotencyKey: saga.Id.ToString("N")),
                ct);

            if (string.Equals(response.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
            {
                saga.MarkCompleted(response.ProviderReference ?? "unknown");
            }
            else
            {
                // Declined or ProviderUnavailable — both terminal in v1.
                // ProviderUnavailable will become "stay in ProviderCalled,
                // retry with backoff" when the saga grows a retry budget.
                saga.MarkFailed(response.DeclineCode ?? response.Status);
            }
        }
        catch (PaymentClientException ex)
        {
            _logger.LogError(ex,
                "Payment refund call threw for saga {SagaId}; marking failed.",
                saga.Id);
            saga.MarkFailed("TRANSPORT_ERROR");
        }

        await _uow.SaveChangesAsync(ct);
        // SaveChanges #2: terminal state + payflow.refund.completed.v1 or
        // payflow.refund.failed.v1 in outbox.
    }
}
