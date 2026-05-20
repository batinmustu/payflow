using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Domain.Transactions;

/// <summary>
/// The aggregate of record for a payment attempt. Owns the state machine and
/// the link to the underlying Payment row (final_provider_code +
/// provider_reference). Refunds, partial captures, and richer transitions
/// land in subsequent milestones.
/// </summary>
public sealed class Transaction : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public string OrderReference { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; }
    public TransactionState State { get; private set; }
    public string? FinalProviderCode { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }

    /// <summary>
    /// Cumulative refunded amount (in minor units). Denormalised — the
    /// authoritative ledger is the <c>refunds</c> table; this column lets
    /// the <c>Captured → PartiallyRefunded → Refunded</c> decision be a
    /// single-row read. See docs/database/erd-transaction.md.
    /// </summary>
    public long RefundedAmountMinor { get; private set; }

    private Transaction(
        Guid id,
        Guid tenantId,
        string orderReference,
        long amountMinor,
        string currency,
        TransactionState state,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        OrderReference = orderReference;
        AmountMinor = amountMinor;
        Currency = currency;
        State = state;
        CreatedAt = createdAt;
    }

    public static Result<Transaction> Initiate(
        Guid tenantId,
        string orderReference,
        long amountMinor,
        string currency)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Transaction>("TENANT_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(orderReference))
        {
            return Result.Failure<Transaction>("ORDER_REFERENCE_REQUIRED");
        }

        if (amountMinor <= 0)
        {
            return Result.Failure<Transaction>("AMOUNT_INVALID");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return Result.Failure<Transaction>("CURRENCY_NOT_SUPPORTED");
        }

        var trimmedRef = orderReference.Trim();
        var normalisedCurrency = currency.Trim().ToUpperInvariant();

        var tx = new Transaction(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            orderReference: trimmedRef,
            amountMinor: amountMinor,
            currency: normalisedCurrency,
            state: TransactionState.Initiated,
            createdAt: DateTimeOffset.UtcNow);

        tx.Raise(new TransactionInitiatedDomainEvent(
            TransactionId: tx.Id,
            TenantId: tenantId,
            OrderReference: trimmedRef,
            AmountMinor: amountMinor,
            Currency: normalisedCurrency));

        return Result.Success(tx);
    }

    public void MarkCaptured(string providerCode, string providerReference)
    {
        EnsureState(TransactionState.Initiated);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);

        State = TransactionState.Captured;
        FinalProviderCode = providerCode;
        ProviderReference = providerReference;
        CapturedAt = DateTimeOffset.UtcNow;

        Raise(new TransactionCapturedDomainEvent(
            TransactionId: Id,
            TenantId: TenantId,
            ProviderCode: providerCode,
            ProviderReference: providerReference,
            AmountMinor: AmountMinor,
            Currency: Currency));
    }

    public void MarkFailed(string failureReason, string? providerCodeAttempted = null)
    {
        EnsureState(TransactionState.Initiated);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        State = TransactionState.Failed;
        FailureReason = failureReason;
        FinalProviderCode = providerCodeAttempted;
        FailedAt = DateTimeOffset.UtcNow;

        Raise(new TransactionFailedDomainEvent(
            TransactionId: Id,
            TenantId: TenantId,
            FailureReason: failureReason,
            ProviderCodeAttempted: providerCodeAttempted));
    }

    /// <summary>
    /// Reaction to <c>payflow.refund.completed.v1</c>: bumps the cumulative
    /// refund amount and shifts the state machine. Captured → PartiallyRefunded
    /// when there is still capturable balance left; → Refunded when the full
    /// amount has been refunded. Pure state change — TX does not re-emit.
    /// </summary>
    public void ApplyRefundCompleted(long refundAmountMinor)
    {
        if (refundAmountMinor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refundAmountMinor),
                "Refund amount must be positive.");
        }

        if (State is not (TransactionState.Captured or TransactionState.PartiallyRefunded))
        {
            throw new InvalidOperationException(
                $"Transaction {Id} is in state {State}; refunds may only be applied to Captured or PartiallyRefunded.");
        }

        var newTotal = RefundedAmountMinor + refundAmountMinor;
        if (newTotal > AmountMinor)
        {
            throw new InvalidOperationException(
                $"Refund total ({newTotal}) would exceed captured amount ({AmountMinor}).");
        }

        RefundedAmountMinor = newTotal;
        State = newTotal == AmountMinor
            ? TransactionState.Refunded
            : TransactionState.PartiallyRefunded;
    }

    private void EnsureState(TransactionState expected)
    {
        if (State != expected)
        {
            throw new InvalidOperationException(
                $"Transaction {Id} is in state {State}; expected {expected}.");
        }
    }
}
