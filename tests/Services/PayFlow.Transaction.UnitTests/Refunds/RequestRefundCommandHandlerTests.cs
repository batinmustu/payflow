using MediatR;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Application.Refunds.RequestRefund;
using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.UnitTests.Refunds;

public class RequestRefundCommandHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeTransactions _txs = new();
    private readonly FakeRefunds _refunds = new();
    private readonly FakeUow _uow = new();

    private IRequestHandler<RequestRefundCommand, Result<RequestRefundResponse>> Handler()
        => (IRequestHandler<RequestRefundCommand, Result<RequestRefundResponse>>)
            Activator.CreateInstance(
                typeof(RequestRefundCommandHandler),
                _txs, _refunds, _uow)!;

    private static TransactionAggregate Captured(Guid tenantId, long amount = 20000, string currency = "TRY")
    {
        var t = TransactionAggregate.Initiate(tenantId, "ORD-R", amount, currency).Value;
        t.MarkCaptured("stripe", "ch_test");
        return t;
    }

    [Fact]
    public async Task Happy_path_creates_refund_and_persists()
    {
        var tx = Captured(Tenant);
        _txs.Saved.Add(tx);

        var cmd = new RequestRefundCommand(Tenant, tx.Id, 5000, "ops@acme.test");
        var result = await Handler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Requested");
        result.Value.AmountMinor.Should().Be(5000);
        result.Value.Currency.Should().Be("TRY");
        _refunds.Added.Should().ContainSingle();
        _uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Unknown_transaction_returns_TRANSACTION_NOT_FOUND()
    {
        var cmd = new RequestRefundCommand(Tenant, Guid.NewGuid(), 5000, "ops@acme.test");
        var result = await Handler().Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TRANSACTION_NOT_FOUND");
        _refunds.Added.Should().BeEmpty();
        _uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Non_captured_transaction_returns_REFUND_NOT_ALLOWED_IN_STATE()
    {
        var tx = TransactionAggregate.Initiate(Tenant, "ORD-R-init", 10000, "TRY").Value;
        _txs.Saved.Add(tx);

        var cmd = new RequestRefundCommand(Tenant, tx.Id, 1000, "ops@acme.test");
        var result = await Handler().Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("REFUND_NOT_ALLOWED_IN_STATE");
    }

    [Fact]
    public async Task Amount_exceeding_remaining_returns_REFUND_AMOUNT_EXCEEDS_REMAINING()
    {
        var tx = Captured(Tenant, amount: 10000);
        _txs.Saved.Add(tx);

        var cmd = new RequestRefundCommand(Tenant, tx.Id, 10001, "ops@acme.test");
        var result = await Handler().Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("REFUND_AMOUNT_EXCEEDS_REMAINING");
    }

    [Fact]
    public async Task In_flight_refund_counts_against_remaining_balance()
    {
        var tx = Captured(Tenant, amount: 10000);
        _txs.Saved.Add(tx);
        // A 6000 refund is already Requested (not yet completed) for this tx.
        _refunds.OutstandingByTx[(Tenant, tx.Id)] = 6000;

        var cmd = new RequestRefundCommand(Tenant, tx.Id, 5000, "ops@acme.test");
        var result = await Handler().Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("REFUND_AMOUNT_EXCEEDS_REMAINING");
    }

    [Fact]
    public async Task Exact_remaining_amount_is_allowed()
    {
        var tx = Captured(Tenant, amount: 10000);
        _txs.Saved.Add(tx);
        _refunds.OutstandingByTx[(Tenant, tx.Id)] = 6000;

        var cmd = new RequestRefundCommand(Tenant, tx.Id, 4000, "ops@acme.test");
        var result = await Handler().Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AmountMinor.Should().Be(4000);
    }

    // ---- Fakes ----

    private sealed class FakeTransactions : ITransactionRepository
    {
        public List<TransactionAggregate> Saved { get; } = [];

        public Task<bool> OrderReferenceExistsAsync(Guid tenantId, string orderReference, CancellationToken ct)
            => Task.FromResult(false);

        public Task<TransactionAggregate?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Saved.FirstOrDefault(t => t.Id == id && t.TenantId == tenantId));

        public Task AddAsync(TransactionAggregate transaction, CancellationToken ct)
        {
            Saved.Add(transaction);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefunds : IRefundRepository
    {
        public List<Refund> Added { get; } = [];
        public Dictionary<(Guid Tenant, Guid Tx), long> OutstandingByTx { get; } = new();

        public Task<Refund?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Added.FirstOrDefault(r => r.Id == id && r.TenantId == tenantId));

        public Task<long> SumOutstandingAmountAsync(Guid tenantId, Guid transactionId, CancellationToken ct)
            => Task.FromResult(OutstandingByTx.GetValueOrDefault((tenantId, transactionId)));

        public Task AddAsync(Refund refund, CancellationToken ct)
        {
            Added.Add(refund);
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
}
