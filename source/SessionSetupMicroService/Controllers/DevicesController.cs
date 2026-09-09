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
    }
}
