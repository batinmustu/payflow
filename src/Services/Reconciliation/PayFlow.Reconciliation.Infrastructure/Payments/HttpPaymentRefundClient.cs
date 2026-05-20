using System.Net.Http.Json;
using System.Text.Json;
using PayFlow.Reconciliation.Application.Abstractions;

namespace PayFlow.Reconciliation.Infrastructure.Payments;

internal sealed class HttpPaymentRefundClient : IPaymentRefundClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public HttpPaymentRefundClient(HttpClient http) => _http = http;

    public async Task<PaymentRefundResponse> RefundAsync(PaymentRefundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("api/payments/refund", request, Json, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentClientException("Could not reach Payment service.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PaymentClientException("Payment service refund call timed out.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentClientException(
                $"Payment service returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var body = await response.Content.ReadFromJsonAsync<PaymentRefundResponse>(Json, ct);
        return body ?? throw new PaymentClientException("Payment service returned an empty body.");
    }
}
