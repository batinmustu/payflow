using PayFlow.SharedKernel;

namespace PayFlow.Identity.Domain.Tenants;

public sealed record TenantCreatedDomainEvent(Guid TenantId, string Name, string Slug) : DomainEvent;
