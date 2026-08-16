using UserMicroService.Entities;
using UserMicroService.Models;

namespace UserMicroService.Repositories
{
    public interface IUserRepository
    {
        Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

        Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken);

        Task<OperationResult<UserEntity>> CreateAsync(UserEntity user, string password, CancellationToken cancellationToken);

        Task<UserEntity?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
    }
}
