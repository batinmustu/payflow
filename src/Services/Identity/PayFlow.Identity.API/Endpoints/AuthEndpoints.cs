using FluentValidation;
using MediatR;
using PayFlow.Identity.Application.Auth.Login;

namespace PayFlow.Identity.API.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return routes;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginCommand> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new LoginCommand(
            TenantSlug: request.TenantSlug,
            Email: request.Email,
            Password: request.Password);

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
