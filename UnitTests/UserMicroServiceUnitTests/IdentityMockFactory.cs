using Castle.Core.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Entities;
using MockQueryable.Moq;
using MockQueryable;

namespace UserMicroServiceUnitTests
{
    public static class IdentityMockFactory
    {
        public static Mock<UserManager<UserEntity>> CreateUserManager(IEnumerable<UserEntity>? users = null)
        {
            var store = new Mock<IUserStore<UserEntity>>();

            var manager = new Mock<UserManager<UserEntity>>(
                store.Object,
                null!,   // IOptions<IdentityOptions>
                null!,   // IPasswordHasher<UserEntity>
                null!,   // IEnumerable<IUserValidator<UserEntity>>
                null!,   // IEnumerable<IPasswordValidator<UserEntity>>
                null!,   // ILookupNormalizer
                null!,   // IdentityErrorDescriber
                null!,   // IServiceProvider
                null!)   // ILogger<UserManager<UserEntity>>
            {
                CallBase = false
            };

            manager.Setup(m => m.Users).Returns((users ?? Array.Empty<UserEntity>()).ToList().BuildMock());

            manager.Setup(m => m.NormalizeEmail(It.IsAny<string>()))
                .Returns((string? value) => value is null ? null! : value.Normalize().ToUpperInvariant());

            manager.Setup(m => m.NormalizeName(It.IsAny<string>()))
                .Returns((string? value) => value is null ? null! : value.Normalize().ToUpperInvariant());

            return manager;
        }

        public static Mock<SignInManager<UserEntity>> CreateSignInManager(UserManager<UserEntity> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            contextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<UserEntity>>();

            return new Mock<SignInManager<UserEntity>>(
                userManager,
                contextAccessor.Object,
                claimsFactory.Object,
                Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<UserEntity>>.Instance,
                Mock.Of<IAuthenticationSchemeProvider>(),
                Mock.Of<IUserConfirmation<UserEntity>>())
            {
                CallBase = false
            };

        }
    }
}
