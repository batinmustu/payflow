namespace PayFlow.SharedKernel;

/// <summary>
/// Non-generic facet implemented by every aggregate so framework code (EF
/// SaveChanges interceptor, etc.) can iterate aggregates without knowing
/// their id type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
