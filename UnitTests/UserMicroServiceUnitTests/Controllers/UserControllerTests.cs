using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Controllers;
using UserMicroService.Dtos;
using UserMicroService.Models;
using UserMicroService.Orchestrators;

namespace UserMicroServiceUnitTests.Controllers
{
    public class UserControllerTests
    {
        private readonly Mock<IUserOrchestrator> _orchestrator = new Mock<IUserOrchestrator>(MockBehavior.Strict);

        private UserController CreateSut()
        {
            return new UserController(_orchestrator.Object)
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private void ArrangeAdd(OperationResult<UserModel> result)
        {
            _orchestrator
                .Setup(o => o.AddUserAsync(It.IsAny<AddUserModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        private void ArrangeGet(Guid userId, OperationResult<UserModel> result)
        {
            _orchestrator
                .Setup(o => o.GetUserAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        // ------------------------------------------------------------ AddUserProfile
        
        [Fact]
        public async Task GivenSuccess_WhenCallingAddUserProfile_Returns201PointingAtGetAction()
        {
            UserModel created = TestData.UserModel();
            ArrangeAdd(OperationResult<UserModel>.Success(created));

            IActionResult result = await CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(UserController.GetUserProfile), createdResult.ActionName);
            Assert.Equal(created.Id, createdResult.RouteValues!["userId"]);
        }

        [Fact]
        public async Task GivenSuccess_WhenCallingAddUserProfile_ReturnsCreatedProfileInBody()
        {
            UserModel created = TestData.UserModel();
            ArrangeAdd(OperationResult<UserModel>.Success(created));

            IActionResult result = await CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None);

            var body = Assert.IsType<UserDto>(((CreatedAtActionResult)result).Value);
            Assert.Equal(created.Id, body.Id);
            Assert.Equal(created.Email, body.Email);
            Assert.Equal(created.UserName, body.UserName);
            Assert.Equal(created.DisplayName, body.DisplayName);
        }

        [Fact]
        public async Task GivenUntrimedIdentityFileds_WhenCallingAddUserProfile_TrimsIdentityFields()
        {
            AddUserModel? captured = null;
            _orchestrator
                .Setup(o => o.AddUserAsync(It.IsAny<AddUserModel>(), It.IsAny<CancellationToken>()))
                .Callback<AddUserModel, CancellationToken>((model, _) => captured = model)
                .ReturnsAsync(OperationResult<UserModel>.Success(TestData.UserModel()));

            await CreateSut().AddUserProfile(
                TestData.AddUserDto(email: "  ada@example.com ", userName: " ada.lovelace ", displayName: " Ada Lovelace "),
                CancellationToken.None);

            Assert.Equal("ada@example.com", captured!.Email);
            Assert.Equal("ada.lovelace", captured.UserName);
            Assert.Equal("Ada Lovelace", captured.DisplayName);
        }

        [Fact]
        public async Task GivenCancellationToken_WhenCallingAddUserProfile_ForwardsCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            _orchestrator
                .Setup(o => o.AddUserAsync(It.IsAny<AddUserModel>(), cts.Token))
                .ReturnsAsync(OperationResult<UserModel>.Success(TestData.UserModel()));

            await CreateSut().AddUserProfile(TestData.AddUserDto(), cts.Token);

            _orchestrator.Verify(o => o.AddUserAsync(It.IsAny<AddUserModel>(), cts.Token), Times.Once);
        }

        [Fact]
        public async Task GivenConflict_WhenCallingAddUserProfile_Returns409ProblemDetails()
        {
            ArrangeAdd(OperationResult<UserModel>.Conflict("EmailAlreadyRegistered", "An account with this email address already exists."));

            IActionResult result = await CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(conflict.Value);
            Assert.Equal("Conflict", problem.Title);
            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
            Assert.Equal("An account with this email address already exists.", problem.Detail);
        }

        [Fact]
        public async Task GivenValidationFailed_WhenCallingAddUserProfile_Returns400WithErrorsGroupedByCode()
        {
            ArrangeAdd(OperationResult<UserModel>.Failure(
                OperationStatus.ValidationFailed,
                new OperationError("PasswordTooShort", "Passwords must bt at least 12 characters."),
                new OperationError("PasswordRequiredDigit", "Passwords must contain a digit")));

            IActionResult result = await CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Equal("Validation failed", problem.Title);
            Assert.Equal(2, problem.Errors.Count);
            Assert.Single(problem.Errors["PasswordTooShort"]);
        }

        [Fact]
        public async Task GivenNotFound_WhenCallingAddUserProfile_Returns404ProblemDetails()
        {
            ArrangeAdd(OperationResult<UserModel>.NotFound("Missing", "Nothing here."));

            IActionResult result = await CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Not found", Assert.IsType<ProblemDetails>(notFound.Value).Title);
        }

        [Theory]
        [InlineData(OperationStatus.Unexpected)]
        [InlineData(OperationStatus.Unauthorized)]
        [InlineData(OperationStatus.Forbidden)]
        public async Task GivenUnmapedStatus_WhenCallingAddUserProfile_FallBackTo500(OperationStatus status)
        {
            ArrangeAdd(OperationResult<UserModel>.Failure(status, new OperationError("Code", "Detail.")));

            IActionResult result = await CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.Equal("Unexpected error", Assert.IsType<ProblemDetails>(objectResult.Value).Title);
        }

        [Fact]
        public async Task GivenNullRefrenceFromOrchestrator_WhenCallingAddUserProfile_IsCaughtReturns500()
        {
            _orchestrator
                .Setup(o => o.AddUserAsync(It.IsAny<AddUserModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NullReferenceException("Object reference not set"));

            IActionResult result = await CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.Equal("Object reference not set", objectResult.Value);
        }

        [Fact]
        public async Task GivenOtherExceptions_WhenCallingAddUserProfile_AreNotCaught()
        {
            _orchestrator
                .Setup(o => o.AddUserAsync(It.IsAny<AddUserModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("New Exception"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().AddUserProfile(TestData.AddUserDto(), CancellationToken.None));
        }

        [Fact]
        public async Task GivenNullRequestBody_WhenCallingAddUserProfile_ThrowsException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => CreateSut().AddUserProfile(null!, CancellationToken.None));
        }

        // ----------------------------------------------------------- GetUserProfile

        [Fact]
        public async Task GivenExistingUser_WhenCallingGetUserProfile_Returns200WithProfile()
        {
            UserModel user = TestData.UserModel(lastSeen: new DateTime(2026, 3, 3, 7, 0, 0, DateTimeKind.Utc));
            ArrangeGet(user.Id, OperationResult<UserModel>.Success(user));

            IActionResult result = await CreateSut().GetUserProfile(user.Id, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<UserDto>(ok.Value);
            Assert.Equal(user.Id, body.Id);
            Assert.Equal(user.Email, body.Email);
            Assert.Equal(user.LastSeen, body.LastSeen);
        }

        [Fact]
        public async Task GivenMissingUser_WhenCallingGetUserProfile_Returns404ProblemDetails()
        {
            Guid userId = Guid.CreateVersion7();
            ArrangeGet(userId, OperationResult<UserModel>.NotFound(
                "UserProfileNotFound",
                "No user profile exists with the suppplied identifier."));

            IActionResult result = await CreateSut().GetUserProfile(userId, CancellationToken.None);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(notFound.Value);
            Assert.Equal("Not found", problem.Title);
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal("No user profile exists with the suppplied identifier.", problem.Detail);
        }

        [Fact]
        public async Task GivenConflict_WhenCallingGetUserProfile_Returns409()
        {
            Guid userId = Guid.CreateVersion7();
            ArrangeGet(userId, OperationResult<UserModel>.Conflict("Conflicted", "Clash."));

            IActionResult result = await CreateSut().GetUserProfile(userId, CancellationToken.None);

            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task GivenValidationFailed_WhenCallingGetUserProfile_Reutrns400()
        {
            Guid userId = Guid.CreateVersion7();
            ArrangeGet(userId, OperationResult<UserModel>.ValidationFailed("UserId", "Malformed."));

            IActionResult result = await CreateSut().GetUserProfile(userId, CancellationToken.None);

            Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(result).Value);
        }

        [Fact]
        public async Task GivenUnexpectedStatus_WhenCallingGetUserProfile_Returns500()
        {
            Guid userId = Guid.CreateVersion7();
            ArrangeGet(userId, OperationResult<UserModel>.Failure(OperationStatus.Unexpected));

            IActionResult result = await CreateSut().GetUserProfile(userId, CancellationToken.None);

            Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<ObjectResult>(result).StatusCode);
        }

        [Fact]
        public async Task GivenNullReferenceFromOrchestrator_WhenCallingGetUserProfile_IsCaughtReturns500()
        {
            Guid userId = Guid.CreateVersion7();
            _orchestrator
                .Setup(o => o.GetUserAsync(userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NullReferenceException("object reference not set"));

            IActionResult result = await CreateSut().GetUserProfile(userId, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.Equal("object reference not set", objectResult.Value);
        }

        [Fact]
        public async Task GivenOtherExceptions_WhenCallingGetUserProfile_AreNotCaught()
        {
            Guid userId = Guid.CreateVersion7();
            _orchestrator
                .Setup(o => o.GetUserAsync(userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("slow"));

            await Assert.ThrowsAsync<TimeoutException>(
                () => CreateSut().GetUserProfile(userId, CancellationToken.None));
        }

        [Fact]
        public async Task GivenCancellationToken_WhenCallingGetUserProfile_ForwardsCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            Guid userId = Guid.CreateVersion7();
            _orchestrator
                .Setup(o => o.GetUserAsync(userId, cts.Token))
                .ReturnsAsync(OperationResult<UserModel>.Success(TestData.UserModel(id: userId)));

            await CreateSut().GetUserProfile(userId, cts.Token);

            _orchestrator.Verify(o => o.GetUserAsync(userId, cts.Token), Times.Once);
        }

        [Fact]
        public async Task GivenRouteId_WhenCallingGetUserProfile_PassesRouteIdUnchanged()
        {
            Guid userId = Guid.CreateVersion7();
            ArrangeGet(userId, OperationResult<UserModel>.Success(TestData.UserModel(userId)));

            await CreateSut().GetUserProfile(userId, CancellationToken.None);

            _orchestrator.Verify(o => o.GetUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        }

    }
}
