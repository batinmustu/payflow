using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Reporting.Infrastructure.Persistence;

namespace PayFlow.Reporting.Infrastructure.Maintenance;

/// <summary>
/// Periodically deletes <c>processed_events</c> rows older than the retention
/// horizon. Reporting writes one row per consumed Kafka message — without a
/// cleanup the table grows linearly with traffic. The horizon (default 90
/// days) is longer than Kafka's default retention window so duplicate-protection
/// still works for any redelivery the broker could realistically produce.
/// </summary>
internal sealed class ProcessedEventsRetentionService : BackgroundService
{
    private static readonly TimeSpan RetentionHorizon = TimeSpan.FromDays(90);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan FirstSweepDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _services;
    private readonly ILogger<ProcessedEventsRetentionService> _logger;

    public ProcessedEventsRetentionService(
        IServiceProvider services,
        ILogger<ProcessedEventsRetentionService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstSweepDelay, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "processed_events retention sweep failed");
            }

            try { await Task.Delay(SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        var cutoff = DateTimeOffset.UtcNow - RetentionHorizon;
        var deleted = await db.ProcessedEvents
            .Where(p => p.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Removed {Deleted} processed_events row(s) older than {Cutoff:o}.",
                deleted, cutoff);
        }
    }
}
