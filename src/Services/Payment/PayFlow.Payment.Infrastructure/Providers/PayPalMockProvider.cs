using System.Diagnostics;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain;

namespace PayFlow.Payment.Infrastructure.Providers;

/// <summary>
/// PayPal-style mock adapter. Demonstrates non-trivial behaviour so the
/// abstraction is doing real work — a deterministic "test card" gate
/// based on the amount's last two digits:
///   - ends in 13: SoftDeclined (provider says "we won't, try another route")
///   - ends in 99: HardDeclined (provider says "stop trying")
///   - otherwise: Captured
/// Real PayPal returns its own response codes; this lets dev / Postman
/// demos exercise every PaymentStatus branch without an external service.
/// </summary>
internal sealed class PayPalMockProvider : IPaymentProvider
{
    public string Code => ProviderCode.PayPal;

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(140, ct);
        stopwatch.Stop();

        var lastTwo = (int)(request.AmountMinor % 100);
        var (status, declineCode) = lastTwo switch
        {
            13 => (PaymentStatus.SoftDeclined, "INSUFFICIENT_FUNDS"),
            99 => (PaymentStatus.HardDeclined, "DO_NOT_HONOR"),
            _ => (PaymentStatus.Captured, (string?)null),
        };

        var providerReference = status == PaymentStatus.Captured
            ? "PAY-" + Guid.NewGuid().ToString("N")[..16]
            : null;
        var raw = $"{{\"intent\":\"CAPTURE\",\"status\":\"{status}\",\"amount\":{request.AmountMinor}}}";

        return new PaymentResult(
            PaymentId: Guid.NewGuid(),
            ProviderCode: Code,
            Status: status,
            ProviderReference: providerReference,
            DeclineCode: declineCode,
            LatencyMilliseconds: stopwatch.ElapsedMilliseconds,
            RawResponse: raw);
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(110, ct);
        stopwatch.Stop();

        // Same "test card" gate as ChargeAsync — last two digits of the
        // refund amount decide the branch. Demos exercise the saga's
        // Declined path without an external service.
        var lastTwo = (int)(request.AmountMinor % 100);
        var (status, declineCode) = lastTwo switch
        {
            13 => (RefundStatus.Declined, "TRANSACTION_TOO_OLD"),
            _ => (RefundStatus.Refunded, (string?)null),
        };

        var providerReference = status == RefundStatus.Refunded
            ? "RFND-" + Guid.NewGuid().ToString("N")[..16]
            : null;
        var raw = $"{{\"id\":\"{providerReference}\",\"status\":\"{status}\",\"amount\":{request.AmountMinor},\"capture_id\":\"{request.ProviderReference}\"}}";

        return new RefundResult(
            ProviderCode: Code,
            Status: status,
            ProviderReference: providerReference,
            DeclineCode: declineCode,
            LatencyMilliseconds: stopwatch.ElapsedMilliseconds,
            RawResponse: raw);
    }
}
