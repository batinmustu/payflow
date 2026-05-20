using MediatR;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Application.Transactions.CreateTransaction;

namespace PayFlow.Transaction.UnitTests.Transactions;

public class CreateTransactionCommandHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeTransactions _txs = new();
    private readonly FakeUow _uow = new();
    private readonly FakePaymentClient _payments = new();
    private readonly FakeRouting _routing = new("stripe");

    private IRequestHandler<CreateTransactionCommand, Result<CreateTransactionResponse>> Handler()
        => (IRequestHandler<CreateTransactionCommand, Result<CreateTransactionResponse>>)
            Activator.CreateInstance(
                typeof(CreateTransactionCommandHandler),
                _txs, _uow, _payments, _routing)!;

    private static CreateTransactionCommand AValidCommand(string orderRef = "ORD-001") => new(
        TenantId: Tenant,
        OrderReference: orderRef,
        AmountMinor: 14990,
        Currency: "TRY",
        CardToken: "tok_visa");

    [Fact]
    public async Task Captured_payment_response_marks_transaction_Captured()
    {
        _payments.NextResponse = new PaymentChargeResponse(
            PaymentId: Guid.NewGuid(),
            ProviderCode: "stripe",
            Status: "Captured",
            ProviderReference: "ch_abc",
            DeclineCode: null,
            LatencyMilliseconds: 100);

        var result = await Handler().Handle(AValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Captured");
        result.Value.ProviderCode.Should().Be("stripe");
        result.Value.ProviderReference.Should().Be("ch_abc");
        _txs.Saved.Should().ContainSingle();
        _uow.SaveCount.Should().Be(2); // initiate + finalisation
    }

    [Fact]
    public async Task HardDeclined_response_marks_transaction_Failed_with_HardDeclined()
    {
        _payments.NextResponse = new PaymentChargeResponse(
            Guid.NewGuid(), "paypal", "HardDeclined", null, "DO_NOT_HONOR", 80);

        var result = await Handler().Handle(AValidCommand("ORD-h"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        result.Value.FailureReason.Should().Be(FailureReason.HardDeclined);
        result.Value.ProviderCode.Should().Be("paypal");
    }

    [Fact]
    public async Task SoftDeclined_response_marks_transaction_Failed_with_RoutingExhausted()
    {
        _payments.NextResponse = new PaymentChargeResponse(
            Guid.NewGuid(), "paypal", "SoftDeclined", null, "INSUFFICIENT_FUNDS", 60);

        var result = await Handler().Handle(AValidCommand("ORD-s"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        result.Value.FailureReason.Should().Be(FailureReason.RoutingExhausted);
    }

    [Fact]
    public async Task PaymentClientException_marks_transaction_Failed_with_ProviderError()
    {
        _payments.ThrowOnNextCall = new PaymentClientException("connection refused");

        var result = await Handler().Handle(AValidCommand("ORD-x"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        result.Value.FailureReason.Should().StartWith(FailureReason.ProviderError);
    }

    [Fact]
    public async Task Duplicate_order_reference_short_circuits_before_payment()
    {
        _txs.ReservedReferences.Add((Tenant, "ORD-dup"));

        var result = await Handler().Handle(AValidCommand("ORD-dup"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ORDER_REFERENCE_ALREADY_USED");
        _payments.LastRequest.Should().BeNull();
        _uow.SaveCount.Should().Be(0);
    }

    // ---- Fakes ----

    private sealed class FakeTransactions : ITransactionRepository
    {
        public List<TransactionAggregate> Saved { get; } = [];
        public HashSet<(Guid Tenant, string Ref)> ReservedReferences { get; } = [];

        public Task<bool> OrderReferenceExistsAsync(Guid tenantId, string orderReference, CancellationToken ct)
            => Task.FromResult(ReservedReferences.Contains((tenantId, orderReference.Trim())));

        public Task<TransactionAggregate?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Saved.FirstOrDefault(t => t.Id == id && t.TenantId == tenantId));

        public Task AddAsync(TransactionAggregate transaction, CancellationToken ct)
        {
            Saved.Add(transaction);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakePaymentClient : IPaymentClient
    {
        public PaymentChargeResponse NextResponse { get; set; } = new(
            Guid.NewGuid(), "stripe", "Captured", "ch_default", null, 100);
        public PaymentClientException? ThrowOnNextCall { get; set; }
        public PaymentChargeRequest? LastRequest { get; private set; }

        public Task<PaymentChargeResponse> ChargeAsync(PaymentChargeRequest request, CancellationToken ct)
        {
            LastRequest = request;
            if (ThrowOnNextCall is { } ex) throw ex;
            return Task.FromResult(NextResponse);
        }
    }

    private sealed class FakeRouting : IRoutingPolicy
    {
        private readonly string _provider;
        public FakeRouting(string provider) => _provider = provider;
        public string PickProvider(Guid tenantId) => _provider;
    }
}
