using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(Guid tenantId, Email email, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}
