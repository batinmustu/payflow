using PayFlow.Webhooks.Application.Abstractions;

namespace PayFlow.Webhooks.IntegrationTests;

/// <summary>
/// Test stand-in for <see cref="IWebhookHttpClient"/>. Each <c>PostAsync</c>
/// returns the next response from <see cref="Responses"/>, or <see cref="Default"/>
/// when the queue is empty. Captures the (url, payload, signature) of every
/// call for assertions.
/// </summary>
public sealed class RecordingWebhookHttpClient : IWebhookHttpClient
{
    public Queue<WebhookHttpResult> Responses { get; } = new();
    public WebhookHttpResult Default { get; set; } = WebhookHttpResult.Ok(200);
    public List<RecordedCall> Calls { get; } = new();

    public void Reset()
    {
        Responses.Clear();
        Calls.Clear();
        Default = WebhookHttpResult.Ok(200);
    }

    public Task<WebhookHttpResult> PostAsync(
        string url, string payload, string signatureHeader, CancellationToken ct)
    {
        Calls.Add(new RecordedCall(url, payload, signatureHeader));
        var response = Responses.Count > 0 ? Responses.Dequeue() : Default;
        return Task.FromResult(response);
    }

    public sealed record RecordedCall(string Url, string Payload, string Signature);
}
