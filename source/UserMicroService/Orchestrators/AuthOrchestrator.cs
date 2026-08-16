using UserMicroService.Entities;
using UserMicroService.Mappings;
using UserMicroService.Models;
using UserMicroService.Repositories;
using UserMicroService.Security;

namespace UserMicroService.Orchestrators
{
    /// <inheritdoc cref="IAuthOrchestrator"/>
    public sealed class AuthOrchestrator : IAuthOrchestrator
    {
        // Every rejection that is not a lockout returns this exact pair, so a caller
        // cannot tell "no such account" apart from "wrong password". Distinguishing
        // them turns the login endpoint into a way to enumerate registered users.
        private const string InvalidCredentialsCode = "InvalidCredentials";
        private const string InvalidCredentialsMessage = "The supplied credentials are incorrect.";

        private readonly IAuthRepository _authRepository;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthOrchestrator> _logger;

        public AuthOrchestrator(IAuthRepository authRepository, ITokenService tokenService, ILogger<AuthOrchestrator> logger)
        {
            _authRepository = authRepository;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<OperationResult<LoginResultModel>> LoginAsync(LoginModel model, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(model);

            UserEntity? user = await _authRepository.FindByIdentifierAsync(model.Identifier, cancellationToken);

            if (user == null) 
            {
                _logger.LogInformation("Login Failed: No Account Matched The Supplied Identifier.");
                return OperationResult<LoginResultModel>.Unauthorized(
                    InvalidCredentialsCode,
                    InvalidCredentialsMessage);
            }

            // An account provisioned by the registration endpoint has no password
            // until one is set. Treat it as a normal credential failure rather than
            // advertising that the account exists but is half-configured.
            if (!await _authRepository.HasPasswordAsync(user, cancellationToken))
            {
                _logger.LogWarning("Login Failed: Account {UserId} Has No Password Set.", user.Id);
                return OperationResult<LoginResultModel>.Unauthorized(
                    InvalidCredentialsCode,
                    InvalidCredentialsMessage);
            }

            CredentialCheckResult check = await _authRepository.CheckPasswordAsync(user, model.Password, cancellationToken);    

            switch (check)
            {
                case CredentialCheckResult.Succeeded:
                    break;

                case CredentialCheckResult.LockedOut:
                    return OperationResult<LoginResultModel>.Forbidden(
                        "AccountLockedOut",
                        "This account is temporarily locked after too many failed attempts. Try again later.");

                case CredentialCheckResult.NotAllowed:
                    return OperationResult<LoginResultModel>.Forbidden(
                        "AccountNotAllowed",
                        "This account is not permitted to sign in yet.");

                default:
                    return OperationResult<LoginResultModel>.Unauthorized(
                        InvalidCredentialsCode,
                        InvalidCredentialsMessage);
            }

            DateTime now = DateTime.UtcNow;
            await _authRepository.UpdateLastSeenAsync(user, now, cancellationToken);

            IReadOnlyCollection<string> roles = await _authRepository.GetRolesAsync(user, cancellationToken);

            UserModel userModel = user.ToModel();
            AccessTokenModel accessToken = _tokenService.CreateAccessToken(userModel, roles);

            _logger.LogInformation("User {UserId} signed in.", user.Id);

            return OperationResult<LoginResultModel>.Success(new LoginResultModel
            {
                AccessToken = accessToken,
                User = userModel
            });

        }
    }
}
