using System.Security.Claims;

namespace PayFlow.Identity.API.Endpoints;

internal static class MeEndpoint
{
    public static IEndpointRouteBuilder MapMeEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/me", Me)
            .RequireAuthorization()
            .WithName("Me")
            .WithTags("Auth")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return routes;
    }

    private static IResult Me(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        var tid = principal.FindFirstValue("tid");
        var email = principal.FindFirstValue("email");
        var name = principal.FindFirstValue("name");
        var roles = principal.FindAll("role").Select(c => c.Value).ToArray();

        return Results.Ok(new MeResponse(
            UserId: sub ?? "(missing)",
            TenantId: tid ?? "(missing)",
            Email: email ?? "(missing)",
            DisplayName: name ?? "(missing)",
            Roles: roles));
    }
}

public sealed record MeResponse(
    string UserId,
    string TenantId,
    string Email,
    string DisplayName,
    string[] Roles);
