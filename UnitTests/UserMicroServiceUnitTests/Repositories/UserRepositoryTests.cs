using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Entities;
using UserMicroService.Models;
using UserMicroService.Repositories;

namespace UserMicroServiceUnitTests.Repositories
{
    public class UserRepositoryTests
    {
        private static UserRepository CreateSut(
            Mock<UserManager<UserEntity>> userManager,
            ILogger<UserRepository>? logger = null)
        {
            return new UserRepository(userManager.Object, logger ?? NullLogger<UserRepository>.Instance);
        }

        // ------------------------------------------------------------- EmailExistsAsync

        [Fact]
        public async Task GivenMatchingNormalizedEmail_WhenCallingEmailExistsAsync_ReturnsTrue()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(
                new[] { TestData.User(email: "ada@example.com") });

            bool exists = await CreateSut(manager).EmailExistsAsync("ada@example.com", CancellationToken.None);

            Assert.True(exists);
        }

        [Fact]
        public async Task GivenEmail_WhenCallingEmailExistsAsync_IsCaseInsensitive()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(
                new[] { TestData.User(email: "ada@example.com") });

            bool exists = await CreateSut(manager).EmailExistsAsync("ADA@Example.COM", CancellationToken.None);

            Assert.True(exists);
        }

        [Fact]
        public async Task GivenNoMatch_WhenCallingEmailExistsAsync_ReturnsFalse()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(
                new[] { TestData.User(email: "ada@example.com") });

            bool exists = await CreateSut(manager).EmailExistsAsync("grace@example.com", CancellationToken.None);

            Assert.False(exists);
        }

        [Fact]
        public async Task GivenEmptyUserTable_EmailExistsAsync_ReturnsFalse()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();

            bool exists = await CreateSut(manager).EmailExistsAsync("ada@example.com", CancellationToken.None);

            Assert.False(exists);
        }

        [Fact]
        public async Task GivenEmailExists_WhenCallingEmailExistsAsync_NormalizedThroughUserManager()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();

            await CreateSut(manager).EmailExistsAsync("ada@example.com", CancellationToken.None);

            manager.Verify(m => m.NormalizeEmail("ada@example.com"), Times.Once);
        }

        // ---------------------------------------------------------- UserNameExistsAsync

        [Fact]
        public async Task GivenMatchingNormalizedUserName_WhenCallingUserNameExistsAsync_ReturnsTrue()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(
                new[] { TestData.User(userName: "ada.lovelace") });

            bool exists = await CreateSut(manager).UserNameExistsAsync("ADA.LOVELACE", CancellationToken.None);

            Assert.True(exists);    
        }

        [Fact]
        public async Task GivenNoMatch_WhenCallingUserNameExistsAsync_ReturnsFalse()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(
                new[] { TestData.User(userName: "ada.lovelace") });

            bool exists = await CreateSut(manager).UserNameExistsAsync("grace.hopper", CancellationToken.None);

            Assert.False(exists);
        }

        [Fact]
        public async Task GivenEmail_WhenCallingUserNameExistsAsync_DoesNotMatchOnEmailColumn()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(
                new[] { TestData.User(email: "ada@example.com", userName: "ada.lovelace") });

            bool exists = await CreateSut(manager).UserNameExistsAsync("ada@example.com", CancellationToken.None);

            Assert.False(exists);
        }

        // ------------------------------------------------------------------ CreateAsync

        [Fact]
        public async Task GivenNullUser_WhenCallingCreateAsync_ThrowsException()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => CreateSut(manager).CreateAsync(null!, TestData.ValidPassword, CancellationToken.None));
        }

        [Fact]
        public async Task GivenCancellationToken_WhenCallingCreateAsync_ThrowsException()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => CreateSut(manager).CreateAsync(TestData.User(), TestData.ValidPassword, cts.Token));

            manager.Verify(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GivenIdentitySucceeds_WhenCallingCreateAsync_ReturnsSameEntityInstance()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager.Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

            UserEntity user = TestData.User();

            OperationResult<UserEntity> result = await CreateSut(manager)
                .CreateAsync(user, TestData.ValidPassword, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Same(user, result.Value);
            Assert.Empty(result.Errors);
        }

        [Theory]
        [InlineData("DuplicateEmail")]
        [InlineData("DuplicateUserName")]
        public async Task GivenDuplicateIdentityErrors_WhenCallingCreateAsync_MapToConflict(string code)
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager.Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError
                {
                    Code = code,
                    Description = "duplicate"
                }));

            OperationResult<UserEntity> result = await CreateSut(manager)
                .CreateAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Equal(OperationStatus.Conflict, result.Status);
            Assert.Equal(code, Assert.Single(result.Errors).Code);
            Assert.Null(result.Value);
        }

        [Theory]
        [InlineData("PasswordTooShort")]
        [InlineData("InvalidUserName")]
        [InlineData("PasswordRequiresDigit")]
        public async Task GivenNonDuplicateIdentityErrors_WhenCallingCreateAsync_MapToValidationFailed(string code)
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager.Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError
                {
                    Code = code,
                    Description = "invalid"
                }));

            OperationResult<UserEntity> result = await CreateSut(manager)
                .CreateAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        }

        [Fact]
        public async Task GivenMixedErrors_WhenCallingCreateAsync_PrefersConflictWhenDuplicatePresent()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager.Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Code = "PasswordTooShort", Description = "too short" },
                new IdentityError { Code = "DuplicateEmail", Description = "duplicate" }
            ));

            OperationResult<UserEntity> result = await CreateSut(manager)
                .CreateAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Equal(OperationStatus.Conflict, result.Status);
            Assert.Equal(2 , result.Errors.Count);
        }

        [Fact]
        public async Task GivenConflictCode_WhenCallingCreateAsync_ConflictCodeMatchingCaseSensitive()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager
                .Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError
                {
                    Code = "duplicateemail",
                    Description = "d"
                }));

            OperationResult<UserEntity> result = await CreateSut(manager)
                .CreateAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        }

        [Fact]
        public async Task GivenIdentityError_WhenCallingCreateAsync_FailureCopiesCodeAndDescription()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager.Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be 12 characters." },
                    new IdentityError { Code = "PasswordRequiresDigit", Description = "Passwords must have a digit." }));

            OperationResult<UserEntity> result = await CreateSut(manager)
                .CreateAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Collection(
                result.Errors,
                e =>
                {
                    Assert.Equal("PasswordTooShort", e.Code);
                    Assert.Equal("Passwords must be 12 characters.", e.Description);
                },
                e =>
                {
                    Assert.Equal("PasswordRequiresDigit", e.Code);
                    Assert.Equal("Passwords must have a digit.", e.Description);
                });
        }

        [Fact]
        public async Task GivenFailure_WhenCallingCreateAsync_LogsWarning()
        {
            var logger = new Mock<ILogger<UserRepository>>();
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager.Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordTooShort",
                    Description = "d"
                }));

            await CreateSut(manager, logger.Object).CreateAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GivenSuccess_WhenCallingCreateAsync_LogsNothing()
        {
            var logger = new Mock<ILogger<UserRepository>>();
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager();
            manager.Setup(m => m.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            await CreateSut(manager, logger.Object).CreateAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            logger.Verify(
                l => l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        // ----------------------------------------------------------------- GetByIdAsync

        [Fact]
        public async Task GivenExistingId_WhenCallingGetByIdAsync_ReturnsUser()
        {
            UserEntity user = TestData.User();
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(new[] { user });

            UserEntity? found = await CreateSut(manager).GetByIdAsync(user.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(user.Id, found!.Id);
        }

        [Fact]
        public async Task GivenUnkownId_WhenCallingGetByIdAsync_ReturnsNull()
        {
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(new[] {TestData.User()});

            UserEntity? user = await CreateSut(manager).GetByIdAsync(Guid.CreateVersion7(), CancellationToken.None);

            Assert.Null(user);
        }

        [Fact]
        public async Task GivenMultipleRows_WhenCallingGetByIdAsync_PicksRightRow()
        {
            UserEntity target = TestData.User(email: "target@example.com", userName: "target");
            Mock<UserManager<UserEntity>> manager = IdentityMockFactory.CreateUserManager(
                new[] { TestData.User(), target, TestData.User(email: "third@example.com", userName: "third") });

            UserEntity? found = await CreateSut(manager).GetByIdAsync(target.Id, CancellationToken.None);

            Assert.Equal("target@example.com", found!.Email);
        }



    }
}
