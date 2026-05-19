namespace PayFlow.Identity.API.Endpoints;

/// <summary>
/// HTTP request body for <c>POST /api/tenants</c>. Mirrors the
/// <see cref="PayFlow.Identity.Application.Tenants.RegisterTenant.RegisterTenantCommand"/>
/// shape; kept separate so the HTTP contract can evolve independently of
/// the command (e.g. adding optional fields, deprecations).
/// </summary>
public sealed record RegisterTenantRequest(
    string Name,
    string Slug,
    string AdminEmail,
    string AdminPassword,
    string AdminDisplayName);
