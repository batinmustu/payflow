using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Infrastructure.Providers;

namespace PayFlow.Payment.UnitTests.Providers;

public class PaymentProviderFactoryTests
{
    private static PaymentProviderFactory Factory() => new(new IPaymentProvider[]
    {
        new StripeMockProvider(),
        new IyzicoMockProvider(),
        new PayPalMockProvider(),
    });

    [Theory]
    [InlineData("stripe")]
    [InlineData("iyzico")]
    [InlineData("paypal")]
    [InlineData("STRIPE")]    // case-insensitive on input
    public void Resolves_known_codes(string code)
    {
        var result = Factory().Resolve(code);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(code.ToLowerInvariant());
    }

    [Fact]
    public void Returns_PROVIDER_NOT_CONFIGURED_for_unknown_code()
    {
        var result = Factory().Resolve("garbage");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PROVIDER_NOT_CONFIGURED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Returns_PROVIDER_CODE_REQUIRED_for_blank(string? code)
    {
        var result = Factory().Resolve(code!);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PROVIDER_CODE_REQUIRED");
    }
}
