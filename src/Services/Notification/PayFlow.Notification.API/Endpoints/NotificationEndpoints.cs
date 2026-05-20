using MediatR;
using PayFlow.Multitenancy;
using PayFlow.Notification.Application.Notifications.ListNotifications;

namespace PayFlow.Notification.API.Endpoints;

internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", ListAsync)
            .WithName("ListNotifications")
            .Produces<ListNotificationsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

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
                detail: "JWT did not carry a 'tid' claim. Re-authenticate.",
                extensions: new Dictionary<string, object?> { ["code"] = "AUTH_TENANT_MISSING" });
        }

        var result = await mediator.Send(
            new ListNotificationsQuery(tenantContext.TenantId!.Value, take ?? 50), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.ValidationProblem(
                new Dictionary<string, string[]> { ["TenantId"] = [result.ErrorCode!] },
                statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
