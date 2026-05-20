using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace PayFlow.Observability;

/// <summary>
/// Pushes <c>TenantId</c>, <c>UserId</c>, and <c>CorrelationId</c> into
/// Serilog's <c>LogContext</c> for the lifetime of the request, plus
/// echoes the correlation id back to the caller via the
/// <c>X-Correlation-Id</c> response header. Sits after authentication
/// + tenant middleware so the claims it reads are populated.
/// </summary>
public sealed class LogEnrichmentMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public LogEnrichmentMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = ResolveCorrelationId(context);
        context.Response.Headers[HeaderName] = correlationId;

        var props = new List<IDisposable>(3)
        {
            LogContext.PushProperty("CorrelationId", correlationId),
        };

        var tenantId = context.User.FindFirstValue("tid");
        if (!string.IsNullOrEmpty(tenantId))
        {
            props.Add(LogContext.PushProperty("TenantId", tenantId));
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!string.IsNullOrEmpty(userId))
        {
            props.Add(LogContext.PushProperty("UserId", userId));
        }

        try
        {
            await _next(context);
        }
        finally
        {
            for (var i = props.Count - 1; i >= 0; i--)
            {
                props[i].Dispose();
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var inbound)
            && !string.IsNullOrWhiteSpace(inbound))
        {
            return inbound.ToString();
        }
        // Falls back to Activity id when there's an in-flight trace — gives
        // a free correlation_id that lines up 1:1 with the Jaeger trace.
        return System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }
}

public static class LogEnrichmentBuilderExtensions
{
    /// <summary>
    /// Plug after <c>UseAuthentication()</c> + <c>UsePayFlowMultitenancy()</c>
    /// so JWT claims and the resolved tenant id are visible to the
    /// enricher.
    /// </summary>
    public static IApplicationBuilder UsePayFlowLogEnrichment(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<LogEnrichmentMiddleware>();
    }
}
