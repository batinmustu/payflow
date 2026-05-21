using PayFlow.Reconciliation.Application.Abstractions;

namespace PayFlow.Reconciliation.IntegrationTests;

/// <summary>
/// Test stand-in for <see cref="IPaymentRefundClient"/>. Each call returns
/// the next response from <see cref="Responses"/>; if the queue is empty
/// it returns the value of <see cref="Default"/>. Throws a
/// <see cref="PaymentClientException"/> when <see cref="ThrowNextCall"/>
/// is set, then clears it.
/// </summary>
public sealed class InMemoryPaymentRefundClient : IPaymentRefundClient
{
    public Queue<PaymentRefundResponse> Responses { get; } = new();
    public PaymentRefundResponse Default { get; set; } = new(
        ProviderCode: "stripe",
        Status: "Refunded",
        ProviderReference: "rf_default",
        DeclineCode: null,
        LatencyMilliseconds: 10);

    public PaymentClientException? ThrowNextCall { get; set; }

    public List<PaymentRefundRequest> Calls { get; } = new();

    public void Reset()
    {
        Responses.Clear();
        Calls.Clear();
        ThrowNextCall = null;
    }

    public Task<PaymentRefundResponse> RefundAsync(PaymentRefundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        Calls.Add(request);

        if (ThrowNextCall is { } ex)
        {
            ThrowNextCall = null;
            throw ex;
        }

        var response = Responses.Count > 0 ? Responses.Dequeue() : Default;
        return Task.FromResult(response);
    }
}
