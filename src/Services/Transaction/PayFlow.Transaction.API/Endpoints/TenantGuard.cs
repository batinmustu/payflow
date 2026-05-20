using PayFlow.Multitenancy;

namespace PayFlow.Transaction.API.Endpoints;

/// <summary>
/// The same "tenant context resolved?" + "subject claim present?" 401 chunks
/// repeat across endpoints — extracted so the handlers stay focused on
/// orchestrating the command/query rather than re-checking the JWT shape.
/// </summary>
internal static class TenantGuard
{
    public static IResult? RequireTenant(ITenantContext tenantContext, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        if (!tenantContext.IsResolved)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No tenant context",
                detail: "JWT did not carry a 'tid' claim. Re-authenticate.",
                extensions: new Dictionary<string, object?> { ["code"] = "AUTH_TENANT_MISSING" });
        }
        tenantId = tenantContext.TenantId!.Value;
        return null;
    }
}
