using Microsoft.EntityFrameworkCore;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Domain.Tenants;

namespace PayFlow.Identity.Infrastructure.Persistence;

internal sealed class TenantRepository : ITenantRepository
{
    private readonly IdentityDbContext _db;

    public TenantRepository(IdentityDbContext db) => _db = db;

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        var normalised = slug.Trim().ToLowerInvariant();
        return _db.Tenants.AnyAsync(t => t.Slug == normalised, ct);
    }

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var normalised = slug.Trim().ToLowerInvariant();
        return _db.Tenants.FirstOrDefaultAsync(t => t.Slug == normalised, ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        await _db.Tenants.AddAsync(tenant, ct);
    }
}
