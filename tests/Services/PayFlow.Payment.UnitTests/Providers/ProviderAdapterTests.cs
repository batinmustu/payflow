using PayFlow.Payment.Infrastructure.Providers;

namespace PayFlow.Payment.UnitTests.Providers;

public class ProviderAdapterTests
{
    private static PaymentRequest ARequest(long amount = 14990) => PaymentRequest.Create(
        tenantId: Guid.NewGuid(),
        transactionId: Guid.NewGuid(),
        providerCode: "any",
        amountMinor: amount,
        currency: "TRY",
        cardToken: "tok_visa").Value;

    [Fact]
    public async Task Stripe_mock_captures_in_a_single_call()
    {
        var provider = new StripeMockProvider();

        var result = await provider.ChargeAsync(ARequest(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Captured);
        result.ProviderCode.Should().Be("stripe");
        result.ProviderReference.Should().StartWith("ch_");
        result.DeclineCode.Should().BeNull();
        result.PaymentId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Iyzico_mock_authorises_but_does_not_capture_in_a_single_call()
    {
        var provider = new IyzicoMockProvider();

        var result = await provider.ChargeAsync(ARequest(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Authorized);
        result.ProviderCode.Should().Be("iyzico");
        result.ProviderReference.Should().StartWith("iyz_");
    }

    [Theory]
    [InlineData(14990, PaymentStatus.Captured)]
    [InlineData(10013, PaymentStatus.SoftDeclined)]
    [InlineData(20099, PaymentStatus.HardDeclined)]
    public async Task PayPal_mock_dispatches_by_amount_last_two_digits(long amount, PaymentStatus expected)
    {
        var provider = new PayPalMockProvider();

        var result = await provider.ChargeAsync(ARequest(amount), CancellationToken.None);

        result.Status.Should().Be(expected);
        result.ProviderCode.Should().Be("paypal");
        if (expected != PaymentStatus.Captured)
        {
            result.ProviderReference.Should().BeNull();
            result.DeclineCode.Should().NotBeNullOrEmpty();
        }
    }
}
