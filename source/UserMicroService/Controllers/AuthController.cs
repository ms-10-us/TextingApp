using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserMicroService.Dtos;
using UserMicroService.Mappings;
using UserMicroService.Models;
using UserMicroService.Orchestrators;

namespace UserMicroService.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthOrchestrator _authOrchestrator;

        public AuthController(IAuthOrchestrator authOrchestrator)
        {
            _authOrchestrator = authOrchestrator;
        }

        /// <summary>
        /// Exchanges credentials for a bearer token.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginDtoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Login([FromBody] LoginDto request, CancellationToken cancellationToken)
        {
            LoginModel model = request.ToModel();

            OperationResult<LoginResultModel> result = await _authOrchestrator.LoginAsync(model, cancellationToken);
            if (!result.Succeeded)
            {
                return ToErrorResponse(result);
            }

            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            return Ok(result.Value!.ToDto());
        }

        private IActionResult ToErrorResponse<T>(OperationResult<T> result)
        {
            string detail = string.Join(" ", result.Errors.Select(e => e.Description));

            Dictionary<string, string[]> errorsByCode = result.Errors
                .GroupBy(e => e.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray(), StringComparer.Ordinal);

            return result.Status switch
            {
                OperationStatus.Unauthorized => StatusCode(StatusCodes.Status401Unauthorized, new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = detail,
                    Status = StatusCodes.Status401Unauthorized
                }),
                OperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = detail,
                    Status = StatusCodes.Status403Forbidden
                }),
                OperationStatus.NotFound => NotFound(new ProblemDetails
                {
                    Title = "Not found",
                    Detail = detail,
                    Status = StatusCodes.Status404NotFound
                }),
                OperationStatus.Conflict => Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = detail,
                    Status = StatusCodes.Status409Conflict
                }),
                OperationStatus.ValidationFailed => BadRequest(new ValidationProblemDetails(errorsByCode)
                {
                    Title = "Validation failed",
                    Status = StatusCodes.Status400BadRequest
                }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Unexpected error",
                    Status = StatusCodes.Status500InternalServerError
                })
            };
        }
    }
}
