using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Application.RefundSagas;
using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Infrastructure.RefundSagas;

/// <summary>
/// Periodically retries refund sagas stuck in <c>ProviderCalled</c>. Without
/// this, a transient provider failure that lands the saga in ProviderCalled
/// would never re-attempt (the original Kafka delivery has already been
/// processed and the dedup check in the consumer blocks redelivery), so the
/// saga would sit there until the retry budget elsewhere kicks in — which is
/// here.
///
/// Each tick:
///   1. Pull a small batch of sagas in ProviderCalled with AttemptCount &lt;
///      <see cref="RefundSaga.MaxAttempts"/> and StartedAt older than the
///      grace window (so first attempts in flight aren't snatched).
///   2. For each, MarkProviderCalled (attempt++) + SaveChanges to claim it.
///   3. Hand to <see cref="RefundSagaProcessor"/>, which re-calls Payment
///      with the same idempotency key (saga.Id) and either lands on a
///      terminal state or stays in ProviderCalled for the next tick.
/// </summary>
public sealed class RefundSagaRecoveryService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GraceWindow = TimeSpan.FromSeconds(30);
    private const int BatchSize = 25;

    private readonly IServiceProvider _services;
    private readonly ILogger<RefundSagaRecoveryService> _logger;

    public RefundSagaRecoveryService(IServiceProvider services, ILogger<RefundSagaRecoveryService> logger)
    {
        _services = services;
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
                _logger.LogError(ex, "Refund saga recovery sweep failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        // Load the candidate ids first in a short scope, then process each
        // saga in its own fresh scope. Keeps the DbContext per saga small,
        // and lets a single bad saga (handler throws unexpectedly) not poison
        // the rest of the batch.
        IReadOnlyList<(Guid TenantId, Guid SagaId)> stuckIds;
        await using (var scope = _services.CreateAsyncScope())
        {
            var sagas = scope.ServiceProvider.GetRequiredService<IRefundSagaRepository>();
            var rows = await sagas.ListStuckProviderCalledAsync(
                staleSince: DateTimeOffset.UtcNow - GraceWindow,
                maxAttempts: RefundSaga.MaxAttempts,
                take: BatchSize,
                ct);
            stuckIds = rows.Select(r => (r.TenantId, r.Id)).ToList();
        }

        if (stuckIds.Count == 0) return;

        _logger.LogInformation(
            "Refund saga recovery: {Count} saga(s) stuck in ProviderCalled.", stuckIds.Count);

        foreach (var (tenantId, sagaId) in stuckIds)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await using var scope = _services.CreateAsyncScope();
                var sagas = scope.ServiceProvider.GetRequiredService<IRefundSagaRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var processor = scope.ServiceProvider.GetRequiredService<RefundSagaProcessor>();

                var saga = await sagas.GetAsync(tenantId, sagaId, ct);
                if (saga is null || saga.State != RefundSagaState.ProviderCalled) continue;
                // State guard against the (rare) race where the saga moved
                // since we listed it. Saves an unnecessary AttemptCount++.

                saga.MarkProviderCalled(); // attempt++
                await uow.SaveChangesAsync(ct);
                await processor.ProcessAsync(tenantId, sagaId, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Recovery tick failed for saga {SagaId}; will retry next interval.", sagaId);
            }
        }
    }
}
