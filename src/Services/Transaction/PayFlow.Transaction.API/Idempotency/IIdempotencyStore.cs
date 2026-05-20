namespace PayFlow.Transaction.API.Idempotency;

/// <summary>
/// Persists in-flight + completed idempotency entries.
///
/// The contract is the one documented in docs/api/idempotency.md: a key is
/// scoped to (tenant, path, key-string), the request body's hash decides
/// whether a retry is the "same" call, and stored responses replay verbatim.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyReservation> TryReserveAsync(
        IdempotencyKey key,
        string bodyHash,
        CancellationToken ct);

    Task CompleteAsync(
        IdempotencyKey key,
        string bodyHash,
        StoredResponse response,
        CancellationToken ct);
}

/// <summary>The fully-qualified Redis key.</summary>
public readonly record struct IdempotencyKey(Guid TenantId, string Path, string ClientKey)
{
    public string Format() => $"idem:{TenantId:N}:{Path}:{ClientKey}";
}

public sealed record StoredResponse(int StatusCode, string ContentType, string Body);

public enum ReservationOutcome
{
    /// <summary>Key was not in the store; we own the slot. Proceed and call CompleteAsync afterwards.</summary>
    Reserved,

    /// <summary>Key exists with an in-progress marker; another request is still running.</summary>
    InFlight,

    /// <summary>Key exists with a completed response and the same body hash; replay the stored response.</summary>
    CompletedSameBody,

    /// <summary>Key exists with a completed response but a different body hash; client mistake.</summary>
    CompletedDifferentBody,
}

public sealed record IdempotencyReservation(ReservationOutcome Outcome, StoredResponse? Replay = null);
