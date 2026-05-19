using Microsoft.EntityFrameworkCore;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.Infrastructure.Persistence;

internal sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> FindByEmailAsync(Guid tenantId, Email email, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        // EF translates the whole Email VO comparison through the
        // HasConversion on UserConfiguration. `u.Email.Value == ...` would
        // try to project the inner string and fail at translation.
        return _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantId && u.Email == email,
            ct);
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _db.Users.AddAsync(user, ct);
    }
}
