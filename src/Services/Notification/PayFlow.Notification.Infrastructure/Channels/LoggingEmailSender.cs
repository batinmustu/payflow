using Microsoft.Extensions.Logging;
using PayFlow.Notification.Application.Abstractions;

namespace PayFlow.Notification.Infrastructure.Channels;

/// <summary>
/// Dev / portfolio email "sender" — logs the message at Information level so
/// you can see what would have gone out, and pretends the upstream provider
/// returned a Guid as its message id. The audit row in
/// <c>notification.notifications</c> already has the full body, so this is
/// strictly for tail-the-log demos.
/// </summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task<DeliveryResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var messageId = $"mock-email-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "[mock-email] To={Recipient} Subject={Subject} MessageId={MessageId}\n{Body}",
            message.Recipient, message.Subject, messageId, message.Body);
        return Task.FromResult(DeliveryResult.Ok(messageId));
    }
}
