using Microsoft.AspNetCore.Mvc;
using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Orchestrators;
using SessionSetupMicroService.Mapping;
using Microsoft.Extensions.Options;
using SessionSetupMicroService.Options;

namespace SessionSetupMicroService.Controllers
{
    [ApiController]
    [Route("v1/PreKeys")]
    [Produces("application/json")]
    public class PreKeysController : Controller
    {
        private readonly IPreKeyOrchestrator _preKeyOrchestrator;
        private readonly IOptions<PreKeyPolicyOptions> _policy;
        private readonly IDeviceAuthenticator _deviceAuthenticator;
        private readonly ILogger<PreKeysController> _logger;

        private readonly PreKeyPolicyOptions _policyValue;
        public const string CredentialHeader = "X-Device-Credential";

        public PreKeysController(
            IPreKeyOrchestrator preKeyOrchestrator,
            IOptions<PreKeyPolicyOptions> policy,
            IDeviceAuthenticator deviceAuthenticator,
            ILogger<PreKeysController> logger)
        {
            _preKeyOrchestrator = preKeyOrchestrator;
            _policy = policy;
            _policyValue = _policy.Value;
            _deviceAuthenticator = deviceAuthenticator;
            _logger = logger;
        }

        [HttpGet("{address}")]
        [ProducesResponseType<PreKeyBundleResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TakeBundle([FromRoute] string address, CancellationToken ct)
        {
            if (!ProtocolAddress.TryParse(address, out ProtocolAddress parsedAddress))
            {
                return this.MalformedAddress(address);
            }

            Result<PreKeyBundle> result = await _preKeyOrchestrator.TakeBundleAsync(parsedAddress, ct);

            if (result.IsSuccess)
            {
                Response.Headers.CacheControl = "no-store";
                return Ok(result.Value!.ToResponse());
            }
            else
            {
                return this.ToActionResult(result.Error!);
            }
        }

        [HttpGet("{address}/inventory")]
        [ProducesResponseType<PreKeyInventoryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInventory([FromRoute] string address, CancellationToken ct)
        {
            if (!ProtocolAddress.TryParse(address, out var parsed))
            {
                return this.MalformedAddress(address);
            }

            var result = await _preKeyOrchestrator.GetInventoryAsync(parsed, ct);

            if (result.IsSuccess)
            {
                return Ok(result.Value!.ToResponse(_policyValue.LowWaterMark, _policyValue.MaxPoolSize));
            }
            else
            {
                return this.ToActionResult(result.Error!);
            }
        }

        [HttpPut("{address}")]
        [ProducesResponseType<PreKeyInventoryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Publish(
            [FromRoute] string address,
            [FromBody] PublishPreKeysDto request,
            [FromHeader(Name = CredentialHeader)] string? creadential,
            CancellationToken ct)
        {
            if (!ProtocolAddress.TryParse(address, out var parsed))
            {
                return this.MalformedAddress(address);
            }

            var authentication = await _deviceAuthenticator.AuthenticateAsync(parsed, creadential, ct);
            if (!authentication.IsSuccess)
            {
                return this.ToActionResult(authentication.Error!);
            }

            var result = await _preKeyOrchestrator.PublishAsync(parsed, request.ToModel(), ct);
            return result.IsSuccess
            ? Ok(result.Value!.ToResponse(_policyValue.LowWaterMark, _policyValue.MaxPoolSize))
            : this.ToActionResult(result.Error!);
        }
    }
}
