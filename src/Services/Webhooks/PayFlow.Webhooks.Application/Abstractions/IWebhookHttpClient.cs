namespace PayFlow.Webhooks.Application.Abstractions;

/// <summary>
/// Transport for POST'ing a webhook payload + signature to a merchant
/// endpoint. Infrastructure layer implements with HttpClient + a short
/// timeout. Returns the response status code (so the dispatcher can
/// decide success / transient / terminal) or an error string when the
/// transport itself failed (DNS, connection refused, TLS, timeout).
/// </summary>
public interface IWebhookHttpClient
{
    Task<WebhookHttpResult> PostAsync(
        string url,
        string payload,
        string signatureHeader,
        CancellationToken ct);
}

public sealed record WebhookHttpResult(int? StatusCode, string? Error)
{
    public static WebhookHttpResult Ok(int statusCode) => new(statusCode, null);
    public static WebhookHttpResult NonSuccess(int statusCode, string error) => new(statusCode, error);
    public static WebhookHttpResult TransportError(string error) => new(null, error);

    public bool IsSuccess => StatusCode is >= 200 and < 300;
    public bool IsTerminal4xx => StatusCode is >= 400 and < 500
        && StatusCode is not 408 and not 429;
}
