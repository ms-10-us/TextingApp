using Castle.Core.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UserMicroService.Entities;
using UserMicroService.Models;
using UserMicroService.Repositories;

namespace UserMicroServiceUnitTests.Repositories
{
    public class AuthRepositoryTests
    {

        private static (AuthRepository Sut, 
            Mock<UserManager<UserEntity>> UserManager, 
            Mock<SignInManager<UserEntity>> SignInManager, 
            Mock<ILogger<AuthRepository>> Logger)
            CreateSut(IEnumerable<UserEntity>? users = null)
        {
            Mock<UserManager<UserEntity>> userManager = IdentityMockFactory.CreateUserManager(users);
            Mock<SignInManager<UserEntity>> signInManager = IdentityMockFactory.CreateSignInManager(userManager.Object);
            var logger = new Mock<ILogger<AuthRepository>>();

            return (new AuthRepository(userManager.Object, signInManager.Object, logger.Object),
                userManager,
                signInManager,
                logger);

        }

        // ------------------------------------------------------- FindByIdentifierAsync

        [Fact]
        public async Task GivenIdentifier_WhenCallingFindByIdentifierAsync_MatchesOnEmail()
        {
            UserEntity user = TestData.User(email: "ada@example.com", userName: "ada.lovelace");
            var (sut, _, _, _) = CreateSut(new[] {user});

            UserEntity? found = await sut.FindByIdentifierAsync("ada@example.com", CancellationToken.None);

            Assert.Equal(user.Id, found!.Id);
        }

        [Fact]
        public async Task GivenIdentifier_WhenCallingFindByIdentifierAsync_MatchesOnUserName()
        {
            UserEntity user = TestData.User(email: "ada@example.com", userName: "ada.lovelace");
            var (sut, _, _, _) = CreateSut(new[] { user });

            UserEntity? found = await sut.FindByIdentifierAsync("ada.lovelace", CancellationToken.None);

            Assert.Equal(user.Id, found!.Id);
        }

        [Theory]
        [InlineData("ADA@EXAMPLE.COM")]
        [InlineData("Ada@Example.com")]
        [InlineData("ADA.LOVELACE")]
        public async Task GivenIdentifier_WhenCallingFindByIdentifierAsync_IgnoresCasing(string identifier)
        {
            var (sut, _, _, _) = CreateSut(new[] { TestData.User() });

            Assert.NotNull(await sut.FindByIdentifierAsync(identifier, CancellationToken.None));
        }

        [Fact]
        public async Task GivenEmptyUserTable_WhenCallingFindByIdentifierAsync_ReturnsNull()
        {
            var (sut, _, _, _) = CreateSut();

            Assert.Null(await sut.FindByIdentifierAsync("ada@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task GivenIdentifier_WhenCallingFindIdentifierAsync_NormalizeBothEmailAndUserName()
        {
            var (sut, userManager, _, _) = CreateSut();

            await sut.FindByIdentifierAsync("ada@example.com", CancellationToken.None);

            userManager.Verify(m => m.NormalizeEmail("ada@example.com"), Times.Once);
            userManager.Verify(m => m.NormalizeName("ada@example.com"), Times.Once);
        }

        [Fact]
        public async Task GivenOneUsersEmailMatchingAnotherUserName_WhenCallingFindByIdentifierAsync_ThrowsExceptions()
        {
            var (sut, _, _, _) = CreateSut(new[]
            {
                TestData.User(email: "collide@example.com", userName: "someone"),
                TestData.User(email: "other@example.com", userName: "collide@example.com")
            });

            await Assert.ThrowsAsync<TargetInvocationException>(
                () => sut.FindByIdentifierAsync("collide@example.com", CancellationToken.None));
        }

        // ----------------------------------------------------------- CheckPasswordAsync

        [Fact]
        public async Task GivenNullUser_WhenCallingCheckPasswordAsync_ThrowsExceptions()
        {
            var (sut, _, _, _) = CreateSut();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.CheckPasswordAsync(null!, TestData.ValidPassword, CancellationToken.None));
        }

        [Fact]
        public async Task GivenAlreadyCancelledToken_WhenCallingCheckPasswordAsync_ThrowsExceptions()
        {
            var (sut, _, signInManager, _) = CreateSut();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.CheckPasswordAsync(TestData.User(), TestData.ValidPassword, cts.Token));

            signInManager.Verify(
                s => s.CheckPasswordSignInAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public async Task GivenValidPassword_WhenCallingCheckPasswordAsync_ReturnsSucceeded()
        {
            var (sut, _, singInManager, _) = CreateSut();
            singInManager
                .Setup(s => s.CheckPasswordSignInAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(SignInResult.Success);

            CredentialCheckResult result = await sut.CheckPasswordAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Equal(CredentialCheckResult.Succeeded, result);
        }

        [Fact]
        public async Task GivenAccountLockedOut_WhenCallingCheckPasswordAsync_LogsWarning()
        {
            var (sut, _, signInManager, logger) = CreateSut();
            signInManager
                .Setup(s => s.CheckPasswordSignInAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(SignInResult.LockedOut);

            await sut.CheckPasswordAsync(TestData.User(), "wrong", CancellationToken.None);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    )
                );
        }

        [Fact]
        public async Task GivenIdentifierNotAllowed_WhenCallingCheckPasswordAsync_ReturnsNotAllowed()
        {
            var (sut, _, signInManager, _) = CreateSut();
            signInManager
                .Setup(s => s.CheckPasswordSignInAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(SignInResult.NotAllowed);

            CredentialCheckResult result = await sut.CheckPasswordAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Equal(CredentialCheckResult.NotAllowed, result);
        }

        [Fact]
        public async Task GivenPlainFailure_WhenCallingCheckPassword_ReturnsInvalidPassword()
        {
            var (sut, _, signInManager, _) = CreateSut();
            signInManager
                .Setup(s => s.CheckPasswordSignInAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(SignInResult.Failed);

            CredentialCheckResult result = await sut.CheckPasswordAsync(TestData.User(), "wrong", CancellationToken.None);

            Assert.Equal(CredentialCheckResult.InvalidPassword, result);
        }

        [Fact]
        public async Task GivenTwoFactorRequired_WhenCallingCheckPasswordAsync_FallsThroughToInvalidPassword()
        {
            var (sut, _, signInManager, _) = CreateSut();
            signInManager
                .Setup(s => s.CheckPasswordSignInAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(SignInResult.TwoFactorRequired);

            CredentialCheckResult result = await sut.CheckPasswordAsync(TestData.User(), TestData.ValidPassword, CancellationToken.None);

            Assert.Equal(CredentialCheckResult.InvalidPassword, result);
        }

        [Fact]
        public async Task GivenInvalidCredentials_WhenCallingCheckPasswordAsync_AlwaysEnablesLockoutOnFailure()
        {
            var (sut, _, signInManager, _) = CreateSut();
            UserEntity user = TestData.User();
            signInManager
                .Setup(s => s.CheckPasswordSignInAsync(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(SignInResult.Failed);

            CredentialCheckResult result = await sut.CheckPasswordAsync(user, "wrong", CancellationToken.None);

            signInManager.Verify(s => s.CheckPasswordSignInAsync(user, "wrong", true), Times.Once);
        }

        // --------------------------------------------------------------- GetRolesAsync
        [Fact]
        public async Task GivenValidUser_WhenCallingGetRolesAsync_ReturnsEveryRole()
        {
            var (sut, userManager, _, _) = CreateSut();
            UserEntity user = TestData.User();
            userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin", "Moderator" });

            IReadOnlyCollection<string> roles = await sut.GetRolesAsync(user, CancellationToken.None);

            Assert.Equal(new[] { "Admin", "Moderator" }, roles);

        }

        [Fact]
        public async Task GivenNoRoles_WhenCallingGetRolesAsync_ReturnsEmptyCollection()
        {
            var (sut, userManager, _, _) = CreateSut();
            UserEntity user = TestData.User();
            userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            IReadOnlyCollection<string> roles = await sut.GetRolesAsync(user, CancellationToken.None);

            Assert.Empty(roles);
        }

        [Fact]
        public async Task GivenIdentityList_WhenCallingGetRolesAsync_ReturnsSnaphshotDetachedFromIdentityList()
        {
            var (sut, userManager, _, _) = CreateSut();
            UserEntity user = TestData.User();
            var backing = new List<string> { "Admin" };
            userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(backing);

            IReadOnlyCollection<string> roles = await sut.GetRolesAsync(user, CancellationToken.None);
            backing.Add("Sneaked-In");

            Assert.Single(roles);
        }

        [Fact]
        public async Task GivenAlreadyCancelledToken_WhenCallingGetRolesAsync_ThrowsException()
        {
            var (sut, _, _, _) = CreateSut();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.GetRolesAsync(TestData.User(), cts.Token));
        }

        // ------------------------------------------------------------ HasPasswordAsync

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GivenIndetityAnswer_WhenCallingHasPasswordAsync_ForwardsIdentitysAnswer(bool hasPassword)
        {
            var (sut, userManager, _, _) = CreateSut();
            UserEntity user = TestData.User();
            userManager.Setup(m => m.HasPasswordAsync(user)).ReturnsAsync(hasPassword);

            Assert.Equal(hasPassword, await sut.HasPasswordAsync(user, CancellationToken.None));
        }

        [Fact]
        public async Task GivenAlreadyCancelledToken_WhenCallingHasPasswordAsync_ThrowsException()
        {
            var (sut, userManager, _, _) = CreateSut();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.HasPasswordAsync(TestData.User(), cts.Token));

            userManager.Verify(m => m.HasPasswordAsync(It.IsAny<UserEntity>()), Times.Never);
        }

        // --------------------------------------------------------- UpdateLastSeenAsync

        [Fact]
        public async Task GivenTimeStamp_WhenCallingUpdateLastSeenAsync_AssignsTimeStampPersistsUser()
        {
            var (sut, userManager, _, _) = CreateSut();
            UserEntity user = TestData.User();
            DateTime stamp = new DateTime(2026, 5, 5, 9, 30, 0, DateTimeKind.Utc);
            userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

            await sut.UpdateLastSeenAsync(user, stamp, CancellationToken.None);

            Assert.Equal(stamp, user.LastSeen);
            userManager.Verify(m => m.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task GivenIdentityFailure_WhenCallingUpdateLastSeenAsync_DoesNotThrowException()
        {
            var (sut, userManager, _, _) = CreateSut();
            UserEntity user = TestData.User();
            userManager
                .Setup(m => m.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError
                {
                    Code = "ConcurrencyFailure",
                    Description = "stale"
                }));

            await sut.UpdateLastSeenAsync(user, DateTime.UtcNow, CancellationToken.None);
        }

        [Fact]
        public async Task GivenIdentityFailure_WhenCallingUpdateLastSeenAsync_LogsWarning()
        {
            var (sut, userManager, _, logger) = CreateSut();
            UserEntity user = TestData.User();
            userManager
                .Setup(m => m.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError
                {
                    Code = "ConcurrencyFailure",
                    Description = "stale"
                }));

            await sut.UpdateLastSeenAsync(user, DateTime.UtcNow, CancellationToken.None);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    ),
                Times.Once
                );
        }

        [Fact]
        public async Task GivenSuccess_WhenCallingUpdateLastSeenAsync_LogsNothing()
        {
            var (sut, userManager, _, logger) = CreateSut();
            UserEntity user = TestData.User();
            userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

            await sut.UpdateLastSeenAsync(user, DateTime.UtcNow, CancellationToken.None);

            logger.Verify(
                l => l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    ),
                Times.Never
                );
        }

        [Fact]
        public async Task GivenAlreadCancellationToken_WhenCallingUpdateLastSeenAsync_ThrowsExcpetionWithoutMutatingUser()
        {
            var (sut, userManager, _, _) = CreateSut();
            UserEntity user = TestData.User();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.UpdateLastSeenAsync(user, DateTime.UtcNow, cts.Token));

            Assert.Null(user.LastSeen);
            userManager.Verify(m => m.UpdateAsync(It.IsAny<UserEntity>()), Times.Never);
        }

    }
}
