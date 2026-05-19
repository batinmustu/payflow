using MediatR;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Application.Tenants.RegisterTenant;
using PayFlow.Identity.Domain.Tenants;
using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.UnitTests.Tenants;

public class RegisterTenantCommandHandlerTests
{
    private readonly FakeTenantRepository _tenants = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeUnitOfWork _uow = new();

    private IRequestHandler<RegisterTenantCommand, Result<RegisterTenantResponse>> Handler()
        => (IRequestHandler<RegisterTenantCommand, Result<RegisterTenantResponse>>)
            Activator.CreateInstance(
                typeof(RegisterTenantCommandHandler),
                _tenants, _users, _hasher, _uow)!;

    private static RegisterTenantCommand AValidCommand() => new(
        Name: "Acme Payments",
        Slug: "acme",
        AdminEmail: "admin@acme.test",
        AdminPassword: "correct horse battery staple",
        AdminDisplayName: "Acme Admin");

    [Fact]
    public async Task Creates_tenant_and_admin_with_password_hashed_and_role_granted()
    {
        var handler = Handler();

        var result = await handler.Handle(AValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _tenants.Saved.Should().ContainSingle();
        _users.Saved.Should().ContainSingle();
        _uow.SaveCount.Should().Be(1);

        var tenant = _tenants.Saved.Single();
        tenant.Name.Should().Be("Acme Payments");
        tenant.Slug.Should().Be("acme");

        var admin = _users.Saved.Single();
        admin.TenantId.Should().Be(tenant.Id);
        admin.Email.Value.Should().Be("admin@acme.test");
        admin.PasswordHash.Should().Be("HASHED:correct horse battery staple");
        admin.Roles.Should().Equal("admin");

        result.Value.TenantId.Should().Be(tenant.Id);
        result.Value.AdminUserId.Should().Be(admin.Id);
        result.Value.Slug.Should().Be("acme");
    }

    [Fact]
    public async Task Rejects_when_slug_already_taken()
    {
        _tenants.ReservedSlugs.Add("acme");
        var handler = Handler();

        var result = await handler.Handle(AValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TENANT_SLUG_TAKEN");
        _uow.SaveCount.Should().Be(0);
        _tenants.Saved.Should().BeEmpty();
        _users.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Surfaces_email_validation_error()
    {
        var handler = Handler();

        var result = await handler.Handle(
            AValidCommand() with { AdminEmail = "not-an-email" },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("EMAIL_INVALID");
        _uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Surfaces_tenant_validation_error()
    {
        var handler = Handler();

        var result = await handler.Handle(
            AValidCommand() with { Slug = "Has Spaces" },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TENANT_SLUG_INVALID");
        _uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Slug_uniqueness_check_is_case_insensitive()
    {
        _tenants.ReservedSlugs.Add("acme");
        var handler = Handler();

        var result = await handler.Handle(
            AValidCommand() with { Slug = "ACME" },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TENANT_SLUG_TAKEN");
    }

    // ---- Fakes ----------------------------------------------------------

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public List<Tenant> Saved { get; } = [];
        public HashSet<string> ReservedSlugs { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
            => Task.FromResult(ReservedSlugs.Contains(slug));

        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
            => Task.FromResult(Saved.FirstOrDefault(t => t.Slug == slug));

        public Task AddAsync(Tenant tenant, CancellationToken ct)
        {
            Saved.Add(tenant);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Saved { get; } = [];

        public Task<User?> FindByEmailAsync(Guid tenantId, Email email, CancellationToken ct)
            => Task.FromResult(Saved.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email));

        public Task AddAsync(User user, CancellationToken ct)
        {
            Saved.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plaintext) => $"HASHED:{plaintext}";
        public bool Verify(string plaintext, string hash) => hash == $"HASHED:{plaintext}";
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
