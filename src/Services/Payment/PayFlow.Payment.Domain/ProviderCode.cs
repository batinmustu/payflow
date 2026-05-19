namespace PayFlow.Payment.Domain;

/// <summary>
/// Stable identifiers for the payment providers PayFlow integrates with.
/// Stored on rows and surfaced in events; never localised, never renamed.
/// </summary>
public static class ProviderCode
{
    public const string Iyzico = "iyzico";
    public const string Stripe = "stripe";
    public const string PayPal = "paypal";
}
