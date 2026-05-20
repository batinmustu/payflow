using MediatR;
using PayFlow.Reporting.Application.Abstractions;
using PayFlow.SharedKernel;

namespace PayFlow.Reporting.Application.Reports.GetTransactionSummary;

internal sealed class GetTransactionSummaryQueryHandler
    : IRequestHandler<GetTransactionSummaryQuery, Result<GetTransactionSummaryResponse>>
{
    private readonly ISummaryRepository _summaries;

    public GetTransactionSummaryQueryHandler(ISummaryRepository summaries) => _summaries = summaries;

    public async Task<Result<GetTransactionSummaryResponse>> Handle(
        GetTransactionSummaryQuery q, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(q);

        if (q.TenantId == Guid.Empty)
        {
            return Result.Failure<GetTransactionSummaryResponse>("TENANT_REQUIRED");
        }
        if (q.FromDate > q.ToDate)
        {
            return Result.Failure<GetTransactionSummaryResponse>("DATE_RANGE_INVALID");
        }
        if (q.ToDate.DayNumber - q.FromDate.DayNumber > 366)
        {
            // Guardrail — projection scan is unindexed for arbitrary windows.
            return Result.Failure<GetTransactionSummaryResponse>("DATE_RANGE_TOO_WIDE");
        }

        var rows = await _summaries.ListAsync(q.TenantId, q.FromDate, q.ToDate, q.Currency, ct);

        var mapped = rows
            .Select(r => new SummaryRow(
                Date: r.Date,
                Currency: r.Currency,
                AttemptedCount: r.AttemptedCount,
                CapturedCount: r.CapturedCount,
                CapturedAmountMinor: r.CapturedAmountMinor,
                FailedCount: r.FailedCount,
                RefundedCount: r.RefundedCount,
                RefundedAmountMinor: r.RefundedAmountMinor))
            .ToList();

        return Result.Success(new GetTransactionSummaryResponse(
            FromDate: q.FromDate,
            ToDate: q.ToDate,
            Currency: q.Currency,
            Rows: mapped));
    }
}
