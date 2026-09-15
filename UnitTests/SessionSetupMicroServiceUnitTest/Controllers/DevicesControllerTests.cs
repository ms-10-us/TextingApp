using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SessionSetupMicroService.Controllers;
using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Orchestrators;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Controllers
{
    public class DevicesControllerTests
    {
        private readonly IDeviceRegistrationOrchestrator _orchestrator = Substitute.For<IDeviceRegistrationOrchestrator>();
        private readonly DevicesController _controller;

        public DevicesControllerTests()
        {
            _controller = new DevicesController(_orchestrator)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        // --- Register ------------------------------------------------------------

        [Fact]
        public async Task Register_WithARegistrationIdOutsideFourteenBits_Is400AndNeverReachesTheOrchestrator()
        {
            var request = Any.Request(registrationId: RegistrationId.MaxValue + 1);

            var result = await _controller.Register(request, CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(bad.Value);
            Assert.Equal("Invalid registration id", problem.Title);

            await _orchestrator.DidNotReceiveWithAnyArgs().RegisterAsync(default!);
        }

        [Fact]
        public async Task Register_OnSuccess_Is201WithTheCredential()
        {
            var address = Any.Address();
            var device = Any.Device(address);
            _orchestrator.RegisterAsync(Arg.Any<RegisterDeviceModel>(), Arg.Any<CancellationToken>())
                .Returns(Result<DeviceRegistrationResult>.Success(new DeviceRegistrationResult(device, "the-credential")));

            var result = await _controller.Register(Any.Request(), CancellationToken.None);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RegisterDeviceResponse>(created.Value);

            Assert.Equal(address.ToString(), body.Address);
            Assert.Equal(address.Account.Value, body.AccountId);
            Assert.Equal(1, body.DeviceId);

            // If this ever comes back empty, the caller has lost the only copy of the credential
            // that will ever exist — the service stores nothing but its hash.
            Assert.Equal("the-credential", body.DeviceCredential);
        }

        [Fact]
        public async Task Register_PointsTheLocationHeaderAtTheDeviceItCreated()
        {
            var address = Any.Address();
            _orchestrator.RegisterAsync(Arg.Any<RegisterDeviceModel>(), Arg.Any<CancellationToken>())
                .Returns(Result<DeviceRegistrationResult>.Success(
                    new DeviceRegistrationResult(Any.Device(address), "credential")));

            var created = Assert.IsType<CreatedAtActionResult>(
                await _controller.Register(Any.Request(), CancellationToken.None));

            Assert.Equal(nameof(DevicesController.Get), created.ActionName);
            Assert.Equal(address.ToString(), created.RouteValues!["address"]);
        }

        [Fact]
        public async Task Register_WhenTheOrchestratorRejectsTheKeys_Is400()
        {
            _orchestrator.RegisterAsync(Arg.Any<RegisterDeviceModel>(), Arg.Any<CancellationToken>())
                .Returns(Result<DeviceRegistrationResult>.Failure(
                    SessionSetupError.InvalidKeyMaterial("A x25519 public key must be 32 bytes, got 31.")));

            var result = await _controller.Register(Any.Request(), CancellationToken.None);

            var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
            Assert.Equal("Invalid key material", problem.Title);
        }

        // --- Link ----------------------------------------------------------------

        [Fact]
        public async Task Link_WithAMalformedAccount_Is400()
        {
            var result = await _controller.Link("nonsense", Any.Request(), "credential", 1, CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Malformed account id", Assert.IsType<ProblemDetails>(bad.Value).Title);
            await _orchestrator.DidNotReceiveWithAnyArgs().LinkDeviceAsync(default, default, default, default!);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Link_WithoutAUsableVouchingDeviceId_Is400(int? vouchingDeviceId)
        {
            var account = Any.Account();

            var result = await _controller.Link(
                account.ToString(), Any.Request(), "credential", vouchingDeviceId, CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains(DevicesController.VouchingDeviceHeader, Assert.IsType<ProblemDetails>(bad.Value).Title!);

            // Guarded before the DeviceId constructor sees it — device ids start at 1, and letting a
            // 0 through would throw ArgumentOutOfRange and surface as a 500.
            await _orchestrator.DidNotReceiveWithAnyArgs().LinkDeviceAsync(default, default, default, default!);
        }

        [Fact]
        public async Task Link_WithoutACredential_ForwardsTheNullAndLetsTheOrchestratorRefuse()
        {
            var account = Any.Account();
            _orchestrator.LinkDeviceAsync(
                    account, Arg.Any<DeviceId>(), null, Arg.Any<RegisterDeviceModel>(), Arg.Any<CancellationToken>())
                .Returns(Result<DeviceRegistrationResult>.Failure(SessionSetupError.Unauthorized()));

            var result = await _controller.Link(account.ToString(), Any.Request(), null, 1, CancellationToken.None);

            // The controller does not second-guess the credential: authentication is one decision in
            // one place, and it belongs below the HTTP edge.
            AssertProblem(result, StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task Link_OnSuccess_Is201WithTheNewDevicesOwnCredential()
        {
            var account = Any.Account();
            var linked = Any.AddressOn(account, 2);

            _orchestrator.LinkDeviceAsync(
                    account, Arg.Any<DeviceId>(), "vouching-credential", Arg.Any<RegisterDeviceModel>(), Arg.Any<CancellationToken>())
                .Returns(Result<DeviceRegistrationResult>.Success(
                    new DeviceRegistrationResult(Any.Device(linked), "the-new-credential")));

            var result = await _controller.Link(
                account.ToString(), Any.Request(), "vouching-credential", 1, CancellationToken.None);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RegisterDeviceResponse>(created.Value);

            Assert.Equal(2, body.DeviceId);
            Assert.Equal(linked.ToString(), body.Address);

            // Not the voucher's credential. The linked device gets its own, returned exactly once.
            Assert.Equal("the-new-credential", body.DeviceCredential);
        }

        [Fact]
        public async Task Link_PassesTheVouchingDeviceIdThrough()
        {
            var account = Any.Account();
            _orchestrator.LinkDeviceAsync(
                    Arg.Any<AccountId>(), Arg.Any<DeviceId>(), Arg.Any<string?>(), Arg.Any<RegisterDeviceModel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Result<DeviceRegistrationResult>.Success(
                    new DeviceRegistrationResult(Any.Device(Any.AddressOn(account, 3)), "credential")));

            await _controller.Link(account.ToString(), Any.Request(), "credential", 2, CancellationToken.None);

            await _orchestrator.Received(1).LinkDeviceAsync(
                account,
                Arg.Is<DeviceId>(id => id.Value == 2),
                "credential",
                Arg.Any<RegisterDeviceModel>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Link_WhenTheAddressIsTaken_Is409()
        {
            var account = Any.Account();
            _orchestrator.LinkDeviceAsync(
                    Arg.Any<AccountId>(), Arg.Any<DeviceId>(), Arg.Any<string?>(), Arg.Any<RegisterDeviceModel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Result<DeviceRegistrationResult>.Failure(
                    SessionSetupError.DeviceAlreadyRegistered(Any.AddressOn(account, 2))));

            var result = await _controller.Link(account.ToString(), Any.Request(), "credential", 1, CancellationToken.None);

            AssertProblem(result, StatusCodes.Status409Conflict);
        }

        [Fact]
        public async Task Link_WithARegistrationIdOutsideFourteenBits_Is400()
        {
            var account = Any.Account();

            var result = await _controller.Link(
                account.ToString(), Any.Request(registrationId: RegistrationId.MaxValue + 1), "credential", 1,
                CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid registration id", Assert.IsType<ProblemDetails>(bad.Value).Title);
        }

        // --- Get -----------------------------------------------------------------

        [Theory]
        [InlineData("not-an-address")]
        [InlineData("")]
        [InlineData("deadbeef")]
        public async Task Get_WithAMalformedAddress_Is400(string address)
        {
            var result = await _controller.Get(address, CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(bad.Value);
            Assert.Equal("Malformed protocol address", problem.Title);
        }

        [Fact]
        public async Task Get_ForAnUnknownDevice_Is404()
        {
            var address = Any.Address();
            _orchestrator.GetAsync(address, Arg.Any<CancellationToken>())
                .Returns(Result<Device>.Failure(SessionSetupError.DeviceNotFound(address)));

            var result = await _controller.Get(address.ToString(), CancellationToken.None);

            AssertProblem(result, StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task Get_ReturnsThePublicFactsAndNoCredentialHash()
        {
            var address = Any.Address();
            _orchestrator.GetAsync(address, Arg.Any<CancellationToken>())
                .Returns(Result<Device>.Success(Any.Device(address)));

            var ok = Assert.IsType<OkObjectResult>(await _controller.Get(address.ToString(), CancellationToken.None));
            var body = Assert.IsType<DeviceResponse>(ok.Value);

            Assert.Equal(address.ToString(), body.Address);
            Assert.Equal(4242, body.RegistrationId);
            Assert.Equal(KeyAlgorithms.Ed25519, body.IdentityDto.Algorithm);
        }

        // --- ListByAccount -------------------------------------------------------

        [Fact]
        public async Task ListByAccount_WithAMalformedAccount_Is400()
        {
            var result = await _controller.ListByAccount("nonsense", CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Malformed account id", Assert.IsType<ProblemDetails>(bad.Value).Title);
        }

        [Fact]
        public async Task ListByAccount_WithNoDevices_Is404()
        {
            var account = Any.Account();
            _orchestrator.ListAsync(account, Arg.Any<CancellationToken>())
                .Returns(Result<IEnumerable<Device>>.Failure(SessionSetupError.DeviceNotFound(account)));

            var result = await _controller.ListByAccount(account.ToString(), CancellationToken.None);

            AssertProblem(result, StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task ListByAccount_ReturnsOneResponsePerDevice()
        {
            var account = Any.Account();
            _orchestrator.ListAsync(account, Arg.Any<CancellationToken>())
                .Returns(Result<IEnumerable<Device>>.Success(new[]
                {
                    Any.Device(Any.AddressOn(account, 1)),
                    Any.Device(Any.AddressOn(account, 2))
                }));

            var ok = Assert.IsType<OkObjectResult>(
                await _controller.ListByAccount(account.ToString(), CancellationToken.None));

            var body = Assert.IsAssignableFrom<IEnumerable<DeviceResponse>>(ok.Value);
            Assert.Equal(new[] { 1, 2 }, body.Select(device => device.DeviceId));
        }

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
