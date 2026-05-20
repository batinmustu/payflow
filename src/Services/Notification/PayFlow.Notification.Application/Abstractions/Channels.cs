namespace PayFlow.Notification.Application.Abstractions;

/// <summary>
/// What the channel adapters return. <c>ProviderReference</c> is the
/// downstream id (SMTP message-id, SMS provider id, …) we record on the
/// audit row.
/// </summary>
public sealed record DeliveryResult(bool Success, string? ProviderReference, string? FailureReason)
{
    public static DeliveryResult Ok(string? providerReference) => new(true, providerReference, null);
    public static DeliveryResult Fail(string reason) => new(false, null, reason);
}

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string Body);

public sealed record SmsMessage(
    string Recipient,
    string Body);

/// <summary>
/// Channel adapter. Production swaps the dev/mock implementation for a real
/// SMTP / SMS client; behaviour is the same: render-rejected message, attempt
/// send, return a <see cref="DeliveryResult"/>.
/// </summary>
public interface IEmailSender
{
    Task<DeliveryResult> SendAsync(EmailMessage message, CancellationToken ct);
}

public interface ISmsSender
{
    Task<DeliveryResult> SendAsync(SmsMessage message, CancellationToken ct);
}
