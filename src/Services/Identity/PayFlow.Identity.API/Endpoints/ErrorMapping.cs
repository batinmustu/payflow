namespace PayFlow.Identity.API.Endpoints;

/// <summary>
/// Maps Application/Domain <c>ErrorCode</c> strings to RFC 7807 ProblemDetails
/// responses. See docs/api/errors.md for the full catalogue; this mapper
/// covers the codes the Identity API surfaces today.
/// </summary>
internal static class ErrorMapping
{
    public static IResult ToProblem(string errorCode) => errorCode switch
    {
        "TENANT_SLUG_TAKEN" => Problem(
            statusCode: StatusCodes.Status409Conflict,
            code: errorCode,
            title: "Tenant slug already in use",
            detail: "The slug is taken by another tenant. Choose a different one."),

        "EMAIL_INVALID" or "EMAIL_REQUIRED"
            or "TENANT_NAME_REQUIRED" or "TENANT_SLUG_REQUIRED" or "TENANT_SLUG_INVALID"
            or "TENANT_REQUIRED"
            or "PASSWORD_HASH_REQUIRED" or "DISPLAY_NAME_REQUIRED" => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                code: errorCode,
                title: "Validation failed",
                detail: $"{errorCode} — see docs/api/errors.md for the canonical meaning."),

        _ => Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            code: "INTERNAL_ERROR",
            title: "Unexpected error",
            detail: $"Unmapped error code '{errorCode}'."),
    };

    private static IResult Problem(int statusCode, string code, string title, string detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
