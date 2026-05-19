namespace PayFlow.Payment.Domain;

/// <summary>
/// What a provider adapter returns. Provider-neutral by construction —
/// raw provider responses are kept opaquely in <see cref="RawResponse"/>
/// for forensics but never typed out into the domain.
/// </summary>
public sealed record PaymentResult(
    Guid PaymentId,
    string ProviderCode,
    PaymentStatus Status,
    string? ProviderReference,
    string? DeclineCode,
    long LatencyMilliseconds,
    string? RawResponse);
