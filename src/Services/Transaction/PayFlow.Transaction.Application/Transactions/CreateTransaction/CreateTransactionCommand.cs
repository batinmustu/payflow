using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Application.Transactions.CreateTransaction;

/// <summary>
/// Merchant-facing create-a-charge command. Transaction picks the provider
/// (the merchant does not — that is one of the things PayFlow exists to do)
/// and dispatches to Payment.
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
    string? FailureReason);
