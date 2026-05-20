using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Application.Refunds.RequestRefund;

/// <summary>
/// Starts the refund saga. Transaction creates the refund row + outbox
/// event; Reconciliation picks it up downstream and walks the choreography
/// in docs/flows/refund-saga.md.
/// </summary>
public sealed record RequestRefundCommand(
    Guid TenantId,
    Guid TransactionId,
    long AmountMinor,
    string RequestedBy)
    : IRequest<Result<RequestRefundResponse>>;

public sealed record RequestRefundResponse(
    Guid RefundId,
    Guid TransactionId,
    string Status,
    long AmountMinor,
    string Currency);
