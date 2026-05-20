namespace PayFlow.Payment.Domain;

/// <summary>
/// Adapter response for a refund call. Same provider-neutral shape as
/// <see cref="PaymentResult"/>: <see cref="ProviderReference"/> is the
/// provider's refund identifier (e.g. <c>rf_abc</c>) on success, or
/// <c>null</c> if the provider didn't issue one.
/// </summary>
public sealed record RefundResult(
    string ProviderCode,
    RefundStatus Status,
    string? ProviderReference,
    string? DeclineCode,
    long LatencyMilliseconds,
    string? RawResponse);
