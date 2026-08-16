using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserMicroService.Entities;
using UserMicroService.Models;

namespace UserMicroService.Repositories
{
    public sealed class UserRepository : IUserRepository
    {
        private static readonly HashSet<string> ConflictErrorCodes = new(StringComparer.Ordinal)
        {
            "DuplicateEmail",
            "DuplicateUserName"
        };

        private readonly UserManager<UserEntity> _userManager;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(UserManager<UserEntity> userManager, ILogger<UserRepository> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        {
            string normalizedEmail = _userManager.NormalizeEmail(email);

            return _userManager.Users
                .AsNoTracking()
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        }

        public Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken)
        {
            string normalizedUserName = _userManager.NormalizeName(userName);

            return _userManager.Users
                .AsNoTracking()
                .AnyAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);
        }

        public async Task<OperationResult<UserEntity>> CreateAsync(UserEntity user, string password, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(user);
            cancellationToken.ThrowIfCancellationRequested();

            IdentityResult result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                return OperationResult<UserEntity>.Success(user);
            }

            IReadOnlyCollection<OperationError> errors = result.Errors
                .Select(e => new OperationError(e.Code, e.Description))
                .ToList();

            OperationStatus status = result.Errors.Any(e => ConflictErrorCodes.Contains(e.Code))
                ? OperationStatus.Conflict
                : OperationStatus.ValidationFailed;

            _logger.LogWarning(
                            "Identity user creation failed with status {Status}. Codes: {Codes}",
                            status,
                            string.Join(", ", result.Errors.Select(e => e.Code)));

            return OperationResult<UserEntity>.Failure(status, errors);

        }

        public Task<UserEntity?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            return _userManager.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        }

    }
}
