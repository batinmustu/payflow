using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PayFlow.Outbox;
using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Infrastructure.Outbox;

/// <summary>
/// On every SaveChangesAsync, walks every tracked aggregate and turns each
/// of its <see cref="IIntegrationDomainEvent"/>s into an
/// <see cref="OutboxMessage"/> row added to the same DB transaction. This is
/// the "publish on commit" half of ADR-0004 — either the business state
/// change and the outbox row land together, or neither does.
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
                    // Pure in-process event; never crosses a service boundary.
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
