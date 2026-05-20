using System.Diagnostics;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain;

namespace PayFlow.Payment.Infrastructure.Providers;

/// <summary>
/// Iyzico-style mock adapter. Returns <c>Authorized</c> only — the real
/// Iyzico API uses a two-step auth-then-capture flow as the default, so the
/// adapter never returns Captured from a single call. Capture is a separate
/// endpoint (not yet exposed by this service).
/// </summary>
internal sealed class IyzicoMockProvider : IPaymentProvider
{
    public string Code => ProviderCode.Iyzico;

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(120, ct);
        stopwatch.Stop();

        var providerReference = "iyz_" + Guid.NewGuid().ToString("N")[..14];
        var raw = $"{{\"paymentId\":\"{providerReference}\",\"status\":\"AUTHORIZED\",\"paymentStatus\":\"PRE_AUTH\"}}";

        return new PaymentResult(
            PaymentId: Guid.NewGuid(),
            ProviderCode: Code,
            Status: PaymentStatus.Authorized,
            ProviderReference: providerReference,
            DeclineCode: null,
            LatencyMilliseconds: stopwatch.ElapsedMilliseconds,
            RawResponse: raw);
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(95, ct);
        stopwatch.Stop();

        var providerReference = "iyz_rf_" + Guid.NewGuid().ToString("N")[..12];
        var raw = $"{{\"refundId\":\"{providerReference}\",\"status\":\"SUCCESS\",\"paymentTransactionId\":\"{request.ProviderReference}\"}}";

        return new RefundResult(
            ProviderCode: Code,
            Status: RefundStatus.Refunded,
            ProviderReference: providerReference,
            DeclineCode: null,
            LatencyMilliseconds: stopwatch.ElapsedMilliseconds,
            RawResponse: raw);
    }
}
