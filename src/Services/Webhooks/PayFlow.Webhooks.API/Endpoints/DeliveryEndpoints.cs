using MediatR;
using PayFlow.Multitenancy;
using PayFlow.Webhooks.Application.Deliveries;

namespace PayFlow.Webhooks.API.Endpoints;

internal static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/webhooks/deliveries")
            .WithTags("Webhook deliveries")
            .RequireAuthorization();

        group.MapGet("/", ListAsync)
            .WithName("ListWebhookDeliveries")
            .Produces<IReadOnlyList<DeliveryListItem>>(StatusCodes.Status200OK);

        return routes;
    }

    private static async Task<IResult> ListAsync(
        int? take,
        ITenantContext tenantContext,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No tenant context",
                detail: "JWT did not carry a 'tid' claim. Re-authenticate.");
        }

        var rows = await mediator.Send(
            new ListDeliveriesQuery(tenantContext.TenantId!.Value, take ?? 100), ct);
        return Results.Ok(rows);
    }
}
