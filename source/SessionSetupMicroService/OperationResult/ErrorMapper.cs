using Microsoft.AspNetCore.Mvc;
using SessionSetupMicroService.Enums;

namespace SessionSetupMicroService.OperationResult
{
    public static class ErrorMapper
    {
        public static IActionResult ToActionResult(this ControllerBase controller, SessionSetupError error)
        {
            var status = StatusFor(error.Code);

            var problem = new ProblemDetails
            {
                Title = TitleFor(error.Code),
                Detail = error.Message,
                Status = status,
                Type = $"https://sessionsetup.invalid/errors/{error.Code.ToString().ToLowerInvariant()}",
            };

            return controller.StatusCode(status, problem);
        }

        public static IActionResult MalformedAddress(this ControllerBase controller, string value) =>
            controller.BadRequest(new ProblemDetails
            {
                Title = "Malformed protocol address",
                Detail = $"'{value}' is not a valid {{account}}.{{device}} address.",
                Status = StatusCodes.Status400BadRequest
            });















        private static int StatusFor(SessionSetupErrorCode code) => code switch
        {
            SessionSetupErrorCode.DeviceNotFound => StatusCodes.Status404NotFound,
            SessionSetupErrorCode.DeviceAlreadyRegistered => StatusCodes.Status409Conflict,
            SessionSetupErrorCode.PreKeyPoolLimitExceeded => StatusCodes.Status409Conflict,
            SessionSetupErrorCode.InvalidKeyMaterial => StatusCodes.Status400BadRequest,
            SessionSetupErrorCode.NothingToPublish => StatusCodes.Status400BadRequest,
            SessionSetupErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

        private static string TitleFor(SessionSetupErrorCode code) => code switch
        {
            SessionSetupErrorCode.DeviceNotFound => "Device not found",
            SessionSetupErrorCode.DeviceAlreadyRegistered => "Device already registered",
            SessionSetupErrorCode.PreKeyPoolLimitExceeded => "Prekey pool limit exceeded",
            SessionSetupErrorCode.InvalidKeyMaterial => "Invalid key material",
            SessionSetupErrorCode.NothingToPublish => "Nothing to publish",
            SessionSetupErrorCode.Unauthorized => "Unauthorized",
            _ => "Session setup failed",
        };
    }
}
