using MediatR;
using PayFlow.Multitenancy;
using PayFlow.Webhooks.Application.Subscriptions;

namespace PayFlow.Webhooks.API.Endpoints;

internal static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/webhooks/subscriptions")
            .WithTags("Webhook subscriptions")
            .RequireAuthorization();

        group.MapPost("/", CreateAsync)
            .WithName("CreateWebhookSubscription")
            .Produces<CreateSubscriptionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", ListAsync)
            .WithName("ListWebhookSubscriptions")
            .Produces<IReadOnlyList<SubscriptionListItem>>(StatusCodes.Status200OK);

        group.MapDelete("/{id:guid}", DeactivateAsync)
            .WithName("DeactivateWebhookSubscription")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }

    public sealed record CreateSubscriptionRequest(string EventType, string Url);

    private static async Task<IResult> CreateAsync(
        CreateSubscriptionRequest request,
        ITenantContext tenantContext,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!tenantContext.IsResolved) return Tenant401();

        var result = await mediator.Send(
            new CreateSubscriptionCommand(tenantContext.TenantId!.Value, request.EventType, request.Url),
            ct);

        return result.IsSuccess
            ? Results.Created($"/api/webhooks/subscriptions/{result.Value.Id}", result.Value)
            : Results.ValidationProblem(
                new Dictionary<string, string[]> { ["Subscription"] = [result.ErrorCode!] });
    }

    private static async Task<IResult> ListAsync(
        ITenantContext tenantContext,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!tenantContext.IsResolved) return Tenant401();
        var rows = await mediator.Send(new ListSubscriptionsQuery(tenantContext.TenantId!.Value), ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        ITenantContext tenantContext,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!tenantContext.IsResolved) return Tenant401();
        var result = await mediator.Send(
            new DeactivateSubscriptionCommand(tenantContext.TenantId!.Value, id), ct);
        return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    }

    private static IResult Tenant401() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "No tenant context",
        detail: "JWT did not carry a 'tid' claim. Re-authenticate.",
        extensions: new Dictionary<string, object?> { ["code"] = "AUTH_TENANT_MISSING" });
}
