using MediatR;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Domain.Tenants;
using PayFlow.Identity.Domain.Users;
using PayFlow.SharedKernel;

namespace PayFlow.Identity.Application.Tenants.RegisterTenant;

internal sealed class RegisterTenantCommandHandler
    : IRequestHandler<RegisterTenantCommand, Result<RegisterTenantResponse>>
{
    private const string AdminRole = "admin";

    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;

    public RegisterTenantCommandHandler(
        ITenantRepository tenants,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork uow)
    {
        _tenants = tenants;
        _users = users;
        _hasher = hasher;
        _uow = uow;
    }

    public async Task<Result<RegisterTenantResponse>> Handle(
        RegisterTenantCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var emailResult = Email.Create(command.AdminEmail);
        if (emailResult.IsFailure)
        {
            return Result.Failure<RegisterTenantResponse>(emailResult.ErrorCode!);
        }

        var normalisedSlug = command.Slug.Trim().ToLowerInvariant();
        if (await _tenants.SlugExistsAsync(normalisedSlug, ct))
        {
            return Result.Failure<RegisterTenantResponse>("TENANT_SLUG_TAKEN");
        }

        var tenantResult = Tenant.Create(command.Name, command.Slug);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<RegisterTenantResponse>(tenantResult.ErrorCode!);
        }
        var tenant = tenantResult.Value;

        var passwordHash = _hasher.Hash(command.AdminPassword);

        var userResult = User.Create(tenant.Id, emailResult.Value, passwordHash, command.AdminDisplayName);
        if (userResult.IsFailure)
        {
            return Result.Failure<RegisterTenantResponse>(userResult.ErrorCode!);
        }
        var admin = userResult.Value;
        admin.GrantRole(AdminRole);

        await _tenants.AddAsync(tenant, ct);
        await _users.AddAsync(admin, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new RegisterTenantResponse(tenant.Id, admin.Id, tenant.Slug));
    }
}
