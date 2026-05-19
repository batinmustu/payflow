using PayFlow.Identity.Domain.Tenants;

namespace PayFlow.Identity.Application.Abstractions;

public interface ITenantRepository
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct);
    Task AddAsync(Tenant tenant, CancellationToken ct);
}
