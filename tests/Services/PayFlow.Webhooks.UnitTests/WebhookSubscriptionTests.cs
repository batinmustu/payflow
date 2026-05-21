namespace PayFlow.Webhooks.UnitTests;

public sealed class WebhookSubscriptionTests
{
    private static readonly string ValidSecret = new('s', 40);

    [Fact]
    public void Create_rejects_non_http_scheme()
    {
        var result = WebhookSubscription.Create(
            tenantId: Guid.NewGuid(),
            eventType: "payflow.transaction.captured.v1",
            url: "ftp://merchant.example.com/hook",
            secret: ValidSecret);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("URL_SCHEME_UNSUPPORTED");
    }

    [Fact]
    public void Create_rejects_non_uri_garbage()
    {
        var result = WebhookSubscription.Create(
            tenantId: Guid.NewGuid(),
            eventType: "payflow.transaction.captured.v1",
            url: "not a url",
            secret: ValidSecret);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("URL_INVALID");
    }

    [Fact]
    public void Create_rejects_short_secret()
    {
        var result = WebhookSubscription.Create(
            tenantId: Guid.NewGuid(),
            eventType: "payflow.transaction.captured.v1",
            url: "https://merchant.example.com/hook",
            secret: "tooshort");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("SECRET_TOO_SHORT");
    }

    [Fact]
    public void Create_succeeds_with_https_url_and_long_secret()
    {
        var result = WebhookSubscription.Create(
            tenantId: Guid.NewGuid(),
            eventType: "payflow.transaction.captured.v1",
            url: "https://merchant.example.com/hook",
            secret: ValidSecret);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Deactivate_is_idempotent()
    {
        var sub = WebhookSubscription.Create(
            Guid.NewGuid(), "evt", "https://x.test/", ValidSecret).Value;

        sub.Deactivate();
        sub.IsActive.Should().BeFalse();
        var first = sub.DeactivatedAt;

        sub.Deactivate();
        sub.DeactivatedAt.Should().Be(first, "second deactivate must not move the timestamp");
    }
}
