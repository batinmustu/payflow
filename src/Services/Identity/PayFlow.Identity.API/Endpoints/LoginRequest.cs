namespace PayFlow.Identity.API.Endpoints;

public sealed record LoginRequest(
    string TenantSlug,
    string Email,
    string Password);
