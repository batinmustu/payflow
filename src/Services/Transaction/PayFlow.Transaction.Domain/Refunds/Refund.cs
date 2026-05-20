using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Domain.Refunds;

/// <summary>
/// Separate aggregate from <see cref="Transactions.Transaction"/> — per
/// docs/database/erd-transaction.md "The aggregate boundary". A refund
/// references its parent transaction by id; refunds are not modelled as a
/// collection on the transaction row. Independent lifetime, independent
/// concurrency.
/// </summary>
public sealed class Refund : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid TransactionId { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; }
    public RefundState State { get; private set; }
    public string RequestedBy { get; private set; }
    public string? FailureReason { get; private set; }
    public string FinalProviderCode { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }

    private Refund(
        Guid id,
        Guid tenantId,
        Guid transactionId,
        long amountMinor,
        string currency,
        string requestedBy,
        string finalProviderCode,
        RefundState state,
        DateTimeOffset requestedAt)
    {
        Id = id;
        TenantId = tenantId;
        TransactionId = transactionId;
        AmountMinor = amountMinor;
        Currency = currency;
        RequestedBy = requestedBy;
        FinalProviderCode = finalProviderCode;
        State = state;
        RequestedAt = requestedAt;
    }

    public static Result<Refund> Request(
        Guid tenantId,
        Guid transactionId,
        long amountMinor,
        string currency,
        string requestedBy,
        string finalProviderCode)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Refund>("TENANT_REQUIRED");
        }
        if (transactionId == Guid.Empty)
        {
            return Result.Failure<Refund>("TRANSACTION_REQUIRED");
        }
        if (amountMinor <= 0)
        {
            return Result.Failure<Refund>("REFUND_AMOUNT_INVALID");
        }
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return Result.Failure<Refund>("CURRENCY_NOT_SUPPORTED");
        }
        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            return Result.Failure<Refund>("REQUESTED_BY_REQUIRED");
        }
        if (string.IsNullOrWhiteSpace(finalProviderCode))
        {
            return Result.Failure<Refund>("PROVIDER_REQUIRED");
        }

        var refund = new Refund(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            transactionId: transactionId,
            amountMinor: amountMinor,
            currency: currency.Trim().ToUpperInvariant(),
            requestedBy: requestedBy.Trim(),
            finalProviderCode: finalProviderCode.Trim().ToLowerInvariant(),
            state: RefundState.Requested,
            requestedAt: DateTimeOffset.UtcNow);

        refund.Raise(new RefundRequestedDomainEvent(
            RefundId: refund.Id,
            TransactionId: transactionId,
            TenantId: tenantId,
            AmountMinor: refund.AmountMinor,
            Currency: refund.Currency,
            RequestedBy: refund.RequestedBy,
            FinalProviderCode: refund.FinalProviderCode));

        return Result.Success(refund);
    }

    /// <summary>Triggered when Reconciliation's saga consumes RefundRequested.</summary>
    public void MarkProcessing()
    {
        EnsureState(RefundState.Requested);
        State = RefundState.Processing;
    }

    public void MarkCompleted(string providerReference)
    {
        EnsureState(RefundState.Requested, RefundState.Processing);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);

        State = RefundState.Completed;
        CompletedAt = DateTimeOffset.UtcNow;

        Raise(new RefundCompletedDomainEvent(
            RefundId: Id,
            TransactionId: TransactionId,
            TenantId: TenantId,
            AmountMinor: AmountMinor,
            Currency: Currency,
            ProviderCode: FinalProviderCode,
            ProviderReference: providerReference));
    }

    public void MarkFailed(string failureReason)
    {
        EnsureState(RefundState.Requested, RefundState.Processing);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        State = RefundState.Failed;
        FailureReason = failureReason;
        FailedAt = DateTimeOffset.UtcNow;

        Raise(new RefundFailedDomainEvent(
            RefundId: Id,
            TransactionId: TransactionId,
            TenantId: TenantId,
            FailureReason: failureReason));
    }

    private void EnsureState(params RefundState[] expected)
    {
        if (!expected.Contains(State))
        {
            throw new InvalidOperationException(
                $"Refund {Id} is in state {State}; expected one of [{string.Join(",", expected)}].");
        }
    }
}
