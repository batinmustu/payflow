using Microsoft.Extensions.Logging;
using PayFlow.Notification.Application.Abstractions;

namespace PayFlow.Notification.Infrastructure.Channels;

internal sealed class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;
    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task<DeliveryResult> SendAsync(SmsMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var messageId = $"mock-sms-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "[mock-sms] To={Recipient} MessageId={MessageId} Body={Body}",
            message.Recipient, messageId, message.Body);
        return Task.FromResult(DeliveryResult.Ok(messageId));
    }
}
