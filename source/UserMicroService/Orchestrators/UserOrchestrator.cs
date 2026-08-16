using UserMicroService.Entities;
using UserMicroService.Mappings;
using UserMicroService.Models;
using UserMicroService.Repositories;

namespace UserMicroService.Orchestrators
{
    public sealed class UserOrchestrator : IUserOrchestrator
    {
        private readonly IUserRepository _userRepository;

        private readonly ILogger<UserOrchestrator> _logger;

        public UserOrchestrator(IUserRepository userRepository, ILogger<UserOrchestrator> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<OperationResult<UserModel>> AddUserAsync(AddUserModel model, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (await _userRepository.EmailExistsAsync(model.Email, cancellationToken))
            {
                return OperationResult<UserModel>.Conflict(
                    "EmailAlreadyRegistered",
                    "An account with this email address already exists."
                    );
            }

            if (await _userRepository.UserNameExistsAsync(model.UserName, cancellationToken))
            {
                return OperationResult<UserModel>.Conflict(
                    "UserNameAlreadyTaken",
                    "This username is already taken."
                    );
            }

            UserEntity entity = model.ToEntity();

            OperationResult<UserEntity> createResult = await _userRepository.CreateAsync(entity, model.Password, cancellationToken);

            if (!createResult.Succeeded)
            {
                return createResult.ToFailure<UserModel>();
            }

            _logger.LogInformation("Registered user profile {UserId}.", createResult.Value!.Id);

            return OperationResult<UserModel>.Success(createResult.Value.ToModel());
        }

        public async Task<OperationResult<UserModel>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            UserEntity entity = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (entity == null)
            {
                return OperationResult<UserModel>.NotFound(
                    "UserProfileNotFound",
                    "No user profile exists with the supplied identifier.");
            }

            return OperationResult<UserModel>.Success(entity.ToModel());
        }

    }
}
