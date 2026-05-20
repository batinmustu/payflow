using System.Net.Http.Json;
using System.Text.Json;
using PayFlow.Transaction.Application.Abstractions;

namespace PayFlow.Transaction.Infrastructure.Payments;

/// <summary>
/// HttpClient-backed <see cref="IPaymentClient"/> — talks to the Payment
/// service over plain HTTP. Maps any transport-level failure
/// (HttpRequestException, non-2xx, malformed body) into
/// <see cref="PaymentClientException"/> so the handler can map it onto a
/// "ProviderError" outcome.
/// </summary>
internal sealed class HttpPaymentClient : IPaymentClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public HttpPaymentClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PaymentChargeResponse> ChargeAsync(PaymentChargeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("api/payments/charge", request, Json, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentClientException("Could not reach Payment service.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PaymentClientException("Payment service call timed out.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentClientException(
                $"Payment service returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var body = await response.Content.ReadFromJsonAsync<PaymentChargeResponse>(Json, ct);
        return body
            ?? throw new PaymentClientException("Payment service returned an empty body.");
    }
}
