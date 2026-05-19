using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Identity.Application.Auth.Login;

/// <summary>
/// Logs a user in by tenant slug + email + password. The response carries an
/// opaque access token (see <see cref="LoginResponse"/>) and its expiry.
///
/// All failure paths collapse to the same code (AUTH_INVALID_CREDENTIALS).
/// That is deliberate — the response must not let a caller distinguish
/// "wrong tenant", "unknown email", and "wrong password" by status or body,
/// or it becomes a tenant/account enumeration oracle.
/// </summary>
public sealed record LoginCommand(
    string TenantSlug,
    string Email,
    string Password)
    : IRequest<Result<LoginResponse>>;

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    Guid TenantId);
