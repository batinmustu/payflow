using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace PayFlow.EventBus.Kafka.Telemetry;

/// <summary>
/// ActivitySources for Kafka produce/consume spans + W3C trace-context
/// propagation through Kafka message headers. Lets a trace started in
/// the producing service's HTTP request flow continue across the broker
/// into the consumer's handler — so a Jaeger search for a transaction id
/// shows the full path from "POST /api/transactions" all the way to the
/// projection update in Reporting.
/// </summary>
public static class KafkaTelemetry
{
    public const string ProducerSourceName = "PayFlow.EventBus.Kafka.Producer";
    public const string ConsumerSourceName = "PayFlow.EventBus.Kafka.Consumer";

    public static readonly ActivitySource ProducerSource = new(ProducerSourceName);
    public static readonly ActivitySource ConsumerSource = new(ConsumerSourceName);

    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>Inject the current Activity's W3C context onto Kafka headers.</summary>
    public static void Inject(Activity activity, Headers headers)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(headers);

        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            headers,
            static (h, key, value) => h.Add(key, Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>Pull a W3C parent context out of Kafka headers (or default if absent).</summary>
    public static PropagationContext Extract(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        return Propagator.Extract(
            default,
            headers,
            static (h, key) =>
            {
                if (h.TryGetLastBytes(key, out var bytes))
                {
                    return [Encoding.UTF8.GetString(bytes)];
                }
                return [];
            });
    }
}
