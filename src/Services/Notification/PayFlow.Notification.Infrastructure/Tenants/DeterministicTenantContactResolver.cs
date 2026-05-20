using PayFlow.Notification.Application.Abstractions;

namespace PayFlow.Notification.Infrastructure.Tenants;

/// <summary>
/// Dev resolver — synthesises a stable <c>merchant-{tenantId}@payflow.test</c>
/// for each tenant. Production swaps this for a real lookup against
/// Identity / a local tenant contact projection fed by tenant events.
/// </summary>
internal sealed class DeterministicTenantContactResolver : ITenantContactResolver
{
    public Task<TenantContact> ResolveAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId must not be empty.", nameof(tenantId));
        }
        var email = $"merchant-{tenantId:N}@payflow.test";
        return Task.FromResult(new TenantContact(email, PhoneE164: null));
    }
}
