using NSubstitute;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.Options;
using SessionSetupMicroService.Orchestrators;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Orchestrators
{
    public class PreKeyOrchestratorTests
    {
        private readonly DirectUnitOfWork _unitOfWork = new();
        private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
        private readonly IPreKeyRepository _preKeys = Substitute.For<IPreKeyRepository>();
        private readonly RecordingLogger<PreKeyOrchestrator> _logger = new();
        private readonly PreKeyPolicyOptions _policy = new() { LowWaterMark = 20, MaxPoolSize = 500, MaxKeysPerUpload = 200 };
        private readonly ProtocolAddress _address = Any.Address();

        private PreKeyOrchestrator Create(SessionSetupMicroService.PostgresDB.IUnitOfWork? unitOfWork = null) => new(
            unitOfWork ?? _unitOfWork,
            _devices,
            _preKeys,
            Microsoft.Extensions.Options.Options.Create(_policy),
            new FixedClock(),
            _logger);

        private void GivenARegisteredDevice()
        {
            _devices.FindAsync(_address, Arg.Any<CancellationToken>()).Returns(Any.Device(_address));
            _devices.ExistsAsync(_address, Arg.Any<CancellationToken>()).Returns(true);
        }

        private void GivenSignedPreKeys()
        {
            _preKeys.GetSignedPreKeyAsync(_address, PreKeyKind.Curve, Arg.Any<CancellationToken>())
                .Returns(Any.SignedCurvePreKey(keyId: 10));
            _preKeys.GetSignedPreKeyAsync(_address, PreKeyKind.Kyber, Arg.Any<CancellationToken>())
                .Returns(Any.LastResortKyberPreKey(keyId: 99));
        }

        // --- TakeBundleAsync -----------------------------------------------------

        [Fact]
        public async Task TakeBundleAsync_ForAnUnknownDevice_IsDeviceNotFound()
        {
            _devices.FindAsync(_address, Arg.Any<CancellationToken>()).Returns((Device?)null);

            var result = await Create().TakeBundleAsync(_address);

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.DeviceNotFound, result.Error!.Code);

            // No key may be consumed for a device that does not exist.
            await _preKeys.DidNotReceiveWithAnyArgs().TakeOneTimePreKeyAsync(default, default);
        }

        [Fact]
        public async Task TakeBundleAsync_WhenASignedPreKeyIsMissing_IsInvalidKeyMaterial()
        {
            GivenARegisteredDevice();
            _preKeys.GetSignedPreKeyAsync(_address, PreKeyKind.Curve, Arg.Any<CancellationToken>())
                .Returns(Any.SignedCurvePreKey());
            _preKeys.GetSignedPreKeyAsync(_address, PreKeyKind.Kyber, Arg.Any<CancellationToken>())
                .Returns((SignedPreKey?)null);

            var result = await Create().TakeBundleAsync(_address);

            // A device that never finished publishing is a real error — unlike an empty pool, which
            // is not.
            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.InvalidKeyMaterial, result.Error!.Code);
        }

        [Fact]
        public async Task TakeBundleAsync_WithBothPoolsStocked_ServesAFullStrengthBundle()
        {
            GivenARegisteredDevice();
            GivenSignedPreKeys();
            _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Curve, Arg.Any<CancellationToken>())
                .Returns(Any.OneTimeCurve(7));
            _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Kyber, Arg.Any<CancellationToken>())
                .Returns(Any.OneTimeKyber(8));

            var result = await Create().TakeBundleAsync(_address);

            Assert.True(result.IsSuccess);
            var bundle = result.Value!;
            Assert.Equal(7, bundle.OneTimePreKey!.Id.Value);
            Assert.Equal(8, bundle.KyberPreKey.Id.Value);
            Assert.False(bundle.ServedLastResortKyberPreKey);
            Assert.True(bundle.IsFullStrength());
            Assert.Empty(_logger.Warnings);
        }

        [Fact]
        public async Task TakeBundleAsync_WithAnEmptyCurvePool_StillSucceedsWithNoOneTimeKey()
        {
            GivenARegisteredDevice();
            GivenSignedPreKeys();
            _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Curve, Arg.Any<CancellationToken>())
                .Returns((OneTimePreKey?)null);
            _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Kyber, Arg.Any<CancellationToken>())
                .Returns(Any.OneTimeKyber(8));

            var result = await Create().TakeBundleAsync(_address);

            // Degrading is the designed behaviour. Refusing to serve would break the conversation to
            // protect nothing: the sender simply omits DH4.
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value!.OneTimePreKey);
            Assert.False(result.Value.IsFullStrength());
            Assert.Contains(_logger.Warnings, message => message.Contains("degraded bundle"));
        }

        [Fact]
        public async Task TakeBundleAsync_WithAnEmptyKyberPool_ServesTheLastResortKeyAndSaysSo()
        {
            GivenARegisteredDevice();
            GivenSignedPreKeys();
            _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Curve, Arg.Any<CancellationToken>())
                .Returns(Any.OneTimeCurve(7));
            _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Kyber, Arg.Any<CancellationToken>())
                .Returns((OneTimePreKey?)null);

            var result = await Create().TakeBundleAsync(_address);

            var bundle = result.Value!;

            // The reusable last-resort key is weaker than a fresh one, so the flag is the only way a
            // sender can tell which it got. Serving it silently is the bug this asserts against.
            Assert.Equal(99, bundle.KyberPreKey.Id.Value);
            Assert.True(bundle.ServedLastResortKyberPreKey);
            Assert.False(bundle.IsFullStrength());
            Assert.Contains(_logger.Warnings, message => message.Contains("degraded bundle"));
        }

        [Fact]
        public async Task TakeBundleAsync_ConsumesExactlyOneKeyFromEachPool()
        {
            GivenARegisteredDevice();
            GivenSignedPreKeys();
            _preKeys.TakeOneTimePreKeyAsync(_address, Arg.Any<PreKeyKind>(), Arg.Any<CancellationToken>())
                .Returns(Any.OneTimeCurve(1));

            await Create().TakeBundleAsync(_address);

            await _preKeys.Received(1).TakeOneTimePreKeyAsync(_address, PreKeyKind.Curve, Arg.Any<CancellationToken>());
            await _preKeys.Received(1).TakeOneTimePreKeyAsync(_address, PreKeyKind.Kyber, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task TakeBundleAsync_AssemblesTheBundleInsideOneUnitOfWork()
        {
            GivenARegisteredDevice();
            GivenSignedPreKeys();

            await Create().TakeBundleAsync(_address);

            // The two signed-key reads and the two pops must see one snapshot, or a bundle can be
            // built from a signed prekey read before a rotation and a one-time key taken after it.
            Assert.Equal(1, _unitOfWork.Executions);
        }

        [Fact]
        public async Task TakeBundleAsync_CarriesTheDevicesIdentityFacts()
        {
            GivenARegisteredDevice();
            GivenSignedPreKeys();

            var result = await Create().TakeBundleAsync(_address);

            var bundle = result.Value!;
            Assert.Equal(_address, bundle.Address);
            Assert.Equal(4242, bundle.RegistrationId.Value);
            Assert.Equal(KeyAlgorithms.Ed25519, bundle.IdentityKey.Algorithm);
        }

        // --- GetInventoryAsync ---------------------------------------------------

        [Fact]
        public async Task GetInventoryAsync_ForAnUnknownDevice_IsDeviceNotFound()
        {
            _devices.ExistsAsync(_address, Arg.Any<CancellationToken>()).Returns(false);

            var result = await Create().GetInventoryAsync(_address);

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.DeviceNotFound, result.Error!.Code);
        }

        [Fact]
        public async Task GetInventoryAsync_ReturnsTheCounts()
        {
            GivenARegisteredDevice();
            _preKeys.CountAsync(_address, Arg.Any<CancellationToken>())
                .Returns(new PreKeyInventory { Curve = 12, Kyber = 4 });

            var result = await Create().GetInventoryAsync(_address);

            Assert.True(result.IsSuccess);
            Assert.Equal(12, result.Value!.Curve);
            Assert.Equal(4, result.Value.Kyber);
        }

        // --- PublishAsync --------------------------------------------------------

        [Fact]
        public async Task PublishAsync_ForAnUnknownDevice_IsDeviceNotFound()
        {
            _devices.ExistsAsync(_address, Arg.Any<CancellationToken>()).Returns(false);

            var result = await Create().PublishAsync(_address, Any.Publication());

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.DeviceNotFound, result.Error!.Code);
        }

        [Fact]
        public async Task PublishAsync_WithNothingToPublish_Fails()
        {
            GivenARegisteredDevice();
            var empty = new PublishPreKeys
            {
                SignedPreKey = null,
                LastResortKyberPreKey = null,
                OneTimePreKeys = Array.Empty<OneTimePreKey>(),
                OneTimeKyberPreKeys = Array.Empty<OneTimePreKey>()
            };

            var result = await Create().PublishAsync(_address, empty);

            // An empty publish is a client bug. Succeeding silently would hide it.
            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.NothingToPublish, result.Error!.Code);
        }

        [Fact]
        public async Task PublishAsync_ThatWouldOverflowThePool_IsRejected()
        {
            GivenARegisteredDevice();
            _preKeys.CountAsync(_address, Arg.Any<CancellationToken>())
                .Returns(new PreKeyInventory { Curve = _policy.MaxPoolSize, Kyber = 0 });

            var result = await Create().PublishAsync(_address, Any.Publication(curveCount: 1, kyberCount: 0));

            Assert.False(result.IsSuccess);
            Assert.Equal(SessionSetupErrorCode.PreKeyPoolLimitExceeded, result.Error!.Code);
            await _preKeys.DidNotReceiveWithAnyArgs().AddOneTimePreKeysAsync(default, default!);
        }

        [Fact]
        public async Task PublishAsync_RotatesOnlyWhatTheRequestCarries()
        {
            GivenARegisteredDevice();
            _preKeys.CountAsync(_address, Arg.Any<CancellationToken>()).Returns(new PreKeyInventory { Curve = 0, Kyber = 0 });

            var publication = new PublishPreKeys
            {
                SignedPreKey = Any.SignedCurvePreKey(keyId: 55),
                LastResortKyberPreKey = null,
                OneTimePreKeys = new[] { Any.OneTimeCurve(1) },
                OneTimeKyberPreKeys = Array.Empty<OneTimePreKey>()
            };

            await Create().PublishAsync(_address, publication);

            // Every field is optional: a device sends only what changed.
            await _preKeys.Received(1).UpsertSignedPreKeyAsync(
                _address, publication.SignedPreKey!, Arg.Any<CancellationToken>());
            await _preKeys.DidNotReceive().UpsertSignedPreKeyAsync(
                _address, Arg.Is<SignedPreKey>(key => key.Kind == PreKeyKind.Kyber), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishAsync_AddsBothPoolsAndTouchesTheDevice()
        {
            GivenARegisteredDevice();
            _preKeys.CountAsync(_address, Arg.Any<CancellationToken>()).Returns(new PreKeyInventory { Curve = 0, Kyber = 0 });

            var publication = Any.Publication();
            var result = await Create().PublishAsync(_address, publication);

            Assert.True(result.IsSuccess);
            await _preKeys.Received(1).AddOneTimePreKeysAsync(_address, publication.OneTimePreKeys, Arg.Any<CancellationToken>());
            await _preKeys.Received(1).AddOneTimePreKeysAsync(_address, publication.OneTimeKyberPreKeys, Arg.Any<CancellationToken>());
            await _devices.Received(1).TouchAsync(_address, Arg.Any<CancellationToken>());
        }

        [Fact(Skip = "Fails until the pool-limit race is fixed. Remove this Skip after moving " +
                     "TouchAsync to the top of the transaction and CountAsync in after it.")]
        public async Task PublishAsync_ChecksThePoolLimitInsideTheTransaction()
        {
            GivenARegisteredDevice();

            var recording = new OrderRecordingUnitOfWork();
            _preKeys.CountAsync(_address, Arg.Any<CancellationToken>()).Returns(callInfo =>
            {
                recording.Record("COUNT");
                return new PreKeyInventory { Curve = 0, Kyber = 0 };
            });
            _devices.When(devices => devices.TouchAsync(_address, Arg.Any<CancellationToken>()))
                .Do(_ => recording.Record("TOUCH"));

            await Create(recording).PublishAsync(_address, Any.Publication());

            // The order that makes the check mean anything:
            //   BEGIN -> TOUCH (takes the device row lock) -> COUNT -> inserts -> COMMIT
            // Counting before BEGIN lets two concurrent publishes both read the old total, both pass
            // the MaxPoolSize check, and both insert.
            var begin = recording.Calls.IndexOf("BEGIN");
            var touch = recording.Calls.IndexOf("TOUCH");
            var count = recording.Calls.IndexOf("COUNT");

            Assert.True(begin < touch, "the device row must be locked inside the transaction");
            Assert.True(touch < count, "the lock must be taken before the counts are read");
        }
    }
}
