using FluentValidation;
using MediatR;
using PayFlow.Identity.Application.Tenants.RegisterTenant;

namespace PayFlow.Identity.API.Endpoints;

internal static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tenants").WithTags("Tenants");

        group.MapPost("/", RegisterTenantAsync)
            .WithName("RegisterTenant")
            .Produces<RegisterTenantResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<IResult> RegisterTenantAsync(
        RegisterTenantRequest request,
        IValidator<RegisterTenantCommand> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new RegisterTenantCommand(
            Name: request.Name,
            Slug: request.Slug,
            AdminEmail: request.AdminEmail,
            AdminPassword: request.AdminPassword,
            AdminDisplayName: request.AdminDisplayName);

        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            // Surface ErrorCode (stable, machine-readable) — not ErrorMessage
            // (human text, localised). Same shape as ErrorMapping returns for
            // Domain-side validation failures.
            return Results.ValidationProblem(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode).ToArray()),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Created($"/api/tenants/{result.Value.TenantId}", result.Value)
            : ErrorMapping.ToProblem(result.ErrorCode!);
    }
}
