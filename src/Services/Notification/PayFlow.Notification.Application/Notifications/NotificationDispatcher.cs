using Microsoft.Extensions.Logging;
using PayFlow.Notification.Application.Abstractions;
using PayFlow.Notification.Domain;
using PayFlow.Notification.Domain.Notifications;

namespace PayFlow.Notification.Application.Notifications;

/// <summary>
/// Shared pipeline for outgoing notifications. Two entry points:
/// <list type="bullet">
///   <item>
///     <see cref="DispatchAsync"/> — invoked by the Kafka consumers on the
///     first attempt. Dedupes on <see cref="NotificationRecord.SourceMessageId"/>,
///     resolves the recipient, renders the body, inserts the row in
///     <c>Pending</c>, then attempts the send.
///   </item>
///   <item>
///     <see cref="RedispatchAsync"/> — invoked by the retry consumer for a
///     notification whose previous attempt failed transiently. Re-runs the
///     channel adapter against the same persisted row.
///   </item>
/// </list>
///
/// Both paths funnel into <see cref="AttemptSendAsync"/>, which decides
/// between transient retry (re-enqueue) and terminal failure (mark
/// <c>Failed</c> + park on DLQ) once <see cref="NotificationRecord.MaxAttempts"/>
/// is reached.
///
/// The audit row is committed before the provider call so a crash mid-send
/// still leaves a Pending record that the retry queue can pick up.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly INotificationRepository _records;
    private readonly ITenantContactResolver _contacts;
    private readonly ITemplateRenderer _renderer;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IUnitOfWork _uow;
    private readonly INotificationRetryQueue _retryQueue;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository records,
        ITenantContactResolver contacts,
        ITemplateRenderer renderer,
        IEmailSender email,
        ISmsSender sms,
        IUnitOfWork uow,
        INotificationRetryQueue retryQueue,
        ILogger<NotificationDispatcher> logger)
    {
        _records = records;
        _contacts = contacts;
        _renderer = renderer;
        _email = email;
        _sms = sms;
        _uow = uow;
        _retryQueue = retryQueue;
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
        // Audit row committed before send — if the process dies during the
        // channel call we still have a Pending row to find later.

        await AttemptSendAsync(record, ct);
    }

    /// <summary>
    /// Retry path. The retry consumer (Rabbit work queue) calls this with
    /// the notification id; we re-load the row and run another send attempt.
    /// </summary>
    public async Task RedispatchAsync(Guid notificationId, CancellationToken ct)
    {
        var record = await _records.GetAsync(notificationId, ct);
        if (record is null)
        {
            _logger.LogWarning(
                "Retry: notification {NotificationId} not found; dropping message.",
                notificationId);
            return;
        }
        if (record.State != NotificationState.Pending)
        {
            _logger.LogInformation(
                "Retry: notification {NotificationId} is in {State}; nothing to do.",
                notificationId, record.State);
            return;
        }

        await AttemptSendAsync(record, ct);
    }

    private async Task AttemptSendAsync(NotificationRecord record, CancellationToken ct)
    {
        DeliveryResult result;
        try
        {
            result = record.Channel == NotificationChannel.Sms
                ? await _sms.SendAsync(new SmsMessage(record.Recipient, record.Body), ct)
                : await _email.SendAsync(
                    new EmailMessage(record.Recipient, record.Subject, record.Body), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Channel adapter threw for notification {NotificationId}; marking attempt failed.",
                record.Id);
            result = DeliveryResult.Fail("ADAPTER_THREW");
        }

        if (result.Success)
        {
            record.MarkSent(result.ProviderReference);
            await _uow.SaveChangesAsync(ct);
            return;
        }

        var reason = result.FailureReason ?? "UNKNOWN";

        // attempt this counts as: current AttemptCount + 1.
        var attemptedNow = record.AttemptCount + 1;
        if (attemptedNow >= NotificationRecord.MaxAttempts)
        {
            // Budget exhausted — terminal.
            record.MarkFailed(reason);
            await _uow.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Notification {NotificationId} exhausted retry budget ({Attempts}/{Max}); parking on DLQ.",
                record.Id, attemptedNow, NotificationRecord.MaxAttempts);
            await _retryQueue.ParkOnDlqAsync(record.Id, reason, ct);
            return;
        }

        // Transient — bump attempt counter, stay Pending, schedule retry.
        record.MarkTransientFailure(reason);
        await _uow.SaveChangesAsync(ct);

        var nextAttempt = record.AttemptCount + 1; // the *upcoming* retry
        _logger.LogInformation(
            "Notification {NotificationId} attempt {Attempt}/{Max} failed ({Reason}); scheduling retry {NextAttempt}.",
            record.Id, record.AttemptCount, NotificationRecord.MaxAttempts, reason, nextAttempt);
        await _retryQueue.EnqueueAsync(record.Id, nextAttempt, ct);
    }
}
