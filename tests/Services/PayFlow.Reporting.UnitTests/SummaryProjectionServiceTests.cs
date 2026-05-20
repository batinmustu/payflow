using Microsoft.Extensions.Logging.Abstractions;

namespace PayFlow.Reporting.UnitTests;

public class SummaryProjectionServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeSummaries _summaries = new();
    private readonly FakeProcessed _processed = new();
    private readonly FakeUow _uow = new();

    private SummaryProjectionService NewService() =>
        new(_summaries, _processed, _uow, NullLogger<SummaryProjectionService>.Instance);

    [Fact]
    public async Task First_event_inserts_a_fresh_summary_row()
    {
        await NewService().ApplyAsync(
            messageId: Guid.NewGuid(),
            eventType: "payflow.transaction.captured.v1",
            tenantId: Tenant,
            occurredAt: new DateTimeOffset(2026, 5, 20, 13, 0, 0, TimeSpan.Zero),
            currency: "TRY",
            apply: r => r.RecordCaptured(15000),
            ct: CancellationToken.None);

        _summaries.Added.Should().ContainSingle();
        var row = _summaries.Added[0];
        row.CapturedCount.Should().Be(1);
        row.CapturedAmountMinor.Should().Be(15000);
        row.Date.Should().Be(new DateOnly(2026, 5, 20));
        _uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Second_event_on_same_day_currency_updates_the_existing_row()
    {
        var ts = new DateTimeOffset(2026, 5, 20, 13, 0, 0, TimeSpan.Zero);
        var service = NewService();

        await service.ApplyAsync(Guid.NewGuid(), "payflow.transaction.captured.v1",
            Tenant, ts, "TRY", r => r.RecordCaptured(10000), CancellationToken.None);
        await service.ApplyAsync(Guid.NewGuid(), "payflow.transaction.captured.v1",
            Tenant, ts, "TRY", r => r.RecordCaptured(5000), CancellationToken.None);

        _summaries.Added.Should().ContainSingle();
        _summaries.Added[0].CapturedCount.Should().Be(2);
        _summaries.Added[0].CapturedAmountMinor.Should().Be(15000);
        _uow.SaveCount.Should().Be(2);
    }

    [Fact]
    public async Task Different_currencies_get_separate_buckets()
    {
        var ts = new DateTimeOffset(2026, 5, 20, 13, 0, 0, TimeSpan.Zero);
        var service = NewService();

        await service.ApplyAsync(Guid.NewGuid(), "payflow.transaction.captured.v1",
            Tenant, ts, "TRY", r => r.RecordCaptured(10000), CancellationToken.None);
        await service.ApplyAsync(Guid.NewGuid(), "payflow.transaction.captured.v1",
            Tenant, ts, "USD", r => r.RecordCaptured(500), CancellationToken.None);

        _summaries.Added.Should().HaveCount(2);
        _summaries.Added.Should().ContainSingle(r => r.Currency == "TRY");
        _summaries.Added.Should().ContainSingle(r => r.Currency == "USD");
    }

    [Fact]
    public async Task Duplicate_messageId_is_skipped()
    {
        var ts = new DateTimeOffset(2026, 5, 20, 13, 0, 0, TimeSpan.Zero);
        var messageId = Guid.NewGuid();
        var service = NewService();

        await service.ApplyAsync(messageId, "payflow.transaction.captured.v1",
            Tenant, ts, "TRY", r => r.RecordCaptured(7000), CancellationToken.None);
        await service.ApplyAsync(messageId, "payflow.transaction.captured.v1",
            Tenant, ts, "TRY", r => r.RecordCaptured(7000), CancellationToken.None);

        _summaries.Added.Should().ContainSingle();
        _summaries.Added[0].CapturedCount.Should().Be(1);
        _summaries.Added[0].CapturedAmountMinor.Should().Be(7000);
        _uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UTC_date_bucket_uses_the_event_timestamp()
    {
        var lateNight = new DateTimeOffset(2026, 5, 20, 23, 30, 0, TimeSpan.Zero);
        var earlyMorning = new DateTimeOffset(2026, 5, 21, 0, 30, 0, TimeSpan.Zero);
        var service = NewService();

        await service.ApplyAsync(Guid.NewGuid(), "payflow.transaction.initiated.v1",
            Tenant, lateNight, "TRY", r => r.RecordAttempted(), CancellationToken.None);
        await service.ApplyAsync(Guid.NewGuid(), "payflow.transaction.initiated.v1",
            Tenant, earlyMorning, "TRY", r => r.RecordAttempted(), CancellationToken.None);

        _summaries.Added.Should().HaveCount(2);
        _summaries.Added.Select(r => r.Date).Should()
            .BeEquivalentTo([new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 21)]);
    }

    // ---- Fakes ----

    private sealed class FakeSummaries : ISummaryRepository
    {
        public List<DailyTransactionSummary> Added { get; } = [];

        public Task<DailyTransactionSummary?> GetAsync(Guid tenantId, DateOnly bucketDate, string currency, CancellationToken ct)
        {
            var normalised = currency.Trim().ToUpperInvariant();
            var match = Added.FirstOrDefault(s =>
                s.TenantId == tenantId && s.Date == bucketDate && s.Currency == normalised);
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<DailyTransactionSummary>> ListAsync(
            Guid tenantId, DateOnly fromDate, DateOnly toDate, string? currency, CancellationToken ct)
        {
            IReadOnlyList<DailyTransactionSummary> rows = Added
                .Where(s => s.TenantId == tenantId && s.Date >= fromDate && s.Date <= toDate)
                .ToList();
            return Task.FromResult(rows);
        }

        public Task AddAsync(DailyTransactionSummary row, CancellationToken ct)
        {
            Added.Add(row);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProcessed : IProcessedEventStore
    {
        public HashSet<Guid> Seen { get; } = [];

        public Task<bool> WasProcessedAsync(Guid messageId, CancellationToken ct) =>
            Task.FromResult(Seen.Contains(messageId));

        public Task AddAsync(ProcessedEvent marker, CancellationToken ct)
        {
            Seen.Add(marker.MessageId);
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
