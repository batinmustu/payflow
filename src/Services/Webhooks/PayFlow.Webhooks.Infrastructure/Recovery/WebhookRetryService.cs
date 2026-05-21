using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Application.Deliveries;
using PayFlow.Webhooks.Domain.Deliveries;

namespace PayFlow.Webhooks.Infrastructure.Recovery;

/// <summary>
/// Picks up deliveries whose <c>NextAttemptAt</c> has elapsed and the
/// <c>State</c> is still Pending — i.e. the previous attempt failed
/// transiently. Each due row is re-driven through the dispatcher in
/// its own DI scope so one bad delivery cannot poison the batch.
///
/// 30s tick, 25-row batches, MaxAttempts gate. The dispatcher itself is
/// responsible for re-scheduling or terminal-failing the row — this
/// service just nudges it.
/// </summary>
public sealed class WebhookRetryService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 25;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WebhookRetryService> _logger;

    public WebhookRetryService(IServiceScopeFactory scopes, ILogger<WebhookRetryService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook retry sweep failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        List<Guid> dueIds;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var deliveries = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
            var rows = await deliveries.ListPendingRetriesAsync(
                dueBefore: DateTimeOffset.UtcNow,
                maxAttempts: WebhookDelivery.MaxAttempts,
                take: BatchSize,
                ct);
            dueIds = rows.Select(r => r.Id).ToList();
        }

        if (dueIds.Count == 0) return;
        _logger.LogInformation("Webhook retry: {Count} delivery(ies) due.", dueIds.Count);

        foreach (var id in dueIds)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<WebhookDispatcher>();
                await dispatcher.RedispatchAsync(id, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Retry tick failed for delivery {DeliveryId}; will retry next interval.", id);
            }
        }
    }
}
