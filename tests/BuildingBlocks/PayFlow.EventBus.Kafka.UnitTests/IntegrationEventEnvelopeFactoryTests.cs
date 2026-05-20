using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace PayFlow.EventBus.Kafka.UnitTests;

public class IntegrationEventEnvelopeFactoryTests
{
    public sealed record SamplePayload(Guid RefundId, Guid TenantId, long AmountMinor, string Currency);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string EventType = "payflow.refund.requested.v1";
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MessageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AggregateId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExtractHeaders_decodes_utf8_byte_values()
    {
        var headers = new Headers
        {
            { "event_type", Encoding.UTF8.GetBytes(EventType) },
            { "schema_version", Encoding.UTF8.GetBytes("v1") },
        };

        var result = IntegrationEventEnvelopeFactory.ExtractHeaders(headers);

        result.Should().ContainKey("event_type").WhoseValue.Should().Be(EventType);
        result.Should().ContainKey("schema_version").WhoseValue.Should().Be("v1");
    }

    [Fact]
    public void Build_deserialises_payload_and_pulls_metadata_from_headers()
    {
        var payload = new SamplePayload(AggregateId, TenantId, 5000, "TRY");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var headers = HeadersWith();

        var envelope = (IntegrationEventEnvelope<SamplePayload>)
            IntegrationEventEnvelopeFactory.Build(typeof(SamplePayload), bytes, headers);

        envelope.EventType.Should().Be(EventType);
        envelope.MessageId.Should().Be(MessageId);
        envelope.TenantId.Should().Be(TenantId);
        envelope.AggregateId.Should().Be(AggregateId);
        envelope.AggregateType.Should().Be("Refund");
        envelope.CreatedAt.Should().Be(CreatedAt);
        envelope.Payload.RefundId.Should().Be(AggregateId);
        envelope.Payload.AmountMinor.Should().Be(5000);
        envelope.Headers.Should().ContainKey("schema_version");
    }

    [Fact]
    public void Build_throws_when_a_required_header_is_missing()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SamplePayload(AggregateId, TenantId, 1, "TRY"));
        var headers = HeadersWith();
        var stripped = headers.Where(kv => kv.Key != "tenant_id")
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var act = () => IntegrationEventEnvelopeFactory.Build(typeof(SamplePayload), bytes, stripped);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tenant_id*");
    }

    [Fact]
    public void Build_throws_when_header_guid_is_malformed()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SamplePayload(AggregateId, TenantId, 1, "TRY"));
        var headers = HeadersWith();
        headers["message_id"] = "not-a-guid";

        var act = () => IntegrationEventEnvelopeFactory.Build(typeof(SamplePayload), bytes, headers);

        act.Should().Throw<InvalidOperationException>().WithMessage("*message_id*");
    }

    [Fact]
    public void Build_throws_when_payload_is_not_valid_json()
    {
        var headers = HeadersWith();
        var act = () => IntegrationEventEnvelopeFactory.Build(
            typeof(SamplePayload), Encoding.UTF8.GetBytes("{not json}"), headers);

        act.Should().Throw<JsonException>();
    }

    private static Dictionary<string, string> HeadersWith() => new(StringComparer.Ordinal)
    {
        ["event_type"] = EventType,
        ["message_id"] = MessageId.ToString(),
        ["tenant_id"] = TenantId.ToString(),
        ["aggregate_id"] = AggregateId.ToString(),
        ["aggregate_type"] = "Refund",
        ["created_at"] = CreatedAt.ToString("O"),
        ["schema_version"] = "v1",
    };
}
