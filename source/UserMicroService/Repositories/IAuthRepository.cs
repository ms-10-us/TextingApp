using UserMicroService.Entities;
using UserMicroService.Models;

namespace UserMicroService.Repositories
{
    public interface IAuthRepository
    {
        Task<UserEntity?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken);

        Task<CredentialCheckResult> CheckPasswordAsync(UserEntity user, string password, CancellationToken cancellationToken);

        Task<IReadOnlyCollection<string>> GetRolesAsync(UserEntity user, CancellationToken cancellationToken);

        Task<bool> HasPasswordAsync(UserEntity user, CancellationToken cancellationToken);

        Task UpdateLastSeenAsync(UserEntity user, DateTime lastSeenUtc, CancellationToken cancellationToken);
    }
}
