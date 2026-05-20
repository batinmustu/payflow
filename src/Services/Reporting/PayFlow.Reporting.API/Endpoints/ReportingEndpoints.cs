using System.Globalization;
using MediatR;
using PayFlow.Multitenancy;
using PayFlow.Reporting.Application.Reports.GetTransactionSummary;

namespace PayFlow.Reporting.API.Endpoints;

internal static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet("/transactions/summary", GetTransactionSummaryAsync)
            .WithName("GetTransactionSummary")
            .Produces<GetTransactionSummaryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return routes;
    }

    private static async Task<IResult> GetTransactionSummaryAsync(
        string? from,
        string? to,
        string? currency,
        ITenantContext tenantContext,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No tenant context",
                detail: "JWT did not carry a 'tid' claim. Re-authenticate.",
                extensions: new Dictionary<string, object?> { ["code"] = "AUTH_TENANT_MISSING" });
        }

        // Default window: last 30 days (UTC) when the caller leaves both ends open.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!TryParseDate(from, today.AddDays(-30), out var fromDate))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["from"] = ["INVALID_DATE"] },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        if (!TryParseDate(to, today, out var toDate))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["to"] = ["INVALID_DATE"] },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var query = new GetTransactionSummaryQuery(
            TenantId: tenantContext.TenantId!.Value,
            FromDate: fromDate,
            ToDate: toDate,
            Currency: currency);

        var result = await mediator.Send(query, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ErrorMapping.ToProblem(result.ErrorCode!);
    }

    private static bool TryParseDate(string? input, DateOnly fallback, out DateOnly value)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            value = fallback;
            return true;
        }
        return DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out value);
    }
}
