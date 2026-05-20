using System.Globalization;
using System.Text.RegularExpressions;
using PayFlow.Notification.Application.Abstractions;
using PayFlow.Notification.Domain;

namespace PayFlow.Notification.Infrastructure.Templates;

/// <summary>
/// Tiny template renderer with <c>{{ key }}</c> placeholders. Each
/// (kind, channel) maps to a baked-in subject + body string here; bringing
/// a real engine (Scriban, RazorLight) is a swap of this class only — the
/// <see cref="ITemplateRenderer"/> contract doesn't change.
/// </summary>
internal sealed partial class PlaceholderTemplateRenderer : ITemplateRenderer
{
    private static readonly Regex PlaceholderRegex = BuildPlaceholderRegex();

    [GeneratedRegex(@"\{\{\s*(?<key>[a-zA-Z_][a-zA-Z0-9_]*)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex BuildPlaceholderRegex();

    private static readonly Dictionary<(NotificationKind, NotificationChannel), (string Subject, string Body)> Templates =
        new()
        {
            [(NotificationKind.TransactionCaptured, NotificationChannel.Email)] = (
                Subject: "Payment captured — {{ providerCode }} {{ amount }} {{ currency }}",
                Body: """
                    Hi,

                    Your payment of {{ amount }} {{ currency }} was captured successfully.

                    Provider:   {{ providerCode }}
                    Reference:  {{ providerReference }}
                    Tx id:      {{ transactionId }}
                    Captured:   {{ occurredAt }}

                    — PayFlow
                    """),
            [(NotificationKind.RefundCompleted, NotificationChannel.Email)] = (
                Subject: "Refund completed — {{ amount }} {{ currency }}",
                Body: """
                    Hi,

                    Your refund of {{ amount }} {{ currency }} has been processed.

                    Provider:   {{ providerCode }}
                    Refund ref: {{ providerReference }}
                    Refund id:  {{ refundId }}
                    Tx id:      {{ transactionId }}
                    Completed:  {{ occurredAt }}

                    — PayFlow
                    """),
            [(NotificationKind.RefundFailed, NotificationChannel.Email)] = (
                Subject: "Refund could not be processed",
                Body: """
                    Hi,

                    A refund attempt could not be processed.

                    Refund id:  {{ refundId }}
                    Tx id:      {{ transactionId }}
                    Reason:     {{ failureReason }}
                    When:       {{ occurredAt }}

                    You can try again from your dashboard. If the issue persists,
                    contact support.

                    — PayFlow
                    """),
        };

    public Task<RenderedTemplate> RenderAsync(
        NotificationKind kind,
        NotificationChannel channel,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!Templates.TryGetValue((kind, channel), out var template))
        {
            throw new InvalidOperationException(
                $"No template for ({kind}, {channel}) — register one in PlaceholderTemplateRenderer.");
        }

        var subject = Substitute(template.Subject, model);
        var body = Substitute(template.Body, model);
        return Task.FromResult(new RenderedTemplate(subject, body));
    }

    private static string Substitute(string template, IReadOnlyDictionary<string, object?> model)
    {
        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (model.TryGetValue(key, out var value) && value is not null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            // Leave the placeholder visible if the caller forgot to provide it —
            // surfaces template/payload drift in the audit row instead of
            // silently producing a half-filled email.
            return $"<missing:{key}>";
        });
    }
}
