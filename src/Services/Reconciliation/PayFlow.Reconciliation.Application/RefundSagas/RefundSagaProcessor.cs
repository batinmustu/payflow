using Microsoft.Extensions.Logging;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Application.RefundSagas;

/// <summary>
/// The provider-call leg of the refund saga, extracted so both the Kafka
/// consumer (first attempt, triggered by RefundRequested) and the recovery
/// worker (subsequent retries against sagas stuck in ProviderCalled) can
/// share it.
///
/// Pre-conditions: <paramref name="sagaId"/> must already be in
/// <see cref="RefundSagaState.ProviderCalled"/> with the attempt counter
/// incremented by the caller. The caller has also already SaveChanged the
/// ProviderCalled transition, so a crash mid-HTTP-call leaves a clear
/// audit row (state=ProviderCalled, attempt_count=N) for the next
/// recovery tick.
/// </summary>
public sealed class RefundSagaProcessor
{
    private readonly IRefundSagaRepository _sagas;
    private readonly IPaymentRefundClient _payments;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RefundSagaProcessor> _logger;

    public RefundSagaProcessor(
        IRefundSagaRepository sagas,
        IPaymentRefundClient payments,
        IUnitOfWork uow,
        ILogger<RefundSagaProcessor> logger)
    {
        _sagas = sagas;
        _payments = payments;
        _uow = uow;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid tenantId, Guid sagaId, CancellationToken ct)
    {
        var saga = await _sagas.GetAsync(tenantId, sagaId, ct);
        if (saga is null)
        {
            _logger.LogWarning("Saga {SagaId} not found; skipping process step.", sagaId);
            return;
        }
        if (saga.State != RefundSagaState.ProviderCalled)
        {
            _logger.LogInformation(
                "Saga {SagaId} is in {State}, not ProviderCalled; skipping process step.",
                sagaId, saga.State);
            return;
        }

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
            else if (string.Equals(response.Status, "ProviderUnavailable", StringComparison.OrdinalIgnoreCase))
            {
                saga.RecordTransientFailure(response.DeclineCode ?? "PROVIDER_UNAVAILABLE");
            }
            else
            {
                // Deterministic decline — terminal, retry would not help.
                saga.MarkFailed(response.DeclineCode ?? response.Status);
            }
        }
        catch (PaymentClientException ex)
        {
            _logger.LogWarning(ex,
                "Payment refund call threw for saga {SagaId} (attempt {Attempt}); transient.",
                saga.Id, saga.AttemptCount);
            saga.RecordTransientFailure("TRANSPORT_ERROR");
        }

        await _uow.SaveChangesAsync(ct);
        // SaveChanges: terminal state + payflow.refund.completed.v1 or
        // payflow.refund.failed.v1 in outbox — or just the updated
        // failure_reason when the saga is still in transient state and
        // a future recovery tick will try again.
    }
}
