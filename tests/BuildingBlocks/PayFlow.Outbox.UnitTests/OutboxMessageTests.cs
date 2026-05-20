namespace PayFlow.Outbox.UnitTests;

public class OutboxMessageTests
{
    private static OutboxMessage AMessage() => OutboxMessage.Create(
        tenantId: Guid.NewGuid(),
        aggregateType: "Transaction",
        aggregateId: Guid.NewGuid(),
        eventType: "payflow.transaction.captured.v1",
        payload: "{\"a\":1}",
        headers: "{\"correlation_id\":\"abc\"}");

    [Fact]
    public void Create_initialises_Pending_with_now_timestamps()
    {
        var msg = AMessage();

        msg.State.Should().Be(OutboxMessageState.Pending);
        msg.AttemptCount.Should().Be(0);
        msg.PublishedAt.Should().BeNull();
        msg.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        msg.NextAttemptAt.Should().BeCloseTo(msg.CreatedAt, TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Create_defaults_headers_to_an_empty_object_when_null()
    {
        var msg = OutboxMessage.Create(Guid.NewGuid(), "X", Guid.NewGuid(), "evt", "{}", null!);

        msg.Headers.Should().Be("{}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_event_type(string? eventType)
    {
        var act = () => OutboxMessage.Create(Guid.NewGuid(), "X", Guid.NewGuid(), eventType!, "{}", "{}");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkPublished_clears_error_and_stamps_timestamp()
    {
        var msg = AMessage();
        msg.MarkPublishing();

        msg.MarkPublished();

        msg.State.Should().Be(OutboxMessageState.Published);
        msg.PublishedAt.Should().NotBeNull();
        msg.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_increments_attempt_and_pushes_next_attempt_forward()
    {
        var msg = AMessage();

        msg.MarkFailed("connection refused", TimeSpan.FromSeconds(30));

        msg.State.Should().Be(OutboxMessageState.Failed);
        msg.AttemptCount.Should().Be(1);
        msg.LastError.Should().Be("connection refused");
        msg.NextAttemptAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ResetPublishingToPending_only_acts_on_Publishing_rows()
    {
        var pending = AMessage();
        pending.ResetPublishingToPending();
        pending.State.Should().Be(OutboxMessageState.Pending);

        var publishing = AMessage();
        publishing.MarkPublishing();
        publishing.ResetPublishingToPending();
        publishing.State.Should().Be(OutboxMessageState.Pending);

        var published = AMessage();
        published.MarkPublishing();
        published.MarkPublished();
        published.ResetPublishingToPending();
        published.State.Should().Be(OutboxMessageState.Published);
    }
}

public class OutboxBackoffTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 30)]
    [InlineData(5, 6 * 3600)]
    [InlineData(99, 6 * 3600)]   // saturates at the last delay
    public void NextDelayFor_matches_docs_database_outbox_schedule(int prevAttempts, int expectedSeconds)
    {
        OutboxBackoff.NextDelayFor(prevAttempts)
            .Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(6, 7, false)]
    [InlineData(7, 7, true)]
    [InlineData(8, 7, true)]
    public void IsTerminal_flips_at_MaxAttempts(int attempts, int max, bool expected)
    {
        OutboxBackoff.IsTerminal(attempts, max).Should().Be(expected);
    }
}
