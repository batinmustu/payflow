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
        // Default FakeRouting is "stripe"; the handler tries it and the fake
        // sends back a HardDeclined response.
        _payments.NextResponse = new PaymentChargeResponse(
            Guid.NewGuid(), "stripe", "HardDeclined", null, "DO_NOT_HONOR", 80);

        var result = await Handler().Handle(AValidCommand("ORD-h"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        result.Value.FailureReason.Should().Be(FailureReason.HardDeclined);
        result.Value.ProviderCode.Should().Be("stripe");
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
    public async Task PaymentClientException_with_no_remaining_providers_lands_on_RoutingExhausted()
    {
        _payments.ThrowOnNextCall = new PaymentClientException("connection refused");

        var result = await Handler().Handle(AValidCommand("ORD-x"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        result.Value.FailureReason.Should().Be(FailureReason.RoutingExhausted);
        result.Value.Attempts.Should().ContainSingle()
            .Which.Result.Should().Be("TransportError");
    }

    [Fact]
    public async Task Soft_decline_on_first_provider_falls_over_to_second()
    {
        var routing = new FakeRouting("paypal", "stripe");
        _payments.ResponsesByProvider["paypal"] =
            new PaymentChargeResponse(Guid.NewGuid(), "paypal", "SoftDeclined", null, "INSUFFICIENT_FUNDS", 80);
        _payments.ResponsesByProvider["stripe"] =
            new PaymentChargeResponse(Guid.NewGuid(), "stripe", "Captured", "ch_after_failover", null, 110);
        var handler = (IRequestHandler<CreateTransactionCommand, Result<CreateTransactionResponse>>)
            Activator.CreateInstance(typeof(CreateTransactionCommandHandler), _txs, _uow, _payments, routing)!;

        var result = await handler.Handle(AValidCommand("ORD-failover"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Captured");
        result.Value.ProviderCode.Should().Be("stripe");
        result.Value.ProviderReference.Should().Be("ch_after_failover");
        result.Value.Attempts.Should().HaveCount(2);
        result.Value.Attempts[0].Result.Should().Be("SoftDeclined");
        result.Value.Attempts[0].ProviderCode.Should().Be("paypal");
        result.Value.Attempts[1].Result.Should().Be("Captured");
        result.Value.Attempts[1].ProviderCode.Should().Be("stripe");
        _payments.AllRequests.Select(r => r.ProviderCode).Should().Equal("paypal", "stripe");
    }

    [Fact]
    public async Task Hard_decline_stops_the_chain_even_with_more_providers_available()
    {
        var routing = new FakeRouting("paypal", "stripe");
        _payments.ResponsesByProvider["paypal"] =
            new PaymentChargeResponse(Guid.NewGuid(), "paypal", "HardDeclined", null, "DO_NOT_HONOR", 60);
        // Stripe would Capture if asked, but a HardDecline stops the chain.
        _payments.ResponsesByProvider["stripe"] =
            new PaymentChargeResponse(Guid.NewGuid(), "stripe", "Captured", "ch_never", null, 110);
        var handler = (IRequestHandler<CreateTransactionCommand, Result<CreateTransactionResponse>>)
            Activator.CreateInstance(typeof(CreateTransactionCommandHandler), _txs, _uow, _payments, routing)!;

        var result = await handler.Handle(AValidCommand("ORD-hard"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        result.Value.FailureReason.Should().Be(FailureReason.HardDeclined);
        result.Value.Attempts.Should().ContainSingle();
        result.Value.Attempts[0].ProviderCode.Should().Be("paypal");
        _payments.AllRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task Every_provider_soft_declining_lands_on_RoutingExhausted()
    {
        var routing = new FakeRouting("paypal", "stripe");
        _payments.ResponsesByProvider["paypal"] =
            new PaymentChargeResponse(Guid.NewGuid(), "paypal", "SoftDeclined", null, "INSUFFICIENT_FUNDS", 80);
        _payments.ResponsesByProvider["stripe"] =
            new PaymentChargeResponse(Guid.NewGuid(), "stripe", "SoftDeclined", null, "INSUFFICIENT_FUNDS", 90);
        var handler = (IRequestHandler<CreateTransactionCommand, Result<CreateTransactionResponse>>)
            Activator.CreateInstance(typeof(CreateTransactionCommandHandler), _txs, _uow, _payments, routing)!;

        var result = await handler.Handle(AValidCommand("ORD-exhausted"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        result.Value.FailureReason.Should().Be(FailureReason.RoutingExhausted);
        result.Value.Attempts.Should().HaveCount(2);
        result.Value.Attempts.Should().AllSatisfy(a => a.Result.Should().Be("SoftDeclined"));
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
        public List<PaymentChargeRequest> AllRequests { get; } = [];

        /// <summary>Per-provider canned response. Falls back to NextResponse when no entry matches.</summary>
        public Dictionary<string, PaymentChargeResponse> ResponsesByProvider { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Per-provider exception (e.g. PaymentClientException) to throw on the call.</summary>
        public Dictionary<string, Exception> ThrowByProvider { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<PaymentChargeResponse> ChargeAsync(PaymentChargeRequest request, CancellationToken ct)
        {
            LastRequest = request;
            AllRequests.Add(request);

            if (ThrowByProvider.TryGetValue(request.ProviderCode, out var providerEx)) throw providerEx;
            if (ThrowOnNextCall is { } ex) throw ex;

            if (ResponsesByProvider.TryGetValue(request.ProviderCode, out var canned))
            {
                return Task.FromResult(canned);
            }
            return Task.FromResult(NextResponse);
        }
    }

    private sealed class FakeRouting : IRoutingPolicy
    {
        private readonly IReadOnlyList<string> _order;
        public FakeRouting(params string[] order) => _order = order;
        public IReadOnlyList<string> ResolveOrder(Guid tenantId) => _order;
    }
}
