using Microsoft.Extensions.Logging;
using PayFlow.Notification.Application.Abstractions;
using PayFlow.Notification.Domain;
using PayFlow.Notification.Domain.Notifications;

namespace PayFlow.Notification.Application.Notifications;

/// <summary>
/// Shared pipeline every consumer goes through:
/// <list type="number">
///   <item>Dedup on <see cref="NotificationRecord.SourceMessageId"/>.</item>
///   <item>Resolve the recipient via <see cref="ITenantContactResolver"/>.</item>
///   <item>Render the template ahead of time.</item>
///   <item>Insert the row in <c>Pending</c>, save.</item>
///   <item>Hand off to the channel adapter.</item>
///   <item>Mark <c>Sent</c> / <c>Failed</c> with the result, save again.</item>
/// </list>
/// Two SaveChanges so the audit row exists even if the provider call panics.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly INotificationRepository _records;
    private readonly ITenantContactResolver _contacts;
    private readonly ITemplateRenderer _renderer;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository records,
        ITenantContactResolver contacts,
        ITemplateRenderer renderer,
        IEmailSender email,
        ISmsSender sms,
        IUnitOfWork uow,
        ILogger<NotificationDispatcher> logger)
    {
        _records = records;
        _contacts = contacts;
        _renderer = renderer;
        _email = email;
        _sms = sms;
        _uow = uow;
        _logger = logger;
    }

    public async Task DispatchAsync(
        Guid sourceMessageId,
        string sourceEventType,
        Guid tenantId,
        NotificationKind kind,
        NotificationChannel channel,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken ct)
    {
        if (await _records.ExistsForSourceMessageAsync(sourceMessageId, ct))
        {
            _logger.LogInformation(
                "Notification for source {MessageId} already exists; skipping.", sourceMessageId);
            return;
        }

        var contact = await _contacts.ResolveAsync(tenantId, ct);
        var recipient = channel == NotificationChannel.Sms
            ? contact.PhoneE164 ?? string.Empty
            : contact.Email;

        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogWarning(
                "No {Channel} recipient for tenant {TenantId}; skipping {Kind}.",
                channel, tenantId, kind);
            return;
        }

        var rendered = await _renderer.RenderAsync(kind, channel, model, ct);

        var recordResult = NotificationRecord.Create(
            tenantId: tenantId,
            kind: kind,
            channel: channel,
            recipient: recipient,
            subject: rendered.Subject,
            body: rendered.Body,
            sourceMessageId: sourceMessageId,
            sourceEventType: sourceEventType);

        if (recordResult.IsFailure)
        {
            _logger.LogError(
                "Refused to create notification for {EventType} {MessageId}: {Error}",
                sourceEventType, sourceMessageId, recordResult.ErrorCode);
            return;
        }
        var record = recordResult.Value;
        await _records.AddAsync(record, ct);
        await _uow.SaveChangesAsync(ct);

        DeliveryResult result;
        try
        {
            result = channel == NotificationChannel.Sms
                ? await _sms.SendAsync(new SmsMessage(recipient, rendered.Body), ct)
                : await _email.SendAsync(new EmailMessage(recipient, rendered.Subject, rendered.Body), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Channel adapter threw for notification {NotificationId}; marking failed.",
                record.Id);
            result = DeliveryResult.Fail("ADAPTER_THREW");
        }

        if (result.Success)
        {
            record.MarkSent(result.ProviderReference);
        }
        else
        {
            record.MarkFailed(result.FailureReason ?? "UNKNOWN");
        }
        await _uow.SaveChangesAsync(ct);
    }
}
