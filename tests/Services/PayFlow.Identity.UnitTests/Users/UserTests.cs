using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.UnitTests.Users;

public class UserTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static Email AnEmail() => Email.Create("user@example.com").Value;

    [Fact]
    public void Create_succeeds_with_valid_input_and_raises_event()
    {
        var result = User.Create(TenantId, AnEmail(), "hash", "Display Name");

        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Id.Should().NotBe(Guid.Empty);
        user.TenantId.Should().Be(TenantId);
        user.Email.Value.Should().Be("user@example.com");
        user.PasswordHash.Should().Be("hash");
        user.DisplayName.Should().Be("Display Name");
        user.Roles.Should().BeEmpty();
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserCreatedDomainEvent>();
    }

    [Fact]
    public void Create_trims_display_name()
    {
        var user = User.Create(TenantId, AnEmail(), "hash", "  Display  ").Value;

        user.DisplayName.Should().Be("Display");
    }

    [Fact]
    public void Create_rejects_empty_tenant()
    {
        var result = User.Create(Guid.Empty, AnEmail(), "hash", "Display");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TENANT_REQUIRED");
    }

    [Theory]
    [InlineData(null, "PASSWORD_HASH_REQUIRED")]
    [InlineData("", "PASSWORD_HASH_REQUIRED")]
    [InlineData("   ", "PASSWORD_HASH_REQUIRED")]
    public void Create_requires_password_hash(string? passwordHash, string expectedCode)
    {
        var result = User.Create(TenantId, AnEmail(), passwordHash!, "Display");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_display_name(string? displayName)
    {
        var result = User.Create(TenantId, AnEmail(), "hash", displayName!);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DISPLAY_NAME_REQUIRED");
    }

    [Fact]
    public void GrantRole_adds_distinct_role()
    {
        var user = User.Create(TenantId, AnEmail(), "hash", "Display").Value;

        user.GrantRole("admin");
        user.GrantRole("admin");
        user.GrantRole("ADMIN"); // case-insensitive

        user.Roles.Should().Equal("admin");
    }

    [Fact]
    public void RevokeRole_removes_existing_role_case_insensitively()
    {
        var user = User.Create(TenantId, AnEmail(), "hash", "Display").Value;
        user.GrantRole("admin");
        user.GrantRole("viewer");

        user.RevokeRole("ADMIN");

        user.Roles.Should().Equal("viewer");
    }

    [Fact]
    public void ChangeDisplayName_updates_and_trims()
    {
        var user = User.Create(TenantId, AnEmail(), "hash", "Old").Value;

        user.ChangeDisplayName("  New Name  ");

        user.DisplayName.Should().Be("New Name");
    }

    [Fact]
    public void ChangeDisplayName_throws_on_blank()
    {
        var user = User.Create(TenantId, AnEmail(), "hash", "Old").Value;

        var act = () => user.ChangeDisplayName("   ");

        act.Should().Throw<ArgumentException>();
    }
}
