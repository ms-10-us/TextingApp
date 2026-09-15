using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SessionSetupMicroService.Controllers;
using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Options;
using SessionSetupMicroService.Orchestrators;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Controllers
{
    public class PreKeysControllerTests
    {
        private readonly IPreKeyOrchestrator _orchestrator = Substitute.For<IPreKeyOrchestrator>();
        private readonly IDeviceAuthenticator _authenticator = Substitute.For<IDeviceAuthenticator>();
        private readonly PreKeyPolicyOptions _policy = new() { LowWaterMark = 20, MaxPoolSize = 500, MaxKeysPerUpload = 200 };
        private readonly PreKeysController _controller;
        private readonly ProtocolAddress _address = Any.Address();

        public PreKeysControllerTests()
        {
            _controller = new PreKeysController(
                _orchestrator,
                Microsoft.Extensions.Options.Options.Create(_policy),
                _authenticator,
                new RecordingLogger<PreKeysController>())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        private PreKeyBundle Bundle(OneTimePreKey? oneTime = null, bool lastResort = false) => new()
        {
            Address = _address,
            RegistrationId = new RegistrationId(4242),
            IdentityKey = Any.IdentityKey(),
            SignedPreKey = Any.SignedCurvePreKey(),
            OneTimePreKey = oneTime,
            KyberPreKey = Any.LastResortKyberPreKey(),
            ServedLastResortKyberPreKey = lastResort
        };

        // --- TakeBundle ----------------------------------------------------------

        [Fact]
        public async Task TakeBundle_WithAMalformedAddress_Is400()
        {
            var result = await _controller.TakeBundle("nope", CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Malformed protocol address", Assert.IsType<ProblemDetails>(bad.Value).Title);
            await _orchestrator.DidNotReceiveWithAnyArgs().TakeBundleAsync(default);
        }

        [Fact]
        public async Task TakeBundle_OnSuccess_SetsCacheControlNoStore()
        {
            _orchestrator.TakeBundleAsync(_address, Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyBundle>.Success(Bundle(Any.OneTimeCurve(7))));

            await _controller.TakeBundle(_address.ToString(), CancellationToken.None);

            // This header is the whole mitigation for a GET with a side effect. A cached bundle is a
            // reused one-time prekey, which silently weakens the forward secrecy of a conversation.
            Assert.Equal("no-store", _controller.Response.Headers.CacheControl.ToString());
        }

        [Fact]
        public async Task TakeBundle_OnFailure_DoesNotSetCacheControl()
        {
            _orchestrator.TakeBundleAsync(_address, Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyBundle>.Failure(SessionSetupError.DeviceNotFound(_address)));

            var result = await _controller.TakeBundle(_address.ToString(), CancellationToken.None);

            AssertProblem(result, StatusCodes.Status404NotFound);
            Assert.True(StringValuesIsEmpty(_controller.Response.Headers.CacheControl));
        }

        [Fact]
        public async Task TakeBundle_WithAnEmptyCurvePool_SerialisesOneTimePreKeyAsNull()
        {
            _orchestrator.TakeBundleAsync(_address, Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyBundle>.Success(Bundle(oneTime: null)));

            var ok = Assert.IsType<OkObjectResult>(
                await _controller.TakeBundle(_address.ToString(), CancellationToken.None));

            var body = Assert.IsType<PreKeyBundleResponse>(ok.Value);

            // Null is the signal to drop DH4. Program.cs must never gain
            // DefaultIgnoreCondition = WhenWritingNull, or this field disappears from the wire and a
            // client cannot tell "no key available" from "old server".
            Assert.Null(body.OneTimePreKey);
        }

        [Fact]
        public async Task TakeBundle_WhenTheLastResortKeyWasServed_SaysSoOnTheWire()
        {
            _orchestrator.TakeBundleAsync(_address, Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyBundle>.Success(Bundle(Any.OneTimeCurve(1), lastResort: true)));

            var ok = Assert.IsType<OkObjectResult>(
                await _controller.TakeBundle(_address.ToString(), CancellationToken.None));

            var body = Assert.IsType<PreKeyBundleResponse>(ok.Value);

            // The reusable last-resort key is weaker than a fresh one. If this flag is misspelled or
            // dropped, a client silently believes it got the stronger key.
            Assert.True(body.ServedLastResortKyberPreKey);
        }

        // --- GetInventory --------------------------------------------------------

        [Fact]
        public async Task GetInventory_ReportsTheCountsAndThePolicyThatJudgesThem()
        {
            _orchestrator.GetInventoryAsync(_address, Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyInventory>.Success(new PreKeyInventory { Curve = 5, Kyber = 40 }));

            var ok = Assert.IsType<OkObjectResult>(
                await _controller.GetInventory(_address.ToString(), CancellationToken.None));

            var body = Assert.IsType<PreKeyInventoryResponse>(ok.Value);
            Assert.Equal(5, body.OneTimePreKeys);
            Assert.Equal(40, body.OneTimeKyberPreKeys);
            Assert.Equal(_policy.LowWaterMark, body.LowWaterMark);
            Assert.Equal(_policy.MaxPoolSize, body.MaxPoolSize);

            // Either pool below the mark means top up: a dry curve pool costs DH4 on its own.
            Assert.True(body.NeedsReplenishment);
        }

        [Fact]
        public async Task GetInventory_ForAnUnknownDevice_Is404()
        {
            _orchestrator.GetInventoryAsync(_address, Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyInventory>.Failure(SessionSetupError.DeviceNotFound(_address)));

            AssertProblem(
                await _controller.GetInventory(_address.ToString(), CancellationToken.None),
                StatusCodes.Status404NotFound);
        }

        // --- Publish -------------------------------------------------------------

        [Fact]
        public async Task Publish_WithoutACredential_Is401AndNeverReachesTheOrchestrator()
        {
            _authenticator.AuthenticateAsync(_address, null, Arg.Any<CancellationToken>())
                .Returns(Result<ProtocolAddress>.Failure(SessionSetupError.Unauthorized()));

            var result = await _controller.Publish(_address.ToString(), new PublishPreKeysDto(), null, CancellationToken.None);

            AssertProblem(result, StatusCodes.Status401Unauthorized);

            // An open publish endpoint lets anyone replace a device's prekeys with keys they
            // control: a complete break of the handshake with no symptom the victim can observe.
            await _orchestrator.DidNotReceiveWithAnyArgs().PublishAsync(default, default!);
        }

        [Fact]
        public async Task Publish_WhenAuthenticated_ForwardsTheMappedPublication()
        {
            GivenAuthenticated();
            _orchestrator.PublishAsync(_address, Arg.Any<PublishPreKeys>(), Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyInventory>.Success(new PreKeyInventory { Curve = 21, Kyber = 21 }));

            var dto = new PublishPreKeysDto
            {
                OneTimePreKeys = new List<OneTimePreKeyDto>
                {
                    new() { KeyId = 1, PublicKey = Any.Dto(Any.CurveKey()) }
                }
            };

            var ok = Assert.IsType<OkObjectResult>(
                await _controller.Publish(_address.ToString(), dto, "credential", CancellationToken.None));

            var body = Assert.IsType<PreKeyInventoryResponse>(ok.Value);
            Assert.Equal(21, body.OneTimePreKeys);
            Assert.False(body.NeedsReplenishment);

            await _orchestrator.Received(1).PublishAsync(
                _address,
                Arg.Is<PublishPreKeys>(publication => publication.OneTimePreKeys.Count() == 1),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Publish_WithOnlyARotatedSignedPreKey_DoesNotThrowOnTheOmittedPools()
        {
            GivenAuthenticated();
            _orchestrator.PublishAsync(_address, Arg.Any<PublishPreKeys>(), Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyInventory>.Success(PreKeyInventory.Empty));

            var dto = new PublishPreKeysDto
            {
                SignedPreKey = new SignedPreKeyDto
                {
                    KeyId = 9,
                    PublicKey = Any.Dto(Any.CurveKey()),
                    Signature = Any.Bytes(Any.SignatureBytes)
                }
                // OneTimePreKeys and OneTimeKyberPreKeys deliberately omitted — every field on this
                // DTO is optional, and a rotation that sends only the signed prekey is normal.
            };

            var result = await _controller.Publish(_address.ToString(), dto, "credential", CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Publish_WithAMalformedAddress_Is400AndDoesNotAuthenticate()
        {
            var result = await _controller.Publish("nope", new PublishPreKeysDto(), "credential", CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            await _authenticator.DidNotReceiveWithAnyArgs().AuthenticateAsync(default, default);
        }

        [Fact]
        public async Task Publish_WhenThePoolLimitIsExceeded_Is409()
        {
            GivenAuthenticated();
            _orchestrator.PublishAsync(_address, Arg.Any<PublishPreKeys>(), Arg.Any<CancellationToken>())
                .Returns(Result<PreKeyInventory>.Failure(SessionSetupError.PoolLimitExceeded(_policy.MaxPoolSize)));

            var result = await _controller.Publish(
                _address.ToString(), new PublishPreKeysDto(), "credential", CancellationToken.None);

            AssertProblem(result, StatusCodes.Status409Conflict);
        }

        private void GivenAuthenticated() =>
            _authenticator.AuthenticateAsync(_address, Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Result<ProtocolAddress>.Success(_address));

        private static bool StringValuesIsEmpty(Microsoft.Extensions.Primitives.StringValues values) =>
            values.Count == 0;

        private static ProblemDetails AssertProblem(IActionResult result, int expectedStatus)
        {
            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(expectedStatus, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(expectedStatus, problem.Status);
            return problem;
        }
    }
}
