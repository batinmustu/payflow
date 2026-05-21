using PayFlow.Notification.Application.Abstractions;
using PayFlow.Notification.Application.Notifications;
using PayFlow.Notification.Domain.Notifications;

namespace PayFlow.Notification.UnitTests;

// Minimal hand-rolled fakes for the dispatcher unit tests. Each one is a
// behavior knob: the test toggles "succeed / fail / throw" + reads back the
// interaction history. No DI container, no mocking framework.

internal sealed class FakeNotificationRepository : INotificationRepository
{
    public readonly List<NotificationRecord> Records = new();
    public readonly HashSet<Guid> ExistingSourceIds = new();

    public Task<bool> ExistsForSourceMessageAsync(Guid sourceMessageId, CancellationToken ct) =>
        Task.FromResult(ExistingSourceIds.Contains(sourceMessageId));

    public Task<IReadOnlyList<NotificationRecord>> ListAsync(Guid tenantId, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NotificationRecord>>(Records);

    public Task<NotificationRecord?> GetAsync(Guid notificationId, CancellationToken ct) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Id == notificationId));

    public Task AddAsync(NotificationRecord record, CancellationToken ct)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeContactResolver : ITenantContactResolver
{
    public string Email { get; set; } = "merchant@example.com";
    public string? Phone { get; set; } = "+905551112233";
    public Task<TenantContact> ResolveAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult(new TenantContact(Email, Phone));
}

internal sealed class FakeRenderer : ITemplateRenderer
{
    public Task<RenderedTemplate> RenderAsync(
        NotificationKind kind,
        NotificationChannel channel,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken ct) =>
        Task.FromResult(new RenderedTemplate("Subject", "Body"));
}

internal sealed class ScriptedEmailSender : IEmailSender
{
    private readonly Queue<DeliveryResult> _scripted;
    public int Calls { get; private set; }
    public ScriptedEmailSender(params DeliveryResult[] results) =>
        _scripted = new Queue<DeliveryResult>(results);
    public Task<DeliveryResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(_scripted.Count > 0 ? _scripted.Dequeue() : DeliveryResult.Ok("default"));
    }
}

internal sealed class ScriptedSmsSender : ISmsSender
{
    public Task<DeliveryResult> SendAsync(SmsMessage message, CancellationToken ct) =>
        Task.FromResult(DeliveryResult.Ok("sms-default"));
}

internal sealed class RecordingRetryQueue : INotificationRetryQueue
{
    public List<(Guid NotificationId, int NextAttempt)> Enqueued { get; } = new();
    public List<(Guid NotificationId, string Reason)> Parked { get; } = new();

    public Task EnqueueAsync(Guid notificationId, int nextAttempt, CancellationToken ct)
    {
        Enqueued.Add((notificationId, nextAttempt));
        return Task.CompletedTask;
    }

    public Task ParkOnDlqAsync(Guid notificationId, string reason, CancellationToken ct)
    {
        Parked.Add((notificationId, reason));
        return Task.CompletedTask;
    }
}
