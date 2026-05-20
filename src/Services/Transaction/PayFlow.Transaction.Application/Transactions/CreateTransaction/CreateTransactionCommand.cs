using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Application.Transactions.CreateTransaction;

/// <summary>
/// Merchant-facing create-a-charge command. Transaction picks the provider
/// order (per the tenant's routing rule), dispatches to Payment, and falls
/// over to the next provider on a soft decline.
/// </summary>
public sealed record CreateTransactionCommand(
    Guid TenantId,
    string OrderReference,
    long AmountMinor,
    string Currency,
    string CardToken)
    : IRequest<Result<CreateTransactionResponse>>;

public sealed record CreateTransactionResponse(
    Guid TransactionId,
    string Status,                  // "Captured" / "Failed"
    string? ProviderCode,
    string? ProviderReference,
    string? FailureReason,
    IReadOnlyList<ProviderAttempt> Attempts);

public sealed record ProviderAttempt(
    string ProviderCode,
    string Result,                  // "Captured" / "Authorized" / "SoftDeclined" / "HardDeclined" / "ProviderUnavailable" / "TransportError"
    string? DeclineCode,
    long LatencyMilliseconds);
