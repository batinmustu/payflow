using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using PayFlow.Webhooks.Application.Abstractions;

namespace PayFlow.Webhooks.Infrastructure.Http;

/// <summary>
/// HttpClient-backed <see cref="IWebhookHttpClient"/>. Sends the JSON
/// payload as-is and the HMAC signature in <c>X-PayFlow-Signature</c>.
/// Maps transport exceptions + non-2xx into the structured
/// <see cref="WebhookHttpResult"/> the dispatcher reasons about.
///
/// The HttpClient comes from IHttpClientFactory ("PayFlow.Webhooks") with
/// a 10s timeout — long enough for slow merchants but short enough that
/// one bad endpoint can't pin a worker thread for minutes.
/// </summary>
internal sealed class HttpWebhookClient : IWebhookHttpClient
{
    public const string ClientName = "PayFlow.Webhooks";
    public const string SignatureHeader = "X-PayFlow-Signature";

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<HttpWebhookClient> _logger;

    public HttpWebhookClient(IHttpClientFactory factory, ILogger<HttpWebhookClient> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<WebhookHttpResult> PostAsync(
        string url, string payload, string signatureHeader, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var client = _factory.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(SignatureHeader, signatureHeader);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PayFlow-Webhooks", "1.0"));

        try
        {
            using var response = await client.SendAsync(request, ct);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
                return WebhookHttpResult.Ok(statusCode);

            return WebhookHttpResult.NonSuccess(statusCode, $"HTTP_{statusCode}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Webhook POST to {Url} timed out.", url);
            return WebhookHttpResult.TransportError("TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            // Includes DNS failures, connection refused, TLS errors. All
            // count as transient — the merchant might bring the endpoint
            // back up; the retry sweeper will try again.
            _logger.LogWarning(ex, "Webhook POST to {Url} failed transport ({Code}).",
                url, ex.StatusCode ?? HttpStatusCode.ServiceUnavailable);
            return WebhookHttpResult.TransportError("TRANSPORT_ERROR");
        }
    }
}
