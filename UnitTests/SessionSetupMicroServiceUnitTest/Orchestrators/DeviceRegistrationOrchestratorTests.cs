using NSubstitute;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Options;
using SessionSetupMicroService.Orchestrators;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Security;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Orchestrators
{
    public class DeviceRegistrationOrchestratorTests
    {
        private readonly DirectUnitOfWork _unitOfWork = new();
        private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
        private readonly IPreKeyRepository _preKeys = Substitute.For<IPreKeyRepository>();
        private readonly IDeviceCredentialHasher _hasher = new Sha256DeviceCredentialHasher();
        private readonly IDeviceAuthenticator _authenticator = Substitute.For<IDeviceAuthenticator>();
        private readonly RecordingLogger<DeviceRegistrationOrchestrator> _logger = new();
        private readonly PreKeyPolicyOptions _policy = new() { LowWaterMark = 20, MaxPoolSize = 500, MaxKeysPerUpload = 200 };

        private DeviceRegistrationOrchestrator Create() => new(
            _unitOfWork,
            _devices,
            _preKeys,
            _hasher,
            _authenticator,
            Microsoft.Extensions.Options.Options.Create(_policy),
            new FixedClock(),
            _logger);

        // --- RegisterAsync -------------------------------------------------------

        [Fact]
        public async Task RegisterAsync_WithInvalidKeyMaterial_FailsWithoutWritingAnything()
        {
            var registration = Any.Registration() with { IdentityKey = Any.CurveKey() };

            var result = await Create().RegisterAsync(registration);

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.InvalidKeyMaterial, result.Error!.Code);

            // Nothing may touch the database before validation passes: a half-written device is
            // unreachable, and a failed registration the caller can retry is strictly better.
            Assert.Equal(0, _unitOfWork.Executions);
            await _devices.DidNotReceiveWithAnyArgs().InsertAsync(default, default!, default!);
        }

        [Fact]
        public async Task RegisterAsync_MintsANewAccountWithDeviceOne()
        {
            var result = await Create().RegisterAsync(Any.Registration());

            Assert.True(result.IsSuccess);
            Assert.Equal(DeviceId.Primary, result.Value!.Device.Address.Device.Value);
        }

        [Fact]
        public async Task RegisterAsync_NeverReusesAnAccountId()
        {
            var orchestrator = Create();

            var first = await orchestrator.RegisterAsync(Any.Registration());
            var second = await orchestrator.RegisterAsync(Any.Registration());

            Assert.NotEqual(first.Value!.Device.Address.Account, second.Value!.Device.Address.Account);
        }

        [Fact]
        public async Task RegisterAsync_DoesEverythingInOneUnitOfWork()
        {
            var registration = Any.Registration();

            await Create().RegisterAsync(registration);

            // One transaction, not six. A device row without prekeys cannot be reached, and a prekey
            // pool without a device row is an orphan.
            Assert.Equal(1, _unitOfWork.Executions);

            await _devices.Received(1).EnsureAccountAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>());
            await _devices.Received(1).InsertAsync(
                Arg.Any<ProtocolAddress>(), registration, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

            await _preKeys.Received(1).UpsertSignedPreKeyAsync(
                Arg.Any<ProtocolAddress>(), registration.SignedPreKey, Arg.Any<CancellationToken>());
            await _preKeys.Received(1).UpsertSignedPreKeyAsync(
                Arg.Any<ProtocolAddress>(), registration.LastResortKyberPreKey, Arg.Any<CancellationToken>());
            await _preKeys.Received(1).AddOneTimePreKeysAsync(
                Arg.Any<ProtocolAddress>(), registration.OneTimePreKeys, Arg.Any<CancellationToken>());
            await _preKeys.Received(1).AddOneTimePreKeysAsync(
                Arg.Any<ProtocolAddress>(), registration.OneTimeKyberPreKeys, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RegisterAsync_StoresOnlyTheHashOfTheCredentialItReturns()
        {
            byte[]? stored = null;
            _devices.WhenForAnyArgs(repository => repository.InsertAsync(default, default!, default!))
                .Do(call => stored = call.Arg<byte[]>());

            var result = await Create().RegisterAsync(Any.Registration());

            var credential = result.Value!.DeviceCredentials;
            Assert.False(string.IsNullOrWhiteSpace(credential));

            // The response is the one and only moment the credential exists in readable form.
            Assert.NotNull(stored);
            Assert.True(_hasher.Verify(credential, stored!));
            Assert.NotEqual(credential, System.Text.Encoding.UTF8.GetString(stored!));
        }

        [Fact]
        public async Task RegisterAsync_BelowTheLowWaterMark_Warns()
        {
            await Create().RegisterAsync(Any.Registration(curveCount: 3));

            // A device that registers nearly dry will start serving degraded bundles almost
            // immediately, and nothing else in the system would say so.
            Assert.Contains(_logger.Warnings, message => message.Contains("low-water mark"));
        }

        [Fact]
        public async Task RegisterAsync_AtOrAboveTheLowWaterMark_DoesNotWarn()
        {
            await Create().RegisterAsync(Any.Registration(curveCount: _policy.LowWaterMark));

            Assert.DoesNotContain(_logger.Warnings, message => message.Contains("low-water mark"));
        }

        // --- LinkDeviceAsync -----------------------------------------------------

        private readonly AccountId _account = Any.Account();

        private ProtocolAddress VouchingAddress => Any.AddressOn(_account, 1);

        private void GivenAVouchingDeviceThatAuthenticates(PublicKey? identityKey = null)
        {
            _authenticator.AuthenticateAsync(VouchingAddress, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Result<ProtocolAddress>.Success(VouchingAddress));

            var vouching = Any.Device(VouchingAddress);
            if (identityKey is not null)
            {
                vouching = vouching with { IdentityKey = identityKey };
            }

            _devices.LockAccountAsync(_account, Arg.Any<CancellationToken>()).Returns(true);
            _devices.FindAsync(VouchingAddress, Arg.Any<CancellationToken>()).Returns(vouching);
            _devices.NextDeviceIdAsync(_account, Arg.Any<CancellationToken>()).Returns(new DeviceId(2));
            _devices.InsertAsync(
                Arg.Any<ProtocolAddress>(), Arg.Any<RegisterDeviceModel>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        [Fact]
        public async Task LinkDeviceAsync_WithoutAValidCredential_IsUnauthorizedAndWritesNothing()
        {
            _authenticator.AuthenticateAsync(VouchingAddress, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Result<ProtocolAddress>.Failure(SessionSetupError.Unauthorized()));

            var result = await Create().LinkDeviceAsync(_account, new DeviceId(1), "wrong", Any.Registration());

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.Unauthorized, result.Error!.Code);

            // Without proof of an existing device, this endpoint would let anyone attach their own
            // device to someone else's account and start receiving bundles addressed to them.
            Assert.Equal(0, _unitOfWork.Executions);
            await _devices.DidNotReceiveWithAnyArgs().InsertAsync(default, default!, default!);
        }

        [Fact]
        public async Task LinkDeviceAsync_AuthenticatesOutsideTheTransaction()
        {
            _authenticator.AuthenticateAsync(VouchingAddress, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Result<ProtocolAddress>.Failure(SessionSetupError.Unauthorized()));

            await Create().LinkDeviceAsync(_account, new DeviceId(1), null, Any.Registration());

            // A failed attempt must not hold a lock on the account — otherwise anyone can stall
            // every link for that account by guessing credentials.
            Assert.Equal(0, _unitOfWork.Executions);
            await _devices.DidNotReceiveWithAnyArgs().LockAccountAsync(default);
        }

        [Fact]
        public async Task LinkDeviceAsync_WithInvalidKeyMaterial_FailsBeforeAuthenticating()
        {
            var registration = Any.Registration() with { IdentityKey = Any.CurveKey() };

            var result = await Create().LinkDeviceAsync(_account, new DeviceId(1), "credential", registration);

            Assert.Equal(SessionSetupErrorCode.InvalidKeyMaterial, result.Error!.Code);
            await _authenticator.DidNotReceiveWithAnyArgs().AuthenticateAsync(default, default);
        }

        [Fact]
        public async Task LinkDeviceAsync_WhenTheAccountDoesNotExist_IsDeviceNotFound()
        {
            _authenticator.AuthenticateAsync(VouchingAddress, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Result<ProtocolAddress>.Success(VouchingAddress));
            _devices.LockAccountAsync(_account, Arg.Any<CancellationToken>()).Returns(false);

            var result = await Create().LinkDeviceAsync(_account, new DeviceId(1), "credential", Any.Registration());

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.DeviceNotFound, result.Error!.Code);
        }

        [Fact]
        public async Task LinkDeviceAsync_LocksTheAccountBeforeAskingForTheNextDeviceId()
        {
            GivenAVouchingDeviceThatAuthenticates();

            // Recorded by hand rather than with Received.InOrder. That API matches the whole
            // sequence or nothing, so a single missing call reports "no matching calls" and tells
            // you nothing about which step was skipped. A list of names shows it immediately.
            var calls = new List<string>();
            _devices.WhenForAnyArgs(devices => devices.LockAccountAsync(default)).Do(_ => calls.Add("LOCK"));
            _devices.WhenForAnyArgs(devices => devices.NextDeviceIdAsync(default)).Do(_ => calls.Add("NEXT_ID"));
            _devices.WhenForAnyArgs(devices => devices.InsertAsync(default, default!, default!)).Do(_ => calls.Add("INSERT"));

            var result = await Create().LinkDeviceAsync(_account, new DeviceId(1), "credential", Any.Registration());

            Assert.True(result.IsSuccess, $"link failed with {result.Error?.Code}; calls were [{string.Join(", ", calls)}]");

            // NextDeviceId is max(device_id) + 1. Without the lock, two concurrent links compute the
            // same id and the second insert dies on the primary key.
            Assert.Equal(new[] { "LOCK", "NEXT_ID", "INSERT" }, calls);
        }

        [Fact]
        public async Task LinkDeviceAsync_WithADifferentIdentityKey_IsRejected()
        {
            GivenAVouchingDeviceThatAuthenticates(identityKey: Any.IdentityKey(seed: 99));

            var result = await Create().LinkDeviceAsync(_account, new DeviceId(1), "credential", Any.Registration());

            // Devices on one account share an identity key — that is what keeps a safety number
            // stable when someone adds a second device. Change this test only if you deliberately
            // move to per-device identity keys.
            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.InvalidKeyMaterial, result.Error!.Code);
            await _devices.DidNotReceiveWithAnyArgs().InsertAsync(default, default!, default!);
        }

        [Fact]
        public async Task LinkDeviceAsync_OnSuccess_AddsTheDeviceAtTheNextIdWithItsOwnCredential()
        {
            GivenAVouchingDeviceThatAuthenticates();

            var result = await Create().LinkDeviceAsync(_account, new DeviceId(1), "credential", Any.Registration());

            Assert.True(result.IsSuccess);
            var linked = result.Value!;

            Assert.Equal(_account, linked.Device.Address.Account);
            Assert.Equal(2, linked.Device.Address.Device.Value);

            // A linked device gets its own credential, not a copy of the voucher's.
            Assert.False(string.IsNullOrWhiteSpace(linked.DeviceCredentials));
            Assert.NotEqual("credential", linked.DeviceCredentials);
        }

        [Fact]
        public async Task LinkDeviceAsync_PublishesTheNewDevicesOwnPreKeys()
        {
            GivenAVouchingDeviceThatAuthenticates();
            var registration = Any.Registration();

            await Create().LinkDeviceAsync(_account, new DeviceId(1), "credential", registration);

            var linkedAddress = Any.AddressOn(_account, 2);

            // The only thing a linked device inherits is the identity key, and that is checked
            // rather than copied. Everything else it publishes for itself.
            await _preKeys.Received(1).UpsertSignedPreKeyAsync(
                linkedAddress, registration.SignedPreKey, Arg.Any<CancellationToken>());
            await _preKeys.Received(1).AddOneTimePreKeysAsync(
                linkedAddress, registration.OneTimePreKeys, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task LinkDeviceAsync_WhenTheAddressIsTaken_IsDeviceAlreadyRegistered()
        {
            GivenAVouchingDeviceThatAuthenticates();
            _devices.InsertAsync(
                Arg.Any<ProtocolAddress>(), Arg.Any<RegisterDeviceModel>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
                .Returns(false);

            var result = await Create().LinkDeviceAsync(_account, new DeviceId(1), "credential", Any.Registration());

            // With the account lock held, a false here is a genuine duplicate rather than a lost
            // race — which is why it maps to 409 and not to a retry.
            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.DeviceAlreadyRegistered, result.Error!.Code);
        }

        // --- GetAsync / ListAsync ------------------------------------------------

        [Fact]
        public async Task GetAsync_ForAnUnknownAddress_IsDeviceNotFound()
        {
            var address = Any.Address();
            _devices.FindAsync(address, Arg.Any<CancellationToken>()).Returns((Device?)null);

            var result = await Create().GetAsync(address);

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.DeviceNotFound, result.Error!.Code);
        }

        [Fact]
        public async Task GetAsync_ReturnsTheDevice()
        {
            var address = Any.Address();
            var device = Any.Device(address);
            _devices.FindAsync(address, Arg.Any<CancellationToken>()).Returns(device);

            var result = await Create().GetAsync(address);

            Assert.True(result.IsSuccess);
            Assert.Same(device, result.Value);
        }

        [Fact]
        public async Task ListAsync_WithNoDevices_IsDeviceNotFoundRatherThanAnEmptyList()
        {
            var account = Any.Account();
            _devices.ListByAccountAsync(account, Arg.Any<CancellationToken>()).Returns(Array.Empty<Device>());

            var result = await Create().ListAsync(account);

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.DeviceNotFound, result.Error!.Code);
        }

        [Fact]
        public async Task ListAsync_ReturnsEveryDeviceOnTheAccount()
        {
            var account = Any.Account();
            var devices = new[]
            {
                Any.Device(Any.AddressOn(account, 1)),
                Any.Device(Any.AddressOn(account, 2))
            };
            _devices.ListByAccountAsync(account, Arg.Any<CancellationToken>()).Returns(devices);

            var result = await Create().ListAsync(account);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.Count());
        }
    }
}
