namespace PayFlow.Transaction.UnitTests.Transactions;

public class TransactionTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Initiate_succeeds_with_valid_input_and_raises_event()
    {
        var result = TransactionAggregate.Initiate(TenantId, "ORD-001", 14990, "try");

        result.IsSuccess.Should().BeTrue();
        var tx = result.Value;
        tx.Id.Should().NotBe(Guid.Empty);
        tx.TenantId.Should().Be(TenantId);
        tx.OrderReference.Should().Be("ORD-001");
        tx.AmountMinor.Should().Be(14990);
        tx.Currency.Should().Be("TRY");
        tx.State.Should().Be(TransactionState.Initiated);
        tx.FinalProviderCode.Should().BeNull();
        tx.ProviderReference.Should().BeNull();
        tx.CapturedAt.Should().BeNull();
        tx.FailedAt.Should().BeNull();
        tx.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        tx.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TransactionInitiatedDomainEvent>();
    }

    [Fact]
    public void Initiate_rejects_empty_tenant()
    {
        var result = TransactionAggregate.Initiate(Guid.Empty, "ORD", 100, "TRY");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TENANT_REQUIRED");
    }

    [Theory]
    [InlineData(null, "ORDER_REFERENCE_REQUIRED")]
    [InlineData("", "ORDER_REFERENCE_REQUIRED")]
    [InlineData("   ", "ORDER_REFERENCE_REQUIRED")]
    public void Initiate_requires_an_order_reference(string? orderRef, string expected)
    {
        var result = TransactionAggregate.Initiate(TenantId, orderRef!, 100, "TRY");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Initiate_rejects_non_positive_amount(long amount)
    {
        var result = TransactionAggregate.Initiate(TenantId, "ORD", amount, "TRY");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AMOUNT_INVALID");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("XX")]
    [InlineData("TURK")]
    public void Initiate_rejects_invalid_currency(string? currency)
    {
        var result = TransactionAggregate.Initiate(TenantId, "ORD", 100, currency!);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CURRENCY_NOT_SUPPORTED");
    }

    [Fact]
    public void MarkCaptured_from_Initiated_moves_to_Captured_and_raises_event()
    {
        var tx = TransactionAggregate.Initiate(TenantId, "ORD", 100, "TRY").Value;
        tx.ClearDomainEvents();

        tx.MarkCaptured("stripe", "ch_abc");

        tx.State.Should().Be(TransactionState.Captured);
        tx.FinalProviderCode.Should().Be("stripe");
        tx.ProviderReference.Should().Be("ch_abc");
        tx.CapturedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        tx.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TransactionCapturedDomainEvent>();
    }

    [Fact]
    public void MarkCaptured_throws_when_not_in_Initiated()
    {
        var tx = TransactionAggregate.Initiate(TenantId, "ORD", 100, "TRY").Value;
        tx.MarkCaptured("stripe", "ch_abc");

        var act = () => tx.MarkCaptured("stripe", "ch_xyz");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_from_Initiated_moves_to_Failed_and_raises_event()
    {
        var tx = TransactionAggregate.Initiate(TenantId, "ORD", 100, "TRY").Value;
        tx.ClearDomainEvents();

        tx.MarkFailed(FailureReason.HardDeclined, providerCodeAttempted: "paypal");

        tx.State.Should().Be(TransactionState.Failed);
        tx.FailureReason.Should().Be(FailureReason.HardDeclined);
        tx.FinalProviderCode.Should().Be("paypal");
        tx.FailedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        tx.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TransactionFailedDomainEvent>();
    }

    [Fact]
    public void MarkFailed_throws_when_already_captured()
    {
        var tx = TransactionAggregate.Initiate(TenantId, "ORD", 100, "TRY").Value;
        tx.MarkCaptured("stripe", "ch");

        var act = () => tx.MarkFailed(FailureReason.ProviderError);

        act.Should().Throw<InvalidOperationException>();
    }
}
