using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Domain.Transactions;

public sealed record TransactionInitiatedDomainEvent(
    Guid TransactionId,
    Guid TenantId,
    string OrderReference,
    long AmountMinor,
    string Currency) : DomainEvent;

public sealed record TransactionCapturedDomainEvent(
    Guid TransactionId,
    Guid TenantId,
    string ProviderCode,
    string ProviderReference,
    long AmountMinor,
    string Currency) : DomainEvent;

public sealed record TransactionFailedDomainEvent(
    Guid TransactionId,
    Guid TenantId,
    string FailureReason,
    string? ProviderCodeAttempted) : DomainEvent;
