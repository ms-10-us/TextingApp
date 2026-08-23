using System;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Models;
using UserMicroService.Security;

namespace UserMicroServiceUnitTests
{
    public sealed class StubTokenService : ITokenService
    {
        public static readonly DateTime FixedExpiry = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly AccessTokenModel _token;

        public StubTokenService(AccessTokenModel? token = null)
        {
            _token = token ?? new AccessTokenModel
            {
                Token = "stub-token",
                ExpiresAtUtc = FixedExpiry
            };
        }

        public int CallCount { get; private set;  }

        public UserModel? LastUser { get; private set;  }

        public IReadOnlyCollection<string>? LastRoles { get; private set;  }

        public AccessTokenModel CreateAccessToken(UserModel user, IReadOnlyCollection<string> roles)
        {
            CallCount++;
            LastUser = user;
            LastRoles = roles;
            return _token;
        }
    }
}
