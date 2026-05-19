using PayFlow.SharedKernel;

namespace PayFlow.Identity.Domain.Users;

public sealed record UserCreatedDomainEvent(
    Guid UserId,
    Guid TenantId,
    string Email,
    string DisplayName) : DomainEvent;
