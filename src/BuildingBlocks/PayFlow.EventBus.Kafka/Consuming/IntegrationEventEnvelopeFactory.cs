using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace PayFlow.EventBus.Kafka.Consuming;

/// <summary>
/// Pulls the envelope metadata out of Kafka headers and decodes the payload.
/// Kept separate from the background service so the parsing is unit-testable
/// without spinning up a broker.
///
/// The header contract mirrors <see cref="KafkaOutboxPublisher"/>:
/// <list type="bullet">
///   <item><c>event_type</c>, <c>message_id</c>, <c>tenant_id</c>,
///   <c>aggregate_type</c>, <c>aggregate_id</c>, <c>created_at</c> — required</item>
///   <item>everything else is folded into <see cref="IntegrationEventEnvelope{TPayload}.Headers"/></item>
/// </list>
/// </summary>
public static class IntegrationEventEnvelopeFactory
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyDictionary<string, string> ExtractHeaders(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        // Producer-side BuildHeaders writes some keys (tenant_id, message_id)
        // twice — once natively, once from the headers blob. Last value wins
        // so the natively-encoded form (compact Guid format) stays.
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var h in headers)
        {
            var bytes = h.GetValueBytes();
            result[h.Key] = bytes is null ? string.Empty : Encoding.UTF8.GetString(bytes);
        }
        return result;
    }

    /// <summary>
    /// Build a strongly-typed <see cref="IntegrationEventEnvelope{TPayload}"/>
    /// via reflection — the caller supplies <paramref name="payloadType"/> from
    /// the <see cref="IntegrationEventRegistry"/>, the result is downcast to the
    /// generic envelope by the registration's dispatch lambda.
    /// </summary>
    public static object Build(
        Type payloadType,
        ReadOnlySpan<byte> payloadBytes,
        IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        ArgumentNullException.ThrowIfNull(headers);

        var payload = JsonSerializer.Deserialize(payloadBytes, payloadType, PayloadOptions)
            ?? throw new JsonException($"Payload deserialised to null for type {payloadType.Name}.");

        var eventType = RequireHeader(headers, "event_type");
        var messageId = ParseGuid(RequireHeader(headers, "message_id"), "message_id");
        var tenantId = ParseGuid(RequireHeader(headers, "tenant_id"), "tenant_id");
        var aggregateId = ParseGuid(RequireHeader(headers, "aggregate_id"), "aggregate_id");
        var aggregateType = RequireHeader(headers, "aggregate_type");
        var createdAt = ParseTimestamp(RequireHeader(headers, "created_at"));

        var envelopeType = typeof(IntegrationEventEnvelope<>).MakeGenericType(payloadType);
        return Activator.CreateInstance(envelopeType,
            eventType,
            messageId,
            tenantId,
            aggregateId,
            aggregateType,
            createdAt,
            payload,
            headers)
            ?? throw new InvalidOperationException(
                $"Failed to construct IntegrationEventEnvelope<{payloadType.Name}>.");
    }

    private static string RequireHeader(IReadOnlyDictionary<string, string> headers, string key)
    {
        if (!headers.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required Kafka header '{key}' missing.");
        }
        return value;
    }

    private static Guid ParseGuid(string raw, string headerName)
    {
        if (!Guid.TryParse(raw, out var value))
        {
            throw new InvalidOperationException($"Header '{headerName}' is not a valid Guid: '{raw}'.");
        }
        return value;
    }

    private static DateTimeOffset ParseTimestamp(string raw)
    {
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
        {
            throw new InvalidOperationException($"Header 'created_at' is not a valid timestamp: '{raw}'.");
        }
        return value;
    }
}
