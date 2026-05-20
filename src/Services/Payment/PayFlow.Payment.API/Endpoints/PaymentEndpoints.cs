using FluentValidation;
using MediatR;
using PayFlow.Multitenancy;
using PayFlow.Payment.Application.Payments.Charge;
using PayFlow.Payment.Application.Payments.Refund;

namespace PayFlow.Payment.API.Endpoints;

internal static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payments")
            .WithTags("Payments")
            .RequireAuthorization();

        group.MapPost("/charge", ChargeAsync)
            .WithName("Charge")
            .Produces<ChargeResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapPost("/refund", RefundAsync)
            .WithName("Refund")
            .Produces<RefundResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return routes;
    }

    private static async Task<IResult> ChargeAsync(
        ChargeRequest request,
        ITenantContext tenantContext,
        IValidator<ChargeCommand> validator,
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

        var command = new ChargeCommand(
            TenantId: tenantContext.TenantId!.Value,
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

    private static async Task<IResult> RefundAsync(
        RefundRequest request,
        ITenantContext tenantContext,
        IValidator<RefundCommand> validator,
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

        var command = new RefundCommand(
            TenantId: tenantContext.TenantId!.Value,
            TransactionId: request.TransactionId,
            ProviderCode: request.ProviderCode,
            ProviderReference: request.ProviderReference,
            AmountMinor: request.AmountMinor,
            Currency: request.Currency,
            IdempotencyKey: request.IdempotencyKey);

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
