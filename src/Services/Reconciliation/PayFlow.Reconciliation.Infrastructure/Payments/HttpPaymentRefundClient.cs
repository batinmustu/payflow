using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PayFlow.Reconciliation.Application.Abstractions;

namespace PayFlow.Reconciliation.Infrastructure.Payments;

internal sealed class HttpPaymentRefundClient : IPaymentRefundClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IServiceTokenIssuer _tokens;

    public HttpPaymentRefundClient(HttpClient http, IServiceTokenIssuer tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<PaymentRefundResponse> RefundAsync(PaymentRefundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/payments/refund")
        {
            Content = JsonContent.Create(request, options: Json),
        };
        // Saga work is event-driven — there is no inbound JWT to forward.
        // We mint a short-lived service token scoped to the saga's tenant
        // so Payment's multitenancy middleware sees the right `tid`.
        var token = _tokens.IssueForTenant(request.TenantId);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, ct);
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
