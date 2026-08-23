using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Entities;
using UserMicroService.Models;
using UserMicroService.Orchestrators;
using UserMicroService.Repositories;
using UserMicroService.Security;

namespace UserMicroServiceUnitTests.Orchestrators
{
    public class AuthOrchestratorTests
    {
        private const string InvalidCredentialsCode = "InvalidCredentials";

        private readonly Mock<IAuthRepository> _authRepository = new Mock<IAuthRepository>(MockBehavior.Strict);
        private readonly Mock<ITokenService> _tokenService = new Mock<ITokenService>(MockBehavior.Strict);
        private readonly Mock<ILogger<AuthOrchestrator>> _logger = new Mock<ILogger<AuthOrchestrator>>();

        private AuthOrchestrator CreateSut() => 
            new AuthOrchestrator(_authRepository.Object, _tokenService.Object, _logger.Object);

        private UserEntity ArrangeHappyPath(params string[] roles)
        {
            UserEntity user = TestData.User();

            _authRepository
                .Setup(r => r.FindByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _authRepository
                .Setup(r => r.HasPasswordAsync(user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _authRepository
                .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CredentialCheckResult.Succeeded);
            _authRepository
                .Setup(r => r.UpdateLastSeenAsync(user, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _authRepository
                .Setup(r => r.GetRolesAsync(user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(roles);

            return user;
        }

        private void VerifyLogged(LogLevel level, Times times)
        {
            _logger.Verify(
                l => l.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    ),
                times);
        }

        [Fact]
        public async Task GivenNullModel_WhenCallingLoginAsync_ThrowsException()
        {

            AuthOrchestrator sut = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.LoginAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task GivenUnknownIdentifier_WhenCallingLoginAsync_ReturnsUnauthorized()
        {
            _authRepository
                .Setup(r => r.FindByIdentifierAsync("nobody@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity?)null);

            OperationResult<LoginResultModel> result = await CreateSut()
                .LoginAsync(TestData.LoginModel(identfier: "nobody@example.com"), CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(OperationStatus.Unauthorized, result.Status);
            Assert.Equal(InvalidCredentialsCode, Assert.Single(result.Errors).Code);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task GivenUnknownIdentifier_WhenCallingLoginAsync_DoesNotCheckPasswordOrIssueToken()
        {
            _authRepository
                .Setup(r => r.FindByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity?)null);

            await CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None);

            _authRepository.Verify(
                r => r.CheckPasswordAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _tokenService.Verify(
                t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()),
                Times.Never);
        }

        [Fact]
        public async Task GivenAccountWithoutPassword_WhenCallingLoginAsync_ReturnsUnauthorizedWithoutCheckingCredentials()
        {
            UserEntity user = TestData.User();
            _authRepository
                .Setup(r => r.FindByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _authRepository
                .Setup(r => r.HasPasswordAsync(user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            OperationResult<LoginResultModel> result = await CreateSut()
                .LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.Equal(OperationStatus.Unauthorized, result.Status);
            Assert.Equal(InvalidCredentialsCode, Assert.Single(result.Errors).Code);
            _authRepository.Verify(
                r => r.CheckPasswordAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GivenAccountWithoutPassword_WhenCallingLoginAsync_LogsWarning()
        {
            UserEntity user = TestData.User();
            _authRepository
                .Setup(r => r.FindByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _authRepository
                .Setup(r => r.HasPasswordAsync(user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None);

            VerifyLogged(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public async Task GivenWrongPassword_WhenCallingLoginAsync_ReturnsUnauthorized()
        {
            UserEntity user = ArrangeHappyPath();
            _authRepository
                .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CredentialCheckResult.InvalidPassword);

            OperationResult<LoginResultModel> result = await CreateSut()
                .LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.Equal(OperationStatus.Unauthorized, result.Status);
            Assert.Equal(InvalidCredentialsCode, Assert.Single(result.Errors).Code);
        }

        [Fact]
        public async Task GivenAccountLockedOut_WhenCallingAsync_ReturnsForbiddenWithLockoutCode()
        {
            UserEntity user = ArrangeHappyPath();
            _authRepository
                .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CredentialCheckResult.LockedOut);

            OperationResult<LoginResultModel> result = await CreateSut()
                .LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.Equal(OperationStatus.Forbidden, result.Status);
            Assert.Equal("AccountLockedOut", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public async Task GivenAccountNotAllowed_WhenCallingLoginAsync_ReturnsForbiddenWithNotAllowedCode()
        {
            UserEntity user = ArrangeHappyPath();
            _authRepository
                .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CredentialCheckResult.NotAllowed);

            OperationResult<LoginResultModel> result = await CreateSut()
                .LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.Equal(OperationStatus.Forbidden, result.Status);
            Assert.Equal("AccountNotAllowed", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public async Task GivenUnrecognizedCredentialResult_WhenCallingLoginAsync_FallsBackToUnauthorized()
        {
            UserEntity user = ArrangeHappyPath();
            _authRepository
                .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CredentialCheckResult)999);

            OperationResult<LoginResultModel> result = await CreateSut()
                .LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.Equal(OperationStatus.Unauthorized, result.Status);
            Assert.Equal(InvalidCredentialsCode, Assert.Single(result.Errors).Code);
        }

        [Theory]
        [InlineData(CredentialCheckResult.InvalidPassword)]
        [InlineData(CredentialCheckResult.LockedOut)]
        [InlineData(CredentialCheckResult.NotAllowed)]
        public async Task GivenAnyRejection_WhenCallingLogin_NeverIssuesTokenOrTouchesLastSeen(CredentialCheckResult check)
        {
            UserEntity user = ArrangeHappyPath();
            _authRepository
                .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(check);

            await CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None);

            _tokenService.Verify(
                t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()),
                Times.Never);
            _authRepository.Verify(
                r => r.UpdateLastSeenAsync(It.IsAny<UserEntity>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GivenUnknownUserAndWrongPassword_WhenCallingLoginAsync_AreIndistinguishableToTheCaller()
        {
            var missingUserRepo = new Mock<IAuthRepository>();
            missingUserRepo
                .Setup(r => r.FindByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity?)null);

            OperationResult<LoginResultModel> unkownUser =
                await new AuthOrchestrator(missingUserRepo.Object, _tokenService.Object, NullLogger<AuthOrchestrator>.Instance)
                .LoginAsync(TestData.LoginModel(), CancellationToken.None);

            UserEntity user = ArrangeHappyPath();
            _authRepository
                .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CredentialCheckResult.InvalidPassword);

            OperationResult<LoginResultModel> wrongPassword = 
                await CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.Equal(unkownUser.Status, wrongPassword.Status);
            Assert.Equal(
                unkownUser.Errors.Single().Code,
                wrongPassword.Errors.Single().Code);
            Assert.Equal(
                unkownUser.Errors.Single().Description,
                wrongPassword.Errors.Single().Description);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenCallingLoginAsync_ReturnsTokenAndUser()
        {
            UserEntity user = ArrangeHappyPath("Admin");
            AccessTokenModel issued = TestData.AccessToken("issued-jwt");
            _tokenService
                .Setup(t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()))
                .Returns(issued);

            OperationResult<LoginResultModel> result = await CreateSut()
                .LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(OperationStatus.Success, result.Status);
            Assert.Empty(result.Errors);
            Assert.Same(issued, result.Value!.AccessToken);
            Assert.Equal(user.Id, result.Value.User.Id);
            Assert.Equal(user.DisplayName, result.Value.User.DisplayName);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenCallingLoginAsync_PassesUsersRolesToTokenService()
        {
            ArrangeHappyPath("Admin", "Moderator");
            IReadOnlyCollection<string>? capturedRoles = null;
            _tokenService
                .Setup(t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()))
                .Callback<UserModel, IReadOnlyCollection<string>>((_, roles) => capturedRoles = roles)
                .Returns(TestData.AccessToken());

            await CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.NotNull(capturedRoles);
            Assert.Equal(new[] { "Admin", "Moderator" }, capturedRoles!);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenCallingAsync_RecordsLastSeenAsUtcNow()
        {
            UserEntity user = ArrangeHappyPath();
            DateTime? captured = null;
            _authRepository
                .Setup(r => r.UpdateLastSeenAsync(user, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<UserEntity, DateTime, CancellationToken>((_, when, _) => captured = when)
                .Returns(Task.CompletedTask);
            _tokenService
                .Setup(t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()))
                .Returns(TestData.AccessToken());

            DateTime before = DateTime.UtcNow;
            await CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None);
            DateTime after = DateTime.UtcNow;

            Assert.NotNull(captured);
            Assert.Equal(DateTimeKind.Utc, captured!.Value.Kind);
            Assert.InRange(captured.Value, before, after);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenCallingLoginAsync_LogsSuccessfulSignIn()
        {
            ArrangeHappyPath();
            _tokenService
                .Setup(t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()))
                .Returns(TestData.AccessToken());

            await CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None);

            VerifyLogged(LogLevel.Information, Times.Once());
        }

        [Fact]
        public async Task GivenCancellationToken_WhenCallingLoginAsync_ForwardsCancellationTokenToEveryRepositoryCall()
        {
            using var cts = new CancellationTokenSource();
            UserEntity user = ArrangeHappyPath();
            _tokenService
                .Setup(t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()))
                .Returns(TestData.AccessToken());

            await CreateSut().LoginAsync(TestData.LoginModel(), cts.Token);

            _authRepository.Verify(r => r.FindByIdentifierAsync(It.IsAny<string>(), cts.Token), Times.Once);
            _authRepository.Verify(r => r.HasPasswordAsync(user, cts.Token), Times.Once);
            _authRepository.Verify(r => r.CheckPasswordAsync(user, It.IsAny<string>(), cts.Token), Times.Once);
            _authRepository.Verify(r => r.UpdateLastSeenAsync(user, It.IsAny<DateTime>(), cts.Token), Times.Once);
            _authRepository.Verify(r => r.GetRolesAsync(user, cts.Token), Times.Once);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenCallingLoginAsync_PassesCredentialsUnchanged()
        {
            UserEntity user = ArrangeHappyPath();
            _tokenService
                .Setup(t => t.CreateAccessToken(It.IsAny<UserModel>(), It.IsAny<IReadOnlyCollection<string>>()))
                .Returns(TestData.AccessToken());

            await CreateSut().LoginAsync(
                TestData.LoginModel(identfier: "ada.lovelace", password: "s3cret-passphrase"),
                CancellationToken.None);

            _authRepository.Verify(r => r.FindByIdentifierAsync("ada.lovelace", It.IsAny<CancellationToken>()), Times.Once);
            _authRepository.Verify(r => r.CheckPasswordAsync(user, "s3cret-passphrase", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GivenRepositoryFailure_WhenCallingLoginAsync_Propagates()
        {
            _authRepository
                .Setup(r => r.FindByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database is down"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().LoginAsync(TestData.LoginModel(), CancellationToken.None));
        }

        [Fact]
        public async Task GivenRepeatedFailures_WhenCallingLoginAsync_LocksOut()
        {
            var repository = new InMemoryAuthRepository
            {
                MaxFailedAttempts = 3
            };
            UserEntity user = TestData.User();
            repository.Seed(user, TestData.ValidPassword);

            var sut = new AuthOrchestrator(repository, new StubTokenService(), NullLogger<AuthOrchestrator>.Instance);
            LoginModel wrong = TestData.LoginModel(password: "wrong-password");

            Assert.Equal(OperationStatus.Unauthorized, (await sut.LoginAsync(wrong, CancellationToken.None)).Status);
            Assert.Equal(OperationStatus.Unauthorized, (await sut.LoginAsync(wrong, CancellationToken.None)).Status);

            OperationResult<LoginResultModel> third = await sut.LoginAsync(wrong, CancellationToken.None);

            Assert.Equal(OperationStatus.Forbidden, third.Status);
            Assert.Equal("AccountLockedOut", third.Errors.Single().Code);
        }

        [Fact]
        public async Task GivenValidCredentianls_WhenCallingLoginAsync_SucceedsStampsLastSeen()
        {
            var repository = new InMemoryAuthRepository();
            UserEntity user = TestData.User();
            repository.Seed(user, TestData.ValidPassword, "Admin");

            var tokenService = new StubTokenService();
            var sut = new AuthOrchestrator(repository, tokenService, NullLogger<AuthOrchestrator>.Instance);

            OperationResult<LoginResultModel> result = await sut.LoginAsync(TestData.LoginModel(), CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(1, tokenService.CallCount);
            Assert.Equal(new[] { "Admin" }, tokenService.LastRoles!);
            Assert.NotNull(repository.LastSeenWritten);
            Assert.Equal(repository.LastSeenWritten, user.LastSeen);
        }
    }
}
