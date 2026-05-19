using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.UnitTests.Users;

public class EmailTests
{
    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("USER@Example.COM", "user@example.com")]
    [InlineData("  spaced@example.com  ", "spaced@example.com")]
    public void Create_normalises_to_trimmed_lowercase(string input, string expected)
    {
        var result = Email.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
        result.Value.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_returns_EMAIL_REQUIRED_when_blank(string? input)
    {
        var result = Email.Create(input!);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("EMAIL_REQUIRED");
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@nostart.com")]
    [InlineData("noend@")]
    [InlineData("nodot@localhost")]
    [InlineData("trailing.@example.com.")]
    public void Create_returns_EMAIL_INVALID_for_malformed_input(string input)
    {
        var result = Email.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("EMAIL_INVALID");
    }

    [Fact]
    public void Two_emails_with_the_same_value_are_equal()
    {
        var a = Email.Create("user@example.com").Value;
        var b = Email.Create("USER@example.com").Value;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
