using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Mapping;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Orchestrators;

namespace SessionSetupMicroService.Controllers
{
    [ApiController]
    [Route("v1/devices")]
    [Produces("application/json")]
    public class DevicesController : ControllerBase
    {
        public const string CredentialHeader = "X-Device-Credential";
        public const string VouchingDeviceHeader = "X-Device-Id";

        private readonly IDeviceRegistrationOrchestrator _orchestrator;

        public DevicesController(IDeviceRegistrationOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        [HttpPost]
        [ProducesResponseType<RegisterDeviceResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterDeviceRequest request,
            CancellationToken ct)
        {
            RegisterDeviceModel? registration = request.ToModel();
            if (registration == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid registration id",
                    Detail = $"Registration ids are 14-bit: 0 to {RegistrationId.MaxValue}.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            Result<DeviceRegistrationResult> result = await _orchestrator.RegisterAsync(registration, ct);
            if (!result.IsSuccess)
            {
                return this.ToActionResult(result.Error!);
            }

            var response = result.Value!.ToResponse();
            return CreatedAtAction(nameof(Get), new
            {
                address = response.Address
            }, response);
        }

        [HttpGet("{address}")]
        [ProducesResponseType<DeviceResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([FromRoute] string address, CancellationToken ct)
        {
            if (!ProtocolAddress.TryParse(address, out var parsed))
            {
                return this.MalformedAddress(address);
            }

            var result = await _orchestrator.GetAsync(parsed, ct);

            return result.IsSuccess
                ? Ok(result.Value!.ToResponse())
                : this.ToActionResult(result.Error!);
        }

        [HttpGet("~/v1/accounts/{account}/devices")]
        [ProducesResponseType<IEnumerable<DeviceResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ListByAccount([FromRoute] string account, CancellationToken ct)
        {
            if (!AccountId.TryParse(account, out var parsed))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Malformed account id",
                    Detail = $"'{account}' is not a valid account identifier.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var result = await _orchestrator.ListAsync(parsed, ct);
            if (result.IsSuccess)
            {
                return Ok(result.Value!.Select(device => device.ToResponse()).ToList());
            }

            return this.ToActionResult(result.Error!);

        }

        [HttpPost("~/v1/accounts/{account}/devices")]
        [ProducesResponseType<RegisterDeviceResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Link(
            [FromRoute] string account,
            [FromBody] RegisterDeviceRequest request,
            [FromHeader(Name = CredentialHeader)] string? credential,
            [FromHeader(Name = VouchingDeviceHeader)] int? vouchingDeviceId,
            CancellationToken ct)
        {
            if (!AccountId.TryParse(account, out var parsedAccount))
            {
                return MalformedAccount(account);
            }

            if (vouchingDeviceId == null || vouchingDeviceId < DeviceId.Primary)
            {

                return BadRequest(new ProblemDetails
                {
                    Title = $"Missing or invalid {VouchingDeviceHeader}",
                    Detail = $"Linking requires the id of an existing device on this account, sent as {VouchingDeviceHeader}.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            RegisterDeviceModel? registration = request.ToModel();
            if (registration == null)
            {
                return InvalidRegistrationId();
            }

            Result<DeviceRegistrationResult> result = await _orchestrator.LinkDeviceAsync(
                parsedAccount,
                new DeviceId(vouchingDeviceId.Value),
                credential,
                registration,
                ct);

            if (!result.IsSuccess)
            {
                return this.ToActionResult(result.Error!);
            }

            var response = result.Value!.ToResponse();
            return CreatedAtAction(nameof(Get), new
            {
                address = response.Address,
            }, response);
        }

        private IActionResult InvalidRegistrationId() =>
            BadRequest(new ProblemDetails
            {
                Title = "Invalid registration id",
                Detail = $"Registration ids are 14-bit: 0 to {RegistrationId.MaxValue}.",
                Status = StatusCodes.Status400BadRequest,
            });

        private IActionResult MalformedAccount(string account) =>
            BadRequest(new ProblemDetails
            {
                Title = "Malformed account id",
                Detail = $"'{account}' is not a valid account identifier.",
                Status = StatusCodes.Status400BadRequest
            });
    }
}
