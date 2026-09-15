using NSubstitute;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.Orchestrators;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Security;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Orchestrators
{
    public class DeviceAuthenticatorTests
    {
        private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
        private readonly IDeviceCredentialHasher _hasher = new Sha256DeviceCredentialHasher();
        private readonly DeviceAuthenticator _authenticator;

        public DeviceAuthenticatorTests()
        {
            _authenticator = new DeviceAuthenticator(_devices, _hasher);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AuthenticateAsync_WithoutACredential_FailsWithoutTouchingTheDatabase(string? credential)
        {
            var address = Any.Address();

            var result = await _authenticator.AuthenticateAsync(address, credential);

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.Unauthorized, result.Error!.Code);

            // Short-circuiting matters: an unauthenticated caller should not be able to make the
            // service do work, and a missing credential is not a database question.
            await _devices.DidNotReceiveWithAnyArgs().GetCredentialHashAsync(default, default);
        }

        [Fact]
        public async Task AuthenticateAsync_WhenNoDeviceIsRegistered_IsUnauthorizedRatherThanNotFound()
        {
            var address = Any.Address();
            _devices.GetCredentialHashAsync(address, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

            var result = await _authenticator.AuthenticateAsync(address, "anything");

            // Deliberately NOT DeviceNotFound: telling an unauthenticated caller whether an address
            // exists turns this endpoint into a device enumerator.
            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.Unauthorized, result.Error!.Code);
        }

        [Fact]
        public async Task AuthenticateAsync_WithTheWrongCredential_Fails()
        {
            var address = Any.Address();
            _devices.GetCredentialHashAsync(address, Arg.Any<CancellationToken>())
                .Returns(_hasher.Hash("the real credential"));

            var result = await _authenticator.AuthenticateAsync(address, "not the real credential");

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.Unauthorized, result.Error!.Code);
        }

        [Fact]
        public async Task AuthenticateAsync_WithTheRightCredential_ReturnsTheAddress()
        {
            var address = Any.Address();
            var credential = _hasher.Generate();
            _devices.GetCredentialHashAsync(address, Arg.Any<CancellationToken>())
                .Returns(_hasher.Hash(credential));

            var result = await _authenticator.AuthenticateAsync(address, credential);

            Assert.True(result.IsSuccess);
            Assert.Equal(address, result.Value);
        }

        [Fact]
        public async Task AuthenticateAsync_IsScopedToOneAddress()
        {
            var account = Any.Account();
            var device1 = Any.AddressOn(account, 1);
            var device2 = Any.AddressOn(account, 2);

            var credential1 = _hasher.Generate();
            _devices.GetCredentialHashAsync(device1, Arg.Any<CancellationToken>()).Returns(_hasher.Hash(credential1));
            _devices.GetCredentialHashAsync(device2, Arg.Any<CancellationToken>()).Returns(_hasher.Hash("other"));

            // Device 1's credential must not authenticate device 2, even on the same account.
            var result = await _authenticator.AuthenticateAsync(device2, credential1);

            Assert.False(result.IsSuccess);
        }
    }
}
