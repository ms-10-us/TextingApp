using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Entities;
using UserMicroService.Models;
using UserMicroService.Orchestrators;
using UserMicroService.Repositories;

namespace UserMicroServiceUnitTests.Orchestrators
{
    public class UserOrchestratorTests
    {
        private readonly Mock<IUserRepository> _userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        private readonly Mock<ILogger<UserOrchestrator>> _logger = new Mock<ILogger<UserOrchestrator>>();

        private UserOrchestrator CreateSut() => new UserOrchestrator(_userRepository.Object, _logger.Object);

        private void ArrangeNoExistingUser()
        {
            _userRepository
                .Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _userRepository
                .Setup(r => r.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        // ---------------------------------------------------------------- AddUserAsync

        [Fact]
        public async Task GivenNullModel_WhenCallingAddUserAsync_ThrowsException()
        { 
            await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().AddUserAsync(null!, CancellationToken.None));           
        }

        [Fact]
        public async Task GivenEmailAlreadyRegistered_WhenCallingAddUserAsync_ReturnsConflict()
        {
            _userRepository
                .Setup(r => r.EmailExistsAsync("ada@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            OperationResult<UserModel> result = await CreateSut()
                .AddUserAsync(TestData.AddUserModel(), CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(OperationStatus.Conflict, result.Status);
            Assert.Equal("EmailAlreadyRegistered", Assert.Single(result.Errors).Code);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task GivenEmailAlreadyRegistered_WhenCallingAddUserAsync_DontCallRepository()
        {
            _userRepository
                .Setup(r => r.EmailExistsAsync("ada@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            await CreateSut().AddUserAsync(TestData.AddUserModel(), CancellationToken.None);

            _userRepository
                .Verify(r => r.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _userRepository
                .Verify(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GivenNameAlreadyTaken_WhenCallingAddUserAsync_ReturnsConflict()
        {
            _userRepository
                .Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _userRepository
                .Setup(r => r.UserNameExistsAsync("ada.lovelace", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            OperationResult<UserModel> result = await CreateSut().AddUserAsync(TestData.AddUserModel(), CancellationToken.None);

            Assert.Equal(OperationStatus.Conflict, result.Status);
            Assert.Equal("UserNameAlreadyTaken", Assert.Single(result.Errors).Code);
            _userRepository.Verify(
                r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GivenValidModel_WhenCallingAddUserAsync_ReturnsMappedUser()
        {
            ArrangeNoExistingUser();
            UserEntity? created = null;
            _userRepository
                .Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<UserEntity, string, CancellationToken>((entity, _, _) => created = entity)
                .ReturnsAsync((UserEntity entity, string _, CancellationToken _) => OperationResult<UserEntity>.Success(entity));

            OperationResult<UserModel> result = await CreateSut().AddUserAsync(TestData.AddUserModel(), CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(created);
            Assert.Equal(created!.Id, result.Value!.Id);
            Assert.Equal("ada@example.com", result.Value.Email);
            Assert.Equal("ada.lovelace", result.Value.UserName);
            Assert.Equal("Ada Lovelace", result.Value.DisplayName);
            Assert.Null(result.Value.LastSeen);
        }

        [Fact]
        public async Task GivenPlainTextPassword_WhenCallingAddUserAsync_CallsrepositoryForHashing()
        {
            ArrangeNoExistingUser();
            _userRepository
                .Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), TestData.ValidPassword, It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity entity, string _, CancellationToken _) => OperationResult<UserEntity>.Success(entity));

            await CreateSut().AddUserAsync(TestData.AddUserModel(), CancellationToken.None);

            _userRepository.Verify(
                r => r.CreateAsync(It.IsAny<UserEntity>(), TestData.ValidPassword, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GivenValidModel_WhenCallingAddUserAsync_LogsRegistration()
        {
            ArrangeNoExistingUser();
            _userRepository
                .Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity entity, string _, CancellationToken _) => OperationResult<UserEntity>.Success(entity));

            await CreateSut().AddUserAsync(TestData.AddUserModel(), CancellationToken.None);


            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    )
                );
        }

        [Theory]
        [InlineData(OperationStatus.Conflict, "DuplicateEmail")]
        [InlineData(OperationStatus.ValidationFailed, "PasswordTooShort")]
        [InlineData(OperationStatus.Unexpected, "UnexpectedFailure")]
        public async Task GivenRepositoryFailure_WhenCallingAddUserAsync_FarwardsStatusAndError(OperationStatus status, string code)
        {
            ArrangeNoExistingUser();
            _userRepository
                .Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<UserEntity>.Failure(status, new OperationError(code, "description")));

            OperationResult<UserModel> result = await CreateSut().AddUserAsync(TestData.AddUserModel(), CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(status, result.Status);
            Assert.Equal(code, Assert.Single(result.Errors).Code);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task GivenSeveralRepositoryErrors_WhenCallingAddUserAsync_ForwardsThemAll()
        {
            ArrangeNoExistingUser();
            _userRepository
                .Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<UserEntity>.Failure(
                    OperationStatus.ValidationFailed,
                    new OperationError("PasswordTooShort", "Too short."),
                    new OperationError("PasswordRequiresDigit", "Needs a digit.")
                    ));

            OperationResult<UserModel> result = await CreateSut().AddUserAsync(TestData.AddUserModel(), CancellationToken.None);

            Assert.Equal(2, result.Errors.Count);
        }

        [Fact]
        public async Task GivenCancellationToken_WhenCallingAddUserAsync_PassCancellationTokenToAllRespositoryCall()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            ArrangeNoExistingUser();

            _userRepository
                .Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity entity, string _, CancellationToken _) => OperationResult<UserEntity>.Success(entity));

            await CreateSut().AddUserAsync(TestData.AddUserModel(), cts.Token);

            _userRepository.Verify(r => r.EmailExistsAsync(It.IsAny<string>(), cts.Token), Times.Once);
            _userRepository.Verify(r => r.UserNameExistsAsync(It.IsAny<string>(), cts.Token), Times.Once);
            _userRepository.Verify(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), cts.Token), Times.Once);
        }

        [Fact]
        public async Task GivenSameEmailRegistration_WhenCallingAddUserAsync_RejectsSecondRegistration()
        {
            var repository = new InMemoryUserRepository();
            var sut = new UserOrchestrator(repository, NullLogger<UserOrchestrator>.Instance);


            OperationResult<UserModel> first = await sut.AddUserAsync(TestData.AddUserModel(), CancellationToken.None);
            OperationResult<UserModel> second = await sut.AddUserAsync(
                TestData.AddUserModel(userName: "different.name"), CancellationToken.None);

            Assert.True(first.Succeeded);
            Assert.Equal(OperationStatus.Conflict, second.Status);
            Assert.Equal("EmailAlreadyRegistered", second.Errors.Single().Code);
            Assert.Single(repository.Users);
        }

        [Fact]
        public async Task GivenDuplicateUserName_WhenCallingAddUserAsync_RejectsDusplicateUserName()
        {
            var repository = new InMemoryUserRepository();
            var sut = new UserOrchestrator(repository, NullLogger<UserOrchestrator>.Instance);

            await sut.AddUserAsync(TestData.AddUserModel(), CancellationToken.None);
            OperationResult<UserModel> second = await sut.AddUserAsync(
                TestData.AddUserModel(email: "other@example.com"), CancellationToken.None);

            Assert.Equal("UserNameAlreadyTaken", second.Errors.Single().Code);
        }

        // ---------------------------------------------------------------- GetUserAsync
        
        [Fact]
        public async Task GivenUserExists_WhenCallingGetUserAsync_ReturnsMappedModel()
        {
            UserEntity entity = TestData.User(lastSeen: new DateTime(2026, 4, 4, 10, 0, 0, 0, DateTimeKind.Utc));
            _userRepository
                .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);

            OperationResult<UserModel> result = await CreateSut().GetUserAsync(entity.Id, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(entity.Id, result.Value!.Id);
            Assert.Equal(entity.Email, result.Value.Email);
            Assert.Equal(entity.UserName, result.Value.UserName);
            Assert.Equal(entity.DisplayName, result.Value.DisplayName);
            Assert.Equal(entity.LastSeen, result.Value.LastSeen);
        }

        [Fact]
        public async Task GivenMissingUser_WhenCallingGetUserAsync_ReturnsNotFound()
        {
            Guid userId = Guid.CreateVersion7();
            _userRepository
                .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity?)null);

            OperationResult<UserModel> result = await CreateSut().GetUserAsync(userId, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(OperationStatus.NotFound, result.Status);
            Assert.Equal("UserProfileNotFound", Assert.Single(result.Errors).Code);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task GivenEmptyGuid_WhenCallingGetUserAsync_IsTreatedLikeAnyOtherLookup()
        {
            _userRepository
                .Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserEntity?)null);

            OperationResult<UserModel> result = await CreateSut().GetUserAsync(Guid.Empty, CancellationToken.None);

            Assert.Equal(OperationStatus.NotFound, result.Status);
            _userRepository.Verify(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GivenCancellationToken_WhenCallingGetUserAsync_ForwardsCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            Guid userId = Guid.CreateVersion7();
            _userRepository
                .Setup(r => r.GetByIdAsync(userId, cts.Token))
                .ReturnsAsync((UserEntity?)null);

            await CreateSut().GetUserAsync(userId, cts.Token);

            _userRepository.Verify(r => r.GetByIdAsync(userId, cts.Token), Times.Once);
        }

        [Fact]
        public async Task GivenRepositoryFailure_WhenCallingGetUserAsync_Propagates()
        {
            Guid userId = Guid.CreateVersion7();
            _userRepository
                .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("Query Timed Out."));

            await Assert.ThrowsAsync<TimeoutException>(() => CreateSut().GetUserAsync(userId, CancellationToken.None));
        }

    }
}
