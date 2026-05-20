using PayFlow.SharedKernel;

namespace PayFlow.Payment.Domain;

/// <summary>
/// Provider-neutral refund request. Mirrors <see cref="PaymentRequest"/>
/// but for the refund side of a transaction: the caller already knows
/// which provider handled the original charge and gives us its
/// reference so the adapter can target the correct underlying object.
/// </summary>
public sealed record RefundRequest
{
    public Guid TenantId { get; }
    public Guid TransactionId { get; }
    public string ProviderCode { get; }
    public string ProviderReference { get; }
    public long AmountMinor { get; }
    public string Currency { get; }

    /// <summary>
    /// Caller-provided dedup key — for the refund saga this is the saga id.
    /// Adapters echo it back to providers that support it so a re-delivered
    /// SagaStarted does not double-refund.
    /// </summary>
    public string IdempotencyKey { get; }

    private RefundRequest(
        Guid tenantId,
        Guid transactionId,
        string providerCode,
        string providerReference,
        long amountMinor,
        string currency,
        string idempotencyKey)
    {
        TenantId = tenantId;
        TransactionId = transactionId;
        ProviderCode = providerCode;
        ProviderReference = providerReference;
        AmountMinor = amountMinor;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
    }

    public static Result<RefundRequest> Create(
        Guid tenantId,
        Guid transactionId,
        string providerCode,
        string providerReference,
        long amountMinor,
        string currency,
        string idempotencyKey)
    {
        if (tenantId == Guid.Empty) return Result.Failure<RefundRequest>("TENANT_REQUIRED");
        if (transactionId == Guid.Empty) return Result.Failure<RefundRequest>("TRANSACTION_REQUIRED");
        if (string.IsNullOrWhiteSpace(providerCode)) return Result.Failure<RefundRequest>("PROVIDER_CODE_REQUIRED");
        if (string.IsNullOrWhiteSpace(providerReference)) return Result.Failure<RefundRequest>("PROVIDER_REFERENCE_REQUIRED");
        if (amountMinor <= 0) return Result.Failure<RefundRequest>("AMOUNT_INVALID");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return Result.Failure<RefundRequest>("CURRENCY_NOT_SUPPORTED");
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return Result.Failure<RefundRequest>("IDEMPOTENCY_KEY_REQUIRED");

        return Result.Success(new RefundRequest(
            tenantId,
            transactionId,
            providerCode.Trim().ToLowerInvariant(),
            providerReference.Trim(),
            amountMinor,
            currency.Trim().ToUpperInvariant(),
            idempotencyKey.Trim()));
    }
}
