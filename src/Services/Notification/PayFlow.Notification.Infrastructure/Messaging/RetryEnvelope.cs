using System.Text;
using System.Text.Json;

namespace PayFlow.Notification.Infrastructure.Messaging;

/// <summary>
/// Wire format for a retry message. JSON, persistent. Kept tiny on purpose —
/// the notification row already holds recipient/subject/body, so the consumer
/// just re-loads the record by id and re-attempts.
/// </summary>
internal sealed record RetryEnvelope(Guid NotificationId, int AttemptNumber)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public byte[] ToBytes() =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, JsonOptions));

    public static RetryEnvelope? TryParse(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<RetryEnvelope>(body.Span, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
