using System.Security.Claims;
using FluentValidation;
using MediatR;
using PayFlow.Multitenancy;
using PayFlow.Transaction.Application.Refunds.RequestRefund;
using PayFlow.Transaction.Application.Transactions.CreateTransaction;

namespace PayFlow.Transaction.API.Endpoints;

internal static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/transactions")
            .WithTags("Transactions")
            .RequireAuthorization();

        group.MapPost("/", CreateAsync)
            .WithName("CreateTransaction")
            .Produces<CreateTransactionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/refunds", RequestRefundAsync)
            .WithName("RequestRefund")
            .Produces<RequestRefundResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<IResult> CreateAsync(
        CreateTransactionRequest request,
        ITenantContext tenantContext,
        IValidator<CreateTransactionCommand> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenantContext.IsResolved)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No tenant context",
                detail: "JWT did not carry a 'tid' claim. Re-authenticate.",
                extensions: new Dictionary<string, object?> { ["code"] = "AUTH_TENANT_MISSING" });
        }

        var command = new CreateTransactionCommand(
            TenantId: tenantContext.TenantId!.Value,
            OrderReference: request.OrderReference,
            AmountMinor: request.AmountMinor,
            Currency: request.Currency,
            CardToken: request.CardToken);

        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode).ToArray()),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var result = await mediator.Send(command, ct);
        return result.IsSuccess
            ? Results.Created($"/api/transactions/{result.Value.TransactionId}", result.Value)
            : ErrorMapping.ToProblem(result.ErrorCode!);
    }

    private static async Task<IResult> RequestRefundAsync(
        Guid id,
        RequestRefundRequest request,
        ClaimsPrincipal user,
        ITenantContext tenantContext,
        IValidator<RequestRefundCommand> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenantContext.IsResolved)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No tenant context",
                detail: "JWT did not carry a 'tid' claim. Re-authenticate.",
                extensions: new Dictionary<string, object?> { ["code"] = "AUTH_TENANT_MISSING" });
        }

        var requestedBy = !string.IsNullOrWhiteSpace(request.RequestedBy)
            ? request.RequestedBy
            : user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? "unknown";

        var command = new RequestRefundCommand(
            TenantId: tenantContext.TenantId!.Value,
            TransactionId: id,
            AmountMinor: request.AmountMinor,
            RequestedBy: requestedBy);

        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode).ToArray()),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var result = await mediator.Send(command, ct);
        return result.IsSuccess
            ? Results.Accepted(
                $"/api/transactions/{result.Value.TransactionId}/refunds/{result.Value.RefundId}",
                result.Value)
            : ErrorMapping.ToProblem(result.ErrorCode!);
    }
}
