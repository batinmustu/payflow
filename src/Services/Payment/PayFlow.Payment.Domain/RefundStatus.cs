namespace PayFlow.Payment.Domain;

/// <summary>
/// Outcome of a provider refund call, classified by the adapter.
/// <list type="bullet">
///   <item><c>Refunded</c> — provider accepted the refund (terminal success).</item>
///   <item><c>Declined</c> — provider refused (e.g. transaction too old, already refunded). Saga moves to Failed without retry.</item>
///   <item><c>ProviderUnavailable</c> — transient transport/5xx. Saga retries with backoff.</item>
/// </list>
/// </summary>
public enum RefundStatus
{
    Refunded,
    Declined,
    ProviderUnavailable,
}
