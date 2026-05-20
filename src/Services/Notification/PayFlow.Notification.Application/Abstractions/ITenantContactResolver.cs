namespace PayFlow.Notification.Application.Abstractions;

/// <summary>
/// Where the notification gets sent. Production: looks up the tenant's
/// notification preferences (admin email, on-call SMS) from Identity or a
/// local read-model fed by tenant events. Dev: deterministic
/// <c>merchant-{tenantId}@payflow.test</c> so log assertions stay stable.
/// </summary>
public interface ITenantContactResolver
{
    Task<TenantContact> ResolveAsync(Guid tenantId, CancellationToken ct);
}

public sealed record TenantContact(string Email, string? PhoneE164);
