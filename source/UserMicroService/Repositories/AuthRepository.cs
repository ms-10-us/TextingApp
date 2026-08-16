using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserMicroService.Entities;
using UserMicroService.Models;

namespace UserMicroService.Repositories
{
    /// <inheritdoc cref="IAuthRepository"/>
    public class AuthRepository : IAuthRepository
    {
        private readonly UserManager<UserEntity> _userManager;
        private readonly SignInManager<UserEntity> _signInManager;
        private readonly ILogger<AuthRepository> _logger;

        public AuthRepository(UserManager<UserEntity> userManager, SignInManager<UserEntity> signInManager, ILogger<AuthRepository> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<UserEntity?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken)
        {
            string normalizedEmail = _userManager.NormalizeEmail(identifier);
            string normalizedUserName = _userManager.NormalizeName(identifier);

            return await _userManager.Users
                .SingleOrDefaultAsync(
                u => u.NormalizedEmail == normalizedEmail || u.NormalizedUserName == normalizedUserName,
                cancellationToken);
        }

        public async Task<CredentialCheckResult> CheckPasswordAsync(UserEntity user, string password, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(user);
            cancellationToken.ThrowIfCancellationRequested();

            SignInResult result = await _signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);
        
            if (result.Succeeded)
            {
                return CredentialCheckResult.Succeeded;
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Login blocked: account {UserId} is locked out.", user.Id);
                return CredentialCheckResult.LockedOut;
            }

            if (result.IsNotAllowed)
            {
                return CredentialCheckResult.NotAllowed;
            }

            return CredentialCheckResult.InvalidPassword;

        }

        public async Task<IReadOnlyCollection<string>> GetRolesAsync(UserEntity user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IList<string> roles = await _userManager.GetRolesAsync(user);
            return roles.ToArray();
        }

        public Task<bool> HasPasswordAsync(UserEntity user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _userManager.HasPasswordAsync(user);
        }

        public async Task UpdateLastSeenAsync(UserEntity user, DateTime lastSeenUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            user.LastSeen = lastSeenUtc;

            IdentityResult result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Could not update LastSeen for {UserId}. Codes: {Codes}",
                    user.Id,
                    string.Join(", ", result.Errors.Select(e => e.Code)));
            }


        }
    }
}
