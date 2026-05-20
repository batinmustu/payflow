using System.Net;
using Microsoft.Extensions.Logging;

namespace PayFlow.Multitenancy;

/// <summary>
/// A tiny Polly-free retry handler for HttpClient. Retries on transport
/// failures (HttpRequestException, TaskCanceledException not initiated by
/// the caller) and 5xx / 408 status codes, with exponential backoff
/// (100ms × 2^attempt + a small jitter, capped at 2s). Caller must keep the
/// downstream call idempotent — the typed clients here already pass an
/// idempotency key, so a retry that hits the server twice is a no-op.
/// </summary>
public sealed class RetryingHttpMessageHandler : DelegatingHandler
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(2);

    private readonly ILogger<RetryingHttpMessageHandler> _logger;

    public RetryingHttpMessageHandler(ILogger<RetryingHttpMessageHandler> logger)
        => _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (!ShouldRetryStatus(response.StatusCode) || attempt >= MaxAttempts)
                {
                    return response;
                }

                _logger.LogWarning(
                    "Transient HTTP {Status} from {Uri}; retry {Attempt}/{Max}.",
                    (int)response.StatusCode, request.RequestUri, attempt, MaxAttempts);
                response.Dispose();
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken))
            {
                if (attempt >= MaxAttempts)
                {
                    throw;
                }
                _logger.LogWarning(ex,
                    "Transient HTTP failure to {Uri}; retry {Attempt}/{Max}.",
                    request.RequestUri, attempt, MaxAttempts);
            }

            var delay = ComputeBackoff(attempt);
            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }
    }

    private static bool ShouldRetryStatus(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout ||
        ((int)status >= 500 && (int)status <= 599);

    private static bool IsTransient(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException ||
        (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private static TimeSpan ComputeBackoff(int attempt)
    {
        var baseMs = 100 * (1 << (attempt - 1));   // 100, 200, 400
        var jitterMs = Random.Shared.Next(0, 50);
        var totalMs = Math.Min(baseMs + jitterMs, (int)MaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(totalMs);
    }
}
