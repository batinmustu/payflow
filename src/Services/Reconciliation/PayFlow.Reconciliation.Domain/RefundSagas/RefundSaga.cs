using PayFlow.SharedKernel;

namespace PayFlow.Reconciliation.Domain.RefundSagas;

/// <summary>
/// Saga record for a single refund choreography (see docs/flows/refund-saga.md).
/// One row per consumed <c>payflow.refund.requested.v1</c>; uniqueness on
/// <c>(tenant_id, refund_id)</c> guards against re-delivery of the same
/// Kafka message double-walking the saga.
/// </summary>
public sealed class RefundSaga : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid RefundId { get; private set; }
    public Guid TransactionId { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string ProviderCode { get; private set; } = string.Empty;
    public string ProviderReference { get; private set; } = string.Empty;
    public RefundSagaState State { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ProviderRefundReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }

    private RefundSaga() { }

    public static Result<RefundSaga> Start(
        Guid tenantId,
        Guid refundId,
        Guid transactionId,
        long amountMinor,
        string currency,
        string providerCode,
        string providerReference)
    {
        if (tenantId == Guid.Empty) return Result.Failure<RefundSaga>("TENANT_REQUIRED");
        if (refundId == Guid.Empty) return Result.Failure<RefundSaga>("REFUND_REQUIRED");
        if (transactionId == Guid.Empty) return Result.Failure<RefundSaga>("TRANSACTION_REQUIRED");
        if (amountMinor <= 0) return Result.Failure<RefundSaga>("AMOUNT_INVALID");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return Result.Failure<RefundSaga>("CURRENCY_NOT_SUPPORTED");
        }
        if (string.IsNullOrWhiteSpace(providerCode)) return Result.Failure<RefundSaga>("PROVIDER_REQUIRED");
        if (string.IsNullOrWhiteSpace(providerReference)) return Result.Failure<RefundSaga>("PROVIDER_REFERENCE_REQUIRED");

        var saga = new RefundSaga
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RefundId = refundId,
            TransactionId = transactionId,
            AmountMinor = amountMinor,
            Currency = currency.Trim().ToUpperInvariant(),
            ProviderCode = providerCode.Trim().ToLowerInvariant(),
            ProviderReference = providerReference.Trim(),
            State = RefundSagaState.Started,
            StartedAt = DateTimeOffset.UtcNow,
        };

        saga.Raise(new RefundProcessingDomainEvent(
            SagaId: saga.Id,
            RefundId: refundId,
            TransactionId: transactionId,
            TenantId: tenantId));

        return Result.Success(saga);
    }

    public void MarkProviderCalled()
    {
        EnsureState(RefundSagaState.Started, RefundSagaState.ProviderCalled);
        State = RefundSagaState.ProviderCalled;
        AttemptCount++;
    }

    public void MarkCompleted(string providerRefundReference)
    {
        EnsureState(RefundSagaState.Started, RefundSagaState.ProviderCalled);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerRefundReference);

        State = RefundSagaState.Completed;
        ProviderRefundReference = providerRefundReference;
        CompletedAt = DateTimeOffset.UtcNow;

        Raise(new RefundCompletedDomainEvent(
            SagaId: Id,
            RefundId: RefundId,
            TransactionId: TransactionId,
            TenantId: TenantId,
            AmountMinor: AmountMinor,
            Currency: Currency,
            ProviderCode: ProviderCode,
            ProviderReference: providerRefundReference));
    }

    public void MarkFailed(string failureReason)
    {
        EnsureState(RefundSagaState.Started, RefundSagaState.ProviderCalled);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        State = RefundSagaState.Failed;
        FailureReason = failureReason;
        FailedAt = DateTimeOffset.UtcNow;

        Raise(new RefundFailedDomainEvent(
            SagaId: Id,
            RefundId: RefundId,
            TransactionId: TransactionId,
            TenantId: TenantId,
            FailureReason: failureReason));
    }

    /// <summary>
    /// Max number of provider calls we'll make before giving up — including
    /// the first attempt. Aligns with docs/flows/refund-saga.md retry
    /// budget table.
    /// </summary>
    public const int MaxAttempts = 6;

    /// <summary>
    /// Records a transient provider failure (timeout, 5xx, ProviderUnavailable).
    /// Keeps the saga in <c>ProviderCalled</c> so a recovery worker can try
    /// again, until the attempt budget is exhausted — at which point the saga
    /// becomes terminal Failed and emits the integration event.
    /// </summary>
    public void RecordTransientFailure(string failureReason)
    {
        EnsureState(RefundSagaState.ProviderCalled);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        FailureReason = failureReason;

        if (AttemptCount >= MaxAttempts)
        {
            // Budget exhausted — promote to terminal Failed.
            State = RefundSagaState.Failed;
            FailedAt = DateTimeOffset.UtcNow;

            Raise(new RefundFailedDomainEvent(
                SagaId: Id,
                RefundId: RefundId,
                TransactionId: TransactionId,
                TenantId: TenantId,
                FailureReason: failureReason));
        }
        // Otherwise stay in ProviderCalled with attempt_count incremented by
        // MarkProviderCalled before the call; a recovery worker (or the next
        // Kafka redelivery) picks the saga up and tries the provider again.
    }

    private void EnsureState(params RefundSagaState[] expected)
    {
        if (!expected.Contains(State))
        {
            throw new InvalidOperationException(
                $"RefundSaga {Id} is in state {State}; expected one of [{string.Join(",", expected)}].");
        }
    }
}
