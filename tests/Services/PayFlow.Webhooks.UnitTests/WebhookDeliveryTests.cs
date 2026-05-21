namespace PayFlow.Webhooks.UnitTests;

public sealed class WebhookDeliveryTests
{
    private static WebhookDelivery Make() =>
        WebhookDelivery.Create(
            tenantId: Guid.NewGuid(),
            subscriptionId: Guid.NewGuid(),
            eventType: "payflow.transaction.captured.v1",
            sourceMessageId: Guid.NewGuid(),
            targetUrl: "https://merchant.example.com/hook",
            payload: "{}",
            signature: "sha256=abc").Value;

    [Fact]
    public void MarkTransientFailure_increments_attempt_and_sets_next_attempt_at()
    {
        var delivery = Make();
        delivery.MarkTransientFailure(503, "HTTP_503", TimeSpan.FromSeconds(30));

        delivery.State.Should().Be(WebhookDeliveryState.Pending);
        delivery.AttemptCount.Should().Be(1);
        delivery.LastStatusCode.Should().Be(503);
        delivery.LastError.Should().Be("HTTP_503");
        delivery.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow);
        delivery.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void MarkSent_is_terminal_and_clears_next_attempt_at()
    {
        var delivery = Make();
        delivery.MarkTransientFailure(503, "HTTP_503", TimeSpan.FromSeconds(30));
        delivery.MarkSent(200);

        delivery.State.Should().Be(WebhookDeliveryState.Sent);
        delivery.LastStatusCode.Should().Be(200);
        delivery.NextAttemptAt.Should().BeNull();
        delivery.SentAt.Should().NotBeNull();
        delivery.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void MarkFailed_is_terminal_and_clears_next_attempt_at()
    {
        var delivery = Make();
        delivery.MarkFailed(404, "HTTP_404");

        delivery.State.Should().Be(WebhookDeliveryState.Failed);
        delivery.NextAttemptAt.Should().BeNull();
        delivery.FailedAt.Should().NotBeNull();
    }

    [Fact]
    public void CanRetry_false_after_MaxAttempts_consumed()
    {
        var delivery = Make();
        for (var i = 0; i < WebhookDelivery.MaxAttempts; i++)
        {
            delivery.MarkTransientFailure(503, "HTTP_503", TimeSpan.FromSeconds(1));
        }
        delivery.CanRetry.Should().BeFalse();
        delivery.AttemptCount.Should().Be(WebhookDelivery.MaxAttempts);
    }

    [Fact]
    public void Operations_on_terminal_state_throw()
    {
        var delivery = Make();
        delivery.MarkSent(200);

        var act = () => delivery.MarkTransientFailure(503, "X", TimeSpan.FromSeconds(1));
        act.Should().Throw<InvalidOperationException>();
    }
}
