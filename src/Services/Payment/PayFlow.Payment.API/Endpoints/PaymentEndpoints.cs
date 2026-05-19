using FluentValidation;
using MediatR;
using PayFlow.Payment.Application.Payments.Charge;

namespace PayFlow.Payment.API.Endpoints;

internal static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payments").WithTags("Payments");

        group.MapPost("/charge", ChargeAsync)
            .WithName("Charge")
            .Produces<ChargeResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return routes;
    }

    private static async Task<IResult> ChargeAsync(
        ChargeRequest request,
        IValidator<ChargeCommand> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new ChargeCommand(
            TenantId: request.TenantId,
            TransactionId: request.TransactionId,
            ProviderCode: request.ProviderCode,
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
            ? Results.Ok(result.Value)
            : ErrorMapping.ToProblem(result.ErrorCode!);
    }
}
