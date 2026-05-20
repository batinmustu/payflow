namespace PayFlow.Payment.API.Endpoints;

/// <summary>
/// Same shape contract as Identity: field-mapped codes → 422
/// ValidationProblemDetails; conflict-ish codes → status-coded
/// ValidationProblem; non-field bugs → 500 Problem with the code.
/// See docs/api/errors.md.
/// </summary>
internal static class ErrorMapping
{
    private static readonly Dictionary<string, (string Field, int Status)> Codes =
        new(StringComparer.Ordinal)
        {
            ["TENANT_REQUIRED"] = ("TenantId", StatusCodes.Status422UnprocessableEntity),
            ["PROVIDER_CODE_REQUIRED"] = ("ProviderCode", StatusCodes.Status422UnprocessableEntity),
            ["PROVIDER_NOT_CONFIGURED"] = ("ProviderCode", StatusCodes.Status422UnprocessableEntity),
            ["AMOUNT_INVALID"] = ("AmountMinor", StatusCodes.Status422UnprocessableEntity),
            ["CURRENCY_NOT_SUPPORTED"] = ("Currency", StatusCodes.Status422UnprocessableEntity),
            ["CARD_TOKEN_INVALID"] = ("CardToken", StatusCodes.Status422UnprocessableEntity),
            ["TRANSACTION_REQUIRED"] = ("TransactionId", StatusCodes.Status422UnprocessableEntity),
            ["PROVIDER_REFERENCE_REQUIRED"] = ("ProviderReference", StatusCodes.Status422UnprocessableEntity),
            ["IDEMPOTENCY_KEY_REQUIRED"] = ("IdempotencyKey", StatusCodes.Status422UnprocessableEntity),
        };

    public static IResult ToProblem(string errorCode)
    {
        if (Codes.TryGetValue(errorCode, out var entry))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { [entry.Field] = [errorCode] },
                statusCode: entry.Status);
        }

        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unexpected error",
            detail: $"Unmapped error code '{errorCode}'.",
            extensions: new Dictionary<string, object?> { ["code"] = errorCode });
    }
}
