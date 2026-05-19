using PayFlow.SharedKernel;

namespace PayFlow.Identity.Domain.Users;

/// <summary>
/// A person who logs into a tenant's dashboard. Uniqueness is
/// <c>(TenantId, Email)</c> — the same email can belong to users of different
/// tenants. The password hash lives on the aggregate; the hashing algorithm
/// is the Infrastructure layer's concern.
/// </summary>
public sealed class User : AggregateRoot<Guid>
{
    private readonly List<string> _roles = [];

    public Guid TenantId { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string DisplayName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<string> Roles => _roles;

    private User(
        Guid id,
        Guid tenantId,
        Email email,
        string passwordHash,
        string displayName,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    public static Result<User> Create(
        Guid tenantId,
        Email email,
        string passwordHash,
        string displayName)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<User>("TENANT_REQUIRED");
        }

        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>("PASSWORD_HASH_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<User>("DISPLAY_NAME_REQUIRED");
        }

        var user = new User(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            email: email,
            passwordHash: passwordHash,
            displayName: displayName.Trim(),
            createdAt: DateTimeOffset.UtcNow);

        user.Raise(new UserCreatedDomainEvent(user.Id, tenantId, email.Value, user.DisplayName));
        return Result.Success(user);
    }

    public void GrantRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        var normalised = role.Trim();
        if (!_roles.Contains(normalised, StringComparer.OrdinalIgnoreCase))
        {
            _roles.Add(normalised);
        }
    }

    public void RevokeRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        var match = _roles.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            _roles.Remove(match);
        }
    }

    public void ChangeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be blank.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
    }
}
