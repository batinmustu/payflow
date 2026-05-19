namespace PayFlow.Payment.UnitTests;

public class PaymentRequestTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TransactionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Create_succeeds_with_valid_input_and_normalises_provider_and_currency()
    {
        var result = PaymentRequest.Create(TenantId, TransactionId, "STRIPE", 14990, "try", "tok_visa");

        result.IsSuccess.Should().BeTrue();
        var req = result.Value;
        req.ProviderCode.Should().Be("stripe");
        req.Currency.Should().Be("TRY");
        req.AmountMinor.Should().Be(14990);
        req.CardToken.Should().Be("tok_visa");
    }

    [Fact]
    public void Create_rejects_empty_tenant()
    {
        var result = PaymentRequest.Create(Guid.Empty, TransactionId, "stripe", 100, "TRY", "tok");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TENANT_REQUIRED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_provider_code(string? providerCode)
    {
        var result = PaymentRequest.Create(TenantId, TransactionId, providerCode!, 100, "TRY", "tok");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PROVIDER_CODE_REQUIRED");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-9999)]
    public void Create_rejects_non_positive_amount(long amount)
    {
        var result = PaymentRequest.Create(TenantId, TransactionId, "stripe", amount, "TRY", "tok");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AMOUNT_INVALID");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("TURK")]
    public void Create_rejects_currency_not_three_letters(string? currency)
    {
        var result = PaymentRequest.Create(TenantId, TransactionId, "stripe", 100, currency!, "tok");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CURRENCY_NOT_SUPPORTED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_card_token(string? token)
    {
        var result = PaymentRequest.Create(TenantId, TransactionId, "stripe", 100, "TRY", token!);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CARD_TOKEN_INVALID");
    }
}
