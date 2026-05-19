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

        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.Seq(seqUrl));
    }

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());
    }
}
