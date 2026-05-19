using PayFlow.Payment.Domain;

namespace PayFlow.Payment.Application.Abstractions;

/// <summary>
/// The Strategy + Adapter abstraction for a single payment provider. Each
/// concrete implementation (Iyzico, Stripe, PayPal in this codebase) lives
/// inside its own anti-corruption layer in Infrastructure — translating
/// PayFlow's neutral <see cref="PaymentRequest"/> into the provider's wire
/// format on the way in, and the provider's response into a
/// <see cref="PaymentResult"/> on the way out.
///
/// Provider-specific concepts never leak past this boundary.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// The stable provider identifier this adapter handles
    /// (e.g. <c>ProviderCode.Stripe</c>).
    /// </summary>
    string Code { get; }

    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct);
}
