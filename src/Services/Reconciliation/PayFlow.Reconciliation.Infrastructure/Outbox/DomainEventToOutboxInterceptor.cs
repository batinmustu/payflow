using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PayFlow.Outbox;
using PayFlow.SharedKernel;

namespace PayFlow.Reconciliation.Infrastructure.Outbox;

/// <summary>
/// Same shape as Transaction's interceptor — turns every
/// <see cref="IIntegrationDomainEvent"/> raised on an aggregate into an
/// <see cref="OutboxMessage"/> in the same SaveChanges. Keeps the saga's
/// state change and the outboxed RefundProcessing/Completed/Failed atomic.
/// </summary>
internal sealed class DomainEventToOutboxInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChangesAsync(eventData, result, ct);

        var aggregates = ctx.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0)
        {
            return base.SavingChangesAsync(eventData, result, ct);
        }

        var outbox = ctx.Set<OutboxMessage>();

        foreach (var entry in aggregates)
        {
            var aggregateType = entry.Entity.GetType().Name;
            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                if (domainEvent is not IIntegrationDomainEvent integration)
                {
                    continue;
                }

                var payload = JsonSerializer.Serialize<object>(domainEvent, Json);
                var headers = JsonSerializer.Serialize(new
                {
                    message_id = domainEvent.EventId,
                    occurred_at = domainEvent.OccurredAt,
                    tenant_id = integration.TenantId,
                    schema_version = "v1",
                }, Json);

                var row = OutboxMessage.Create(
                    tenantId: integration.TenantId,
                    aggregateType: aggregateType,
                    aggregateId: integration.AggregateId,
                    eventType: integration.EventType,
                    payload: payload,
                    headers: headers);

                outbox.Add(row);
            }
            entry.Entity.ClearDomainEvents();
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
