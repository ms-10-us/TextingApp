using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ClientModel.Primitives;
using UserMicroService.Dtos;
using UserMicroService.Mappings;
using UserMicroService.Models;
using UserMicroService.Orchestrators;

namespace UserMicroService.Controllers
{
    [ApiController]
    [Route("api/user-profiles")]
    [Produces("application/json")]
    public class UserController : ControllerBase
    {
        private readonly IUserOrchestrator _userOrchestrator;

        public UserController(IUserOrchestrator userOrchestrator)
        {
            _userOrchestrator = userOrchestrator;
        }

        [HttpPost("AddUser")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AddUserDtoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddUserProfile([FromBody] AddUserDto request, CancellationToken cancellationToken)
        {
            AddUserModel model = request.ToModel();
            try
            {
                OperationResult<UserModel> result = await _userOrchestrator.AddUserAsync(model, cancellationToken);

                if (!result.Succeeded)
                {
                    return ToErrorResponse(result);
                }

                UserDto dtoResponse = result.Value!.ToDto();
                return CreatedAtAction(
                    nameof(GetUserProfile),
                    new { userId = dtoResponse.Id },
                    dtoResponse);
            }
            catch (NullReferenceException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }   
        }

        [HttpGet("{userId:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserProfile(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                OperationResult<UserModel> result = await _userOrchestrator.GetUserAsync(userId, cancellationToken);

                if (!result.Succeeded)
                {
                    return ToErrorResponse(result);
                }

                return Ok(result.Value!.ToDto());
            }
            catch (NullReferenceException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private IActionResult ToErrorResponse<T>(OperationResult<T> result)
        {
            string detail = string.Join(" ", result.Errors.Select(e => e.Description));

            Dictionary<string, string[]> errorsByCode = result.Errors
                .GroupBy(e => e.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray(), StringComparer.Ordinal);

            return result.Status switch
            {
                OperationStatus.Conflict => Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = detail,
                    Status = StatusCodes.Status409Conflict
                }),
                OperationStatus.NotFound => NotFound(new ProblemDetails
                {
                    Title = "Not found",
                    Detail = detail,
                    Status = StatusCodes.Status404NotFound
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
