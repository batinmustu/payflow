using PayFlow.SharedKernel;

namespace PayFlow.Payment.Domain;

/// <summary>
/// What Transaction (or any internal caller) hands to a provider adapter.
/// The provider-neutral shape — Iyzico, Stripe, and PayPal adapters all
/// translate this into their own wire format inside their own boundary.
/// </summary>
public sealed record PaymentRequest
{
    public Guid TenantId { get; }
    public Guid TransactionId { get; }
    public string ProviderCode { get; }
    public long AmountMinor { get; }
    public string Currency { get; }
    public string CardToken { get; }

    private PaymentRequest(
        Guid tenantId,
        Guid transactionId,
        string providerCode,
        long amountMinor,
        string currency,
        string cardToken)
    {
        TenantId = tenantId;
        TransactionId = transactionId;
        ProviderCode = providerCode;
        AmountMinor = amountMinor;
        Currency = currency;
        CardToken = cardToken;
    }

    public static Result<PaymentRequest> Create(
        Guid tenantId,
        Guid transactionId,
        string providerCode,
        long amountMinor,
        string currency,
        string cardToken)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<PaymentRequest>("TENANT_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return Result.Failure<PaymentRequest>("PROVIDER_CODE_REQUIRED");
        }

        if (amountMinor <= 0)
        {
            return Result.Failure<PaymentRequest>("AMOUNT_INVALID");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return Result.Failure<PaymentRequest>("CURRENCY_NOT_SUPPORTED");
        }

        if (string.IsNullOrWhiteSpace(cardToken))
        {
            return Result.Failure<PaymentRequest>("CARD_TOKEN_INVALID");
        }

        return Result.Success(new PaymentRequest(
            tenantId,
            transactionId,
            providerCode.Trim().ToLowerInvariant(),
            amountMinor,
            currency.Trim().ToUpperInvariant(),
            cardToken));
    }
}
