using MediatR;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Domain.Users;
using PayFlow.SharedKernel;

namespace PayFlow.Identity.Application.Auth.Login;

internal sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private const string InvalidCredentialsCode = "AUTH_INVALID_CREDENTIALS";

    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IAccessTokenIssuer _tokens;

    public LoginCommandHandler(
        ITenantRepository tenants,
        IUserRepository users,
        IPasswordHasher hasher,
        IAccessTokenIssuer tokens)
    {
        _tenants = tenants;
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
        {
            return Failure();
        }

        var tenant = await _tenants.GetBySlugAsync(command.TenantSlug, ct);
        if (tenant is null)
        {
            return Failure();
        }

        var user = await _users.FindByEmailAsync(tenant.Id, emailResult.Value, ct);
        if (user is null)
        {
            return Failure();
        }

        if (!_hasher.Verify(command.Password, user.PasswordHash))
        {
            return Failure();
        }

        var token = _tokens.Issue(user);
        return Result.Success(new LoginResponse(
            AccessToken: token.Value,
            ExpiresAt: token.ExpiresAt,
            UserId: user.Id,
            TenantId: user.TenantId));
    }

    private static Result<LoginResponse> Failure() =>
        Result.Failure<LoginResponse>(InvalidCredentialsCode);
}
