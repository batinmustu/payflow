namespace PayFlow.SharedKernel;

/// <summary>
/// Marker + metadata for domain events that should also surface as cross-service
/// integration events through the outbox. Plain <see cref="DomainEvent"/> stays
/// in-process; events that downstream services care about implement this and
/// carry the routing/identification headers the outbox needs to write a row.
///
/// See docs/architecture/bounded-contexts.md: "Domain Event vs Integration Event".
/// </summary>
public interface IIntegrationDomainEvent
{
    /// <summary>Tenant the event belongs to (becomes the Kafka partition key).</summary>
    Guid TenantId { get; }

    /// <summary>Id of the aggregate that raised it. Useful for forensic joins.</summary>
    Guid AggregateId { get; }

    /// <summary>
    /// Stable event name, e.g. <c>payflow.transaction.captured.v1</c>. Matches
    /// the topic name in docs/events/catalog.md.
    /// </summary>
    string EventType { get; }
}
