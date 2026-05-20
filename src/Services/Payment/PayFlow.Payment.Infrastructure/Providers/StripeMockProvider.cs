using System.Diagnostics;
using System.Globalization;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain;

namespace PayFlow.Payment.Infrastructure.Providers;

/// <summary>
/// Stripe-style mock adapter. Treats the request as a "sale" — a single call
/// that authorises + captures in one step (matches Stripe's PaymentIntents
/// auto-capture default). Always succeeds with <c>Captured</c>; the real
/// Stripe adapter, when implemented, will dispatch on response codes and
/// translate failures the same way the other adapters do.
/// </summary>
internal sealed class StripeMockProvider : IPaymentProvider
{
    public string Code => ProviderCode.Stripe;

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        // Simulated provider latency — mirrors docs/flows/payment-happy-path.md
        // budget ("130ms" line) so the dev experience is realistic.
        await Task.Delay(100, ct);
        stopwatch.Stop();

        var providerReference = "ch_" + Guid.NewGuid().ToString("N")[..16];
        var raw = $"{{\"id\":\"{providerReference}\",\"status\":\"succeeded\",\"amount\":{request.AmountMinor.ToString(CultureInfo.InvariantCulture)}}}";

        return new PaymentResult(
            PaymentId: Guid.NewGuid(),
            ProviderCode: Code,
            Status: PaymentStatus.Captured,
            ProviderReference: providerReference,
            DeclineCode: null,
            LatencyMilliseconds: stopwatch.ElapsedMilliseconds,
            RawResponse: raw);
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(80, ct);
        stopwatch.Stop();

        var providerReference = "rf_" + Guid.NewGuid().ToString("N")[..16];
        var raw = $"{{\"id\":\"{providerReference}\",\"object\":\"refund\",\"status\":\"succeeded\",\"amount\":{request.AmountMinor.ToString(CultureInfo.InvariantCulture)},\"payment_intent\":\"{request.ProviderReference}\"}}";

        return new RefundResult(
            ProviderCode: Code,
            Status: RefundStatus.Refunded,
            ProviderReference: providerReference,
            DeclineCode: null,
            LatencyMilliseconds: stopwatch.ElapsedMilliseconds,
            RawResponse: raw);
    }
}
