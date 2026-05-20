namespace PayFlow.Transaction.Infrastructure.Payments;

public sealed class PaymentServiceOptions
{
    public const string SectionName = "Transaction:PaymentService";

    public string BaseUrl { get; set; } = "http://localhost:5002";
    public int TimeoutSeconds { get; set; } = 30;
}
