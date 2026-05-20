namespace PayFlow.Reporting.API.Endpoints;

internal static class ErrorMapping
{
    private static readonly Dictionary<string, (string Field, int Status)> Codes =
        new(StringComparer.Ordinal)
        {
            ["TENANT_REQUIRED"] = ("TenantId", StatusCodes.Status422UnprocessableEntity),
            ["DATE_RANGE_INVALID"] = ("FromDate", StatusCodes.Status422UnprocessableEntity),
            ["DATE_RANGE_TOO_WIDE"] = ("ToDate", StatusCodes.Status422UnprocessableEntity),
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
