namespace PayFlow.Identity.Application.Abstractions;

/// <summary>
/// The bearer string a client puts in <c>Authorization: Bearer ...</c>, plus the
/// moment it stops being valid. The Application layer does not know whether
/// it is a JWT, a Paseto, or an opaque token — that is the Infrastructure
/// layer's call.
/// </summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
