using UserMicroService.Models;

namespace UserMicroService.Security
{
    public interface ITokenService
    {
        AccessTokenModel CreateAccessToken(UserModel user, IReadOnlyCollection<string> roles);
    }
}
