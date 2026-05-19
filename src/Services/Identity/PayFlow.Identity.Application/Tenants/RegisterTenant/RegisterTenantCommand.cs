using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Identity.Application.Tenants.RegisterTenant;

/// <summary>
/// Registers a new tenant together with its first administrator user.
/// The two are created atomically — a tenant without an admin would be
/// unreachable, so we never want one to land without the other.
/// </summary>
public sealed record RegisterTenantCommand(
    string Name,
    string Slug,
    string AdminEmail,
    string AdminPassword,
    string AdminDisplayName)
    : IRequest<Result<RegisterTenantResponse>>;

public sealed record RegisterTenantResponse(Guid TenantId, Guid AdminUserId, string Slug);
