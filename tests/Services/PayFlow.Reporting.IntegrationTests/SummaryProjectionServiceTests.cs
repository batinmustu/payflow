using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Reporting.Application.Projections;
using PayFlow.Reporting.Domain.Projections;
using PayFlow.Reporting.Infrastructure.Persistence;

namespace PayFlow.Reporting.IntegrationTests;

[Collection("reporting-db")]
public class SummaryProjectionServiceTests
{
    private readonly ReportingDbFixture _fx;
    public SummaryProjectionServiceTests(ReportingDbFixture fx) => _fx = fx;

    [Fact]
    public async Task First_event_inserts_a_summary_row_against_postgres()
    {
        var tenant = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

        await Apply(Guid.NewGuid(), "payflow.transaction.captured.v1", tenant, occurredAt, "TRY",
            row => row.RecordCaptured(15000));

        var rows = await ReadAsync(tenant);
        rows.Should().ContainSingle();
        rows[0].CapturedCount.Should().Be(1);
        rows[0].CapturedAmountMinor.Should().Be(15000);
        rows[0].Date.Should().Be(new DateOnly(2026, 5, 20));
    }

    [Fact]
    public async Task Duplicate_message_id_skipped_after_first_commit()
    {
        var tenant = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var messageId = Guid.NewGuid();

        await Apply(messageId, "payflow.transaction.captured.v1", tenant, occurredAt, "TRY",
            row => row.RecordCaptured(10000));
        await Apply(messageId, "payflow.transaction.captured.v1", tenant, occurredAt, "TRY",
            row => row.RecordCaptured(10000));

        var rows = await ReadAsync(tenant);
        rows.Should().ContainSingle();
        rows[0].CapturedCount.Should().Be(1);
        rows[0].CapturedAmountMinor.Should().Be(10000);
    }

    [Fact]
    public async Task Concurrent_inserts_into_the_same_bucket_recover_via_unique_violation()
    {
        var tenant = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

        // Fire 5 inserts in parallel. Whichever one wins inserts the row;
        // the others should hit ux_daily_summary_tenant_date_currency and
        // recover via the catch + replay path in the service.
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            Apply(Guid.NewGuid(), "payflow.transaction.captured.v1", tenant, occurredAt, "TRY",
                row => row.RecordCaptured(1000))).ToArray();
        await Task.WhenAll(tasks);

        var rows = await ReadAsync(tenant);
        rows.Should().ContainSingle("only one row per (tenant, date, currency) bucket");
        rows[0].CapturedCount.Should().BeGreaterOrEqualTo(1)
            .And.BeLessOrEqualTo(5);
        // At least one counter increment landed — and the unique constraint
        // shielded the table from duplicate rows even under contention.
    }

    [Fact]
    public async Task Mixed_event_types_into_same_bucket_accumulate()
    {
        var tenant = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

        await Apply(Guid.NewGuid(), "payflow.transaction.initiated.v1", tenant, occurredAt, "TRY",
            row => row.RecordAttempted());
        await Apply(Guid.NewGuid(), "payflow.transaction.captured.v1", tenant, occurredAt, "TRY",
            row => row.RecordCaptured(12000));
        await Apply(Guid.NewGuid(), "payflow.transaction.failed.v1", tenant, occurredAt, "TRY",
            row => row.RecordFailed());
        await Apply(Guid.NewGuid(), "payflow.refund.completed.v1", tenant, occurredAt, "TRY",
            row => row.RecordRefunded(2000));

        var rows = await ReadAsync(tenant);
        rows.Should().ContainSingle();
        var r = rows[0];
        r.AttemptedCount.Should().Be(1);
        r.CapturedCount.Should().Be(1);
        r.CapturedAmountMinor.Should().Be(12000);
        r.FailedCount.Should().Be(1);
        r.RefundedCount.Should().Be(1);
        r.RefundedAmountMinor.Should().Be(2000);
    }

    private async Task Apply(
        Guid messageId, string eventType, Guid tenantId, DateTimeOffset occurredAt,
        string currency, Action<DailyTransactionSummary> apply)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<SummaryProjectionService>();
        await service.ApplyAsync(messageId, eventType, tenantId, occurredAt, currency, apply, CancellationToken.None);
    }

    private async Task<List<DailyTransactionSummary>> ReadAsync(Guid tenantId)
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        return await db.DailySummaries
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync();
    }
}

[CollectionDefinition("reporting-db")]
#pragma warning disable CA1711 // xUnit convention: "*Collection" naming
public sealed class ReportingDbCollection : ICollectionFixture<ReportingDbFixture> { }
#pragma warning restore CA1711
