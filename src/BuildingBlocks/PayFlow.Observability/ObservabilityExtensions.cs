using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace PayFlow.Observability;

/// <summary>
/// One-call wiring for the PayFlow observability stack: Serilog for structured
/// logs (Console + Seq) and OpenTelemetry for traces and metrics (OTLP to
/// Jaeger / any OTLP collector).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers Serilog and OpenTelemetry on the host builder. Every PayFlow
    /// service calls this once during composition root setup.
    /// </summary>
    /// <param name="builder">The host builder being configured.</param>
    /// <param name="serviceName">
    /// Service name reported on traces / metrics resource and logs (e.g. "payflow-transaction").
    /// </param>
    public static IHostApplicationBuilder AddPayFlowObservability(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        ConfigureSerilog(builder, serviceName);
        ConfigureOpenTelemetry(builder, serviceName);

        return builder;
    }

    private static void ConfigureSerilog(IHostApplicationBuilder builder, string serviceName)
    {
        var seqUrl = builder.Configuration["Observability:Seq:ServerUrl"]
            ?? "http://localhost:5341";

        // Console template surfaces TenantId + CorrelationId in the dev tail
        // alongside the message. Seq sees the same properties as structured
        // fields (filterable columns) without needing a template tweak.
        const string ConsoleTemplate =
            "{Timestamp:HH:mm:ss} [{Level:u3}] " +
            "{TenantId,-32:l} {CorrelationId,-16:l} {Message:lj}{NewLine}{Exception}";

        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(
                outputTemplate: ConsoleTemplate,
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.Seq(seqUrl));
    }

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // Kafka producer + consumer ActivitySources from
                // PayFlow.EventBus.Kafka.Telemetry. Hard-coded here so every
                // service picks them up without thinking; harmless when the
                // service doesn't use Kafka (no spans get emitted).
                .AddSource("PayFlow.EventBus.Kafka.Producer")
                .AddSource("PayFlow.EventBus.Kafka.Consumer")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());
    }
}
