using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.Application.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
