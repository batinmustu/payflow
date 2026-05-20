namespace PayFlow.Transaction.Domain;

public static class FailureReason
{
    public const string RoutingExhausted = "RoutingExhausted";
    public const string HardDeclined = "HardDeclined";
    public const string ProviderError = "ProviderError";
    public const string InvalidRequest = "InvalidRequest";
}
