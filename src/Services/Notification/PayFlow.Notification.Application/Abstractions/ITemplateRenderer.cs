using PayFlow.Notification.Domain;

namespace PayFlow.Notification.Application.Abstractions;

/// <summary>
/// Picks the right template for the (kind, channel) pair, renders it with
/// the model, and returns the subject + body. Concrete implementation lives
/// in Infrastructure (Scriban + embedded templates) so Application stays
/// engine-agnostic.
/// </summary>
public interface ITemplateRenderer
{
    Task<RenderedTemplate> RenderAsync(
        NotificationKind kind,
        NotificationChannel channel,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken ct);
}

public sealed record RenderedTemplate(string Subject, string Body);
