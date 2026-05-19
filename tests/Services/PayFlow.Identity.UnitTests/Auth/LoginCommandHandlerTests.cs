using MediatR;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Application.Auth.Login;
using PayFlow.Identity.Domain.Tenants;
using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.UnitTests.Auth;

public class LoginCommandHandlerTests
{
    private readonly FakeTenants _tenants = new();
    private readonly FakeUsers _users = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeIssuer _issuer = new();

    private IRequestHandler<LoginCommand, Result<LoginResponse>> Handler()
        => (IRequestHandler<LoginCommand, Result<LoginResponse>>)
            Activator.CreateInstance(
                typeof(LoginCommandHandler),
                _tenants, _users, _hasher, _issuer)!;

    private static (Tenant tenant, User user) Seed(
        FakeTenants tenants, FakeUsers users,
        string slug = "acme", string email = "user@acme.test", string passwordHash = "HASH")
    {
        var tenant = Tenant.Create("Acme", slug).Value;
        tenants.Saved.Add(tenant);
        var user = User.Create(tenant.Id, Email.Create(email).Value, passwordHash, "Display").Value;
        user.GrantRole("admin");
        users.Saved.Add(user);
        return (tenant, user);
    }

    [Fact]
    public async Task Returns_a_token_when_credentials_match()
    {
        Seed(_tenants, _users, passwordHash: "HASH:correct");
        _hasher.AcceptPlaintexts.Add("correct");
        var handler = Handler();

        var result = await handler.Handle(
            new LoginCommand("acme", "user@acme.test", "correct"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("issued-token");
        result.Value.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(2));
        result.Value.UserId.Should().NotBe(Guid.Empty);
        result.Value.TenantId.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("acme", "user@acme.test", "wrong-password")]
    [InlineData("acme", "unknown@acme.test", "correct")]
    [InlineData("not-a-tenant", "user@acme.test", "correct")]
    [InlineData("acme", "not-an-email", "correct")]
    public async Task All_failure_paths_return_the_same_AUTH_INVALID_CREDENTIALS_code(
        string slug, string email, string password)
    {
        Seed(_tenants, _users, passwordHash: "HASH:correct");
        _hasher.AcceptPlaintexts.Add("correct");
        var handler = Handler();

        var result = await handler.Handle(new LoginCommand(slug, email, password), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Does_not_issue_a_token_when_the_password_does_not_verify()
    {
        Seed(_tenants, _users);
        _hasher.AcceptPlaintexts.Add("only-this-works");
        var handler = Handler();

        await handler.Handle(new LoginCommand("acme", "user@acme.test", "something-else"), CancellationToken.None);

        _issuer.Issued.Should().BeEmpty();
    }

    // ---- Fakes ----------------------------------------------------------

    private sealed class FakeTenants : ITenantRepository
    {
        public List<Tenant> Saved { get; } = [];

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
            => Task.FromResult(Saved.Any(t => string.Equals(t.Slug, slug.ToLowerInvariant(), StringComparison.Ordinal)));

        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
            => Task.FromResult(Saved.FirstOrDefault(t => string.Equals(t.Slug, slug.Trim().ToLowerInvariant(), StringComparison.Ordinal)));

        public Task AddAsync(Tenant tenant, CancellationToken ct) { Saved.Add(tenant); return Task.CompletedTask; }
    }

    private sealed class FakeUsers : IUserRepository
    {
        public List<User> Saved { get; } = [];

        public Task<User?> FindByEmailAsync(Guid tenantId, Email email, CancellationToken ct)
            => Task.FromResult(Saved.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email));

        public Task AddAsync(User user, CancellationToken ct) { Saved.Add(user); return Task.CompletedTask; }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public HashSet<string> AcceptPlaintexts { get; } = [];

        public string Hash(string plaintext) => $"HASH:{plaintext}";
        public bool Verify(string plaintext, string hash) => AcceptPlaintexts.Contains(plaintext);
    }

    private sealed class FakeIssuer : IAccessTokenIssuer
    {
        public List<User> Issued { get; } = [];

        public AccessToken Issue(User user)
        {
            Issued.Add(user);
            return new AccessToken("issued-token", DateTimeOffset.UtcNow.AddMinutes(15));
        }
    }
}
