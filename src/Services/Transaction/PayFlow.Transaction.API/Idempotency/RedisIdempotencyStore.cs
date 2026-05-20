using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace PayFlow.Transaction.API.Idempotency;

internal sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _ttl;

    public RedisIdempotencyStore(IConnectionMultiplexer redis, IOptions<IdempotencyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);
        _redis = redis;
        _ttl = TimeSpan.FromHours(options.Value.TtlHours);
    }

    public async Task<IdempotencyReservation> TryReserveAsync(
        IdempotencyKey key,
        string bodyHash,
        CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var redisKey = key.Format();

        // Atomic reserve: SET NX EX. If we win, the slot is ours.
        var reservation = new StoredEntry("in-flight", bodyHash, StatusCode: null, ContentType: null, Body: null);
        var reservedOk = await db.StringSetAsync(
            redisKey,
            JsonSerializer.Serialize(reservation, Json),
            expiry: _ttl,
            when: When.NotExists);

        if (reservedOk)
        {
            return new IdempotencyReservation(ReservationOutcome.Reserved);
        }

        // Someone else got there first — read the existing record and dispatch.
        var existingRaw = await db.StringGetAsync(redisKey);
        if (!existingRaw.HasValue)
        {
            // Vanished between SetNX and Get (TTL?) — race-safe retry: try one more SetNX.
            reservedOk = await db.StringSetAsync(
                redisKey,
                JsonSerializer.Serialize(reservation, Json),
                expiry: _ttl,
                when: When.NotExists);
            return reservedOk
                ? new IdempotencyReservation(ReservationOutcome.Reserved)
                : new IdempotencyReservation(ReservationOutcome.InFlight);
        }

        var existing = JsonSerializer.Deserialize<StoredEntry>(existingRaw!, Json)
            ?? throw new InvalidOperationException("Idempotency store returned a malformed entry.");

        if (existing.State == "in-flight")
        {
            return new IdempotencyReservation(ReservationOutcome.InFlight);
        }

        if (existing.BodyHash != bodyHash)
        {
            return new IdempotencyReservation(ReservationOutcome.CompletedDifferentBody);
        }

        return new IdempotencyReservation(
            ReservationOutcome.CompletedSameBody,
            new StoredResponse(
                StatusCode: existing.StatusCode!.Value,
                ContentType: existing.ContentType!,
                Body: existing.Body!));
    }

    public async Task CompleteAsync(
        IdempotencyKey key,
        string bodyHash,
        StoredResponse response,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        var db = _redis.GetDatabase();
        var entry = new StoredEntry(
            "complete",
            bodyHash,
            StatusCode: response.StatusCode,
            ContentType: response.ContentType,
            Body: response.Body);

        // Overwrite the in-flight marker, refresh the 24h TTL from completion.
        await db.StringSetAsync(
            key.Format(),
            JsonSerializer.Serialize(entry, Json),
            expiry: _ttl,
            when: When.Always);
    }

    private sealed record StoredEntry(
        string State,
        string BodyHash,
        int? StatusCode,
        string? ContentType,
        string? Body);
}
