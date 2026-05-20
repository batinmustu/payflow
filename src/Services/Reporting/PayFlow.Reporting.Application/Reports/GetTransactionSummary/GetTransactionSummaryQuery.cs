using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Reporting.Application.Reports.GetTransactionSummary;

public sealed record GetTransactionSummaryQuery(
    Guid TenantId,
    DateOnly FromDate,
    DateOnly ToDate,
    string? Currency)
    : IRequest<Result<GetTransactionSummaryResponse>>;

public sealed record GetTransactionSummaryResponse(
    DateOnly FromDate,
    DateOnly ToDate,
    string? Currency,
    IReadOnlyList<SummaryRow> Rows);

public sealed record SummaryRow(
    DateOnly Date,
    string Currency,
    int AttemptedCount,
    int CapturedCount,
    long CapturedAmountMinor,
    int FailedCount,
    int RefundedCount,
    long RefundedAmountMinor);
