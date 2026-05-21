using Microsoft.Extensions.Logging.Abstractions;
using PayFlow.Notification.Application.Abstractions;
using PayFlow.Notification.Application.Notifications;

namespace PayFlow.Notification.UnitTests;

/// <summary>
/// Verifies the retry-vs-terminal decision inside <see cref="NotificationDispatcher"/>:
/// every transient failure under <see cref="NotificationRecord.MaxAttempts"/>
/// must enqueue another retry, and the attempt that hits the budget must
/// transition to Failed + park on the DLQ.
/// </summary>
public sealed class NotificationDispatcherRetryTests
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyModel =
        new Dictionary<string, object?>();

    [Fact]
    public async Task First_attempt_success_marks_sent_without_touching_retry_queue()
    {
        var records = new FakeNotificationRepository();
        var retry = new RecordingRetryQueue();
        var dispatcher = BuildDispatcher(
            records,
            retry,
            new ScriptedEmailSender(DeliveryResult.Ok("smtp-1")));

        await dispatcher.DispatchAsync(
            sourceMessageId: Guid.NewGuid(),
            sourceEventType: "payflow.transaction.captured.v1",
            tenantId: Guid.NewGuid(),
            kind: NotificationKind.TransactionCaptured,
            channel: NotificationChannel.Email,
            model: EmptyModel,
            ct: CancellationToken.None);

        var record = records.Records.Should().ContainSingle().Subject;
        record.State.Should().Be(NotificationState.Sent);
        record.AttemptCount.Should().Be(1);
        record.ProviderReference.Should().Be("smtp-1");
        retry.Enqueued.Should().BeEmpty();
        retry.Parked.Should().BeEmpty();
    }

    [Fact]
    public async Task Transient_failure_keeps_record_pending_and_schedules_retry()
    {
        var records = new FakeNotificationRepository();
        var retry = new RecordingRetryQueue();
        var dispatcher = BuildDispatcher(
            records,
            retry,
            new ScriptedEmailSender(DeliveryResult.Fail("SMTP_TIMEOUT")));

        await dispatcher.DispatchAsync(
            sourceMessageId: Guid.NewGuid(),
            sourceEventType: "payflow.transaction.captured.v1",
            tenantId: Guid.NewGuid(),
            kind: NotificationKind.TransactionCaptured,
            channel: NotificationChannel.Email,
            model: EmptyModel,
            ct: CancellationToken.None);

        var record = records.Records.Should().ContainSingle().Subject;
        record.State.Should().Be(NotificationState.Pending);
        record.AttemptCount.Should().Be(1);
        record.FailureReason.Should().Be("SMTP_TIMEOUT");

        // We're already 1 attempt in, so the scheduled retry is attempt #2.
        retry.Enqueued.Should().ContainSingle()
            .Which.Should().Be((record.Id, 2));
        retry.Parked.Should().BeEmpty();
    }

    [Fact]
    public async Task Budget_exhausted_marks_failed_and_parks_on_dlq()
    {
        var records = new FakeNotificationRepository();
        var retry = new RecordingRetryQueue();

        // First attempt is created by DispatchAsync; subsequent attempts
        // come from RedispatchAsync via the retry consumer. Script enough
        // failures to chew through MaxAttempts.
        var failures = Enumerable.Range(0, NotificationRecord.MaxAttempts)
            .Select(_ => DeliveryResult.Fail("SMTP_TIMEOUT"))
            .ToArray();
        var dispatcher = BuildDispatcher(records, retry, new ScriptedEmailSender(failures));

        await dispatcher.DispatchAsync(
            sourceMessageId: Guid.NewGuid(),
            sourceEventType: "payflow.transaction.captured.v1",
            tenantId: Guid.NewGuid(),
            kind: NotificationKind.TransactionCaptured,
            channel: NotificationChannel.Email,
            model: EmptyModel,
            ct: CancellationToken.None);

        var record = records.Records.Single();
        // Drive the rest of the budget through Redispatch.
        for (var i = 1; i < NotificationRecord.MaxAttempts; i++)
        {
            await dispatcher.RedispatchAsync(record.Id, CancellationToken.None);
        }

        record.State.Should().Be(NotificationState.Failed);
        record.AttemptCount.Should().Be(NotificationRecord.MaxAttempts);
        retry.Parked.Should().ContainSingle()
            .Which.Should().Be((record.Id, "SMTP_TIMEOUT"));

        // Retries scheduled exactly MaxAttempts - 1 times (the final attempt
        // skips Enqueue and parks instead).
        retry.Enqueued.Should().HaveCount(NotificationRecord.MaxAttempts - 1);
    }

    [Fact]
    public async Task Redispatch_against_already_sent_record_is_noop()
    {
        var records = new FakeNotificationRepository();
        var retry = new RecordingRetryQueue();
        var sender = new ScriptedEmailSender(DeliveryResult.Ok("smtp-1"));
        var dispatcher = BuildDispatcher(records, retry, sender);

        await dispatcher.DispatchAsync(
            sourceMessageId: Guid.NewGuid(),
            sourceEventType: "payflow.transaction.captured.v1",
            tenantId: Guid.NewGuid(),
            kind: NotificationKind.TransactionCaptured,
            channel: NotificationChannel.Email,
            model: EmptyModel,
            ct: CancellationToken.None);

        var record = records.Records.Single();
        sender.Calls.Should().Be(1);

        await dispatcher.RedispatchAsync(record.Id, CancellationToken.None);

        // No second send happened — the dispatcher saw Sent and bailed.
        sender.Calls.Should().Be(1);
        retry.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_source_message_is_deduplicated_with_no_send_attempt()
    {
        var records = new FakeNotificationRepository();
        var sender = new ScriptedEmailSender(DeliveryResult.Ok("smtp-1"));
        var dispatcher = BuildDispatcher(records, new RecordingRetryQueue(), sender);

        var sourceMessageId = Guid.NewGuid();
        records.ExistingSourceIds.Add(sourceMessageId);

        await dispatcher.DispatchAsync(
            sourceMessageId: sourceMessageId,
            sourceEventType: "payflow.transaction.captured.v1",
            tenantId: Guid.NewGuid(),
            kind: NotificationKind.TransactionCaptured,
            channel: NotificationChannel.Email,
            model: EmptyModel,
            ct: CancellationToken.None);

        records.Records.Should().BeEmpty();
        sender.Calls.Should().Be(0);
    }

    private static NotificationDispatcher BuildDispatcher(
        FakeNotificationRepository records,
        RecordingRetryQueue retry,
        ScriptedEmailSender email) =>
        new(
            records: records,
            contacts: new FakeContactResolver(),
            renderer: new FakeRenderer(),
            email: email,
            sms: new ScriptedSmsSender(),
            uow: new FakeUnitOfWork(),
            retryQueue: retry,
            logger: NullLogger<NotificationDispatcher>.Instance);
}
