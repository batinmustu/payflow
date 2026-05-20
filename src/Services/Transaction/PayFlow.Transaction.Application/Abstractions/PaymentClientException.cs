namespace PayFlow.Transaction.Application.Abstractions;

/// <summary>
/// Thrown by <see cref="IPaymentClient"/> implementations when the call to
/// Payment did not produce a usable response (network failure, 5xx,
/// timeout, malformed body). The handler catches this and marks the
/// transaction Failed with ProviderError — the request never observed a
/// provider decision either way.
/// </summary>
public sealed class PaymentClientException : Exception
{
    public PaymentClientException(string message) : base(message) { }
    public PaymentClientException(string message, Exception inner) : base(message, inner) { }
}
