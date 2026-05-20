namespace PayFlow.Transaction.API.Endpoints;

internal static class ErrorMapping
{
    private static readonly Dictionary<string, (string Field, int Status)> Codes =
        new(StringComparer.Ordinal)
        {
            ["TENANT_REQUIRED"] = ("TenantId", StatusCodes.Status422UnprocessableEntity),
            ["ORDER_REFERENCE_REQUIRED"] = ("OrderReference", StatusCodes.Status422UnprocessableEntity),
            ["AMOUNT_INVALID"] = ("AmountMinor", StatusCodes.Status422UnprocessableEntity),
            ["CURRENCY_NOT_SUPPORTED"] = ("Currency", StatusCodes.Status422UnprocessableEntity),
            ["ORDER_REFERENCE_ALREADY_USED"] = ("OrderReference", StatusCodes.Status409Conflict),
        };

    public static IResult ToProblem(string errorCode)
    {
        if (Codes.TryGetValue(errorCode, out var entry))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { [entry.Field] = [errorCode] },
                statusCode: entry.Status,
                title: entry.Status == StatusCodes.Status409Conflict ? "Resource conflict" : null);
        }

        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unexpected error",
            detail: $"Unmapped error code '{errorCode}'.",
            extensions: new Dictionary<string, object?> { ["code"] = errorCode });
    }
}
