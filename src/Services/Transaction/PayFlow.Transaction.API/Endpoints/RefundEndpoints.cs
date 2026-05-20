using System.Security.Claims;
using FluentValidation;
using MediatR;
using PayFlow.Multitenancy;
using PayFlow.Transaction.Application.Refunds.GetRefund;
using PayFlow.Transaction.Application.Refunds.RequestRefund;

namespace PayFlow.Transaction.API.Endpoints;

internal static class RefundEndpoints
{
    public static IEndpointRouteBuilder MapRefundEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/transactions")
            .WithTags("Refunds")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/refunds", RequestRefundAsync)
            .WithName("RequestRefund")
            .Produces<RequestRefundResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/refunds/{refundId:guid}", GetRefundAsync)
            .WithName("GetRefund")
            .Produces<GetRefundResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routes;
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

        var tenantGuard = TenantGuard.RequireTenant(tenantContext, out var tenantId);
        if (tenantGuard is not null) return tenantGuard;

        // RequestedBy comes only from the JWT — never from the body — so a
        // caller can't smuggle PII (e.g. someone else's email) into the
        // refund row and the outboxed Kafka event.
        var requestedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No subject",
                detail: "JWT did not carry a 'sub' claim. Re-authenticate.",
                extensions: new Dictionary<string, object?> { ["code"] = "AUTH_SUBJECT_MISSING" });
        }

        var command = new RequestRefundCommand(
            TenantId: tenantId,
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

    private static async Task<IResult> GetRefundAsync(
        Guid id,
        Guid refundId,
        ITenantContext tenantContext,
        IMediator mediator,
        CancellationToken ct)
    {
        var tenantGuard = TenantGuard.RequireTenant(tenantContext, out var tenantId);
        if (tenantGuard is not null) return tenantGuard;

        var query = new GetRefundQuery(
            TenantId: tenantId,
            TransactionId: id,
            RefundId: refundId);

        var result = await mediator.Send(query, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ErrorMapping.ToProblem(result.ErrorCode!);
    }
}
