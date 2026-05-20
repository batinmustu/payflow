using FluentValidation;
using MediatR;
using PayFlow.Multitenancy;
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

        var tenantGuard = TenantGuard.RequireTenant(tenantContext, out var tenantId);
        if (tenantGuard is not null) return tenantGuard;

        var command = new CreateTransactionCommand(
            TenantId: tenantId,
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
}
