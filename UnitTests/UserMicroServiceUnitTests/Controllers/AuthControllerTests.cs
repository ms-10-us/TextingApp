using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Controllers;
using UserMicroService.Dtos;
using UserMicroService.Models;
using UserMicroService.Orchestrators;

namespace UserMicroServiceUnitTests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthOrchestrator> _orchestrator = new Mock<IAuthOrchestrator>(MockBehavior.Strict);

        private AuthController CreateSut()
        {
            return new AuthController(_orchestrator.Object)
            {
                ControllerContext = new ControllerContext() { HttpContext = new DefaultHttpContext() },
            };
        }

        private void ArrangeResult(OperationResult<LoginResultModel> result)
        {
            _orchestrator
                .Setup(o => o.LoginAsync(It.IsAny<LoginModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenCallingLogin_Returns200WithToken()
        {
            DateTime expiry = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            UserModel user = TestData.UserModel();
            ArrangeResult(OperationResult<LoginResultModel>.Success(
                TestData.LoginResult(user, TestData.AccessToken("signed-jwt", expiry))));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<LoginDtoResponse>(ok.Value);
            Assert.Equal("Bearer", body.TokenType);
            Assert.Equal("signed-jwt", body.AccessToken);
            Assert.Equal(expiry, body.ExpiresAtUtc);
            Assert.Equal(user.Id, body.User.Id);
        }

        [Fact]
        public async Task GivenValidCredentials_WhenCallingLogin_MaksTheResponseAsUncacheable()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Success(TestData.LoginResult()));
            AuthController sut = CreateSut();

            await sut.Login(TestData.LoginDto(), CancellationToken.None);

            Assert.Equal("no-store", sut.Response.Headers.CacheControl.ToString());
            Assert.Equal("no-cache", sut.Response.Headers.Pragma.ToString());
        }

        [Fact]
        public async Task GivenInvalidCredentials_WhenCallingLogin_DoesNotSetCacheHeaders()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Unauthorized("InvalidCredentials", "Nope."));
            AuthController sut = CreateSut();

            await sut.Login(TestData.LoginDto(), CancellationToken.None);

            Assert.False(sut.Response.Headers.ContainsKey("Cache-Control"));
        }

        [Fact]
        public async Task GivenIdentifierWithSapces_WhenCallingLogin_TrimsIdentifier()
        {
            LoginModel? captured = null;
            _orchestrator
                .Setup(o => o.LoginAsync(It.IsAny<LoginModel>(), It.IsAny<CancellationToken>()))
                .Callback<LoginModel, CancellationToken>((model, _) => captured = model)
                .ReturnsAsync(OperationResult<LoginResultModel>.Success(TestData.LoginResult()));

            await CreateSut().Login(TestData.LoginDto(identifier: "  ada@example.com "), CancellationToken.None);

            Assert.Equal("ada@example.com", captured!.Identifier);
        }

        [Fact]
        public async Task GivenCancellationToken_WhenCallingLogin_ForwardsCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            _orchestrator
                .Setup(o => o.LoginAsync(It.IsAny<LoginModel>(), cts.Token))
                .ReturnsAsync(OperationResult<LoginResultModel>.Success(TestData.LoginResult()));

            await CreateSut().Login(TestData.LoginDto(), cts.Token);

            _orchestrator.Verify(o => o.LoginAsync(It.IsAny<LoginModel>(), cts.Token), Times.Once);
        }

        [Fact]
        public async Task GivenUnauthorizedCredentials_WhenCallingLogin_Returns401ProblemDetails()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Unauthorized("InvalidCredentials", "The supplied credentials are incorrect."));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Unauthorized", problem.Title);
            Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
            Assert.Equal("The supplied credentials are incorrect.", problem.Detail);
        }

        [Fact]
        public async Task GivenUnauthorizedCredentials_WhenCallingLogin_LeaksNoErrorCodeInTheBody()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Unauthorized("InvalidCredentials", "The supplied credentials are incorrect."));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var problem = (ProblemDetails)((ObjectResult)result).Value!;
            Assert.DoesNotContain("InvalidCredentials", problem.Detail);
        }

        [Fact]
        public async Task GivenForbiddenCredentials_WhenCallingLogin_Returns403ProblemDetails()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Forbidden("AccountLockedOut", "Try again later."));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Forbidden", problem.Title);
            Assert.Equal("Try again later.", problem.Detail);
        }

        [Fact]
        public async Task GivenCredentialsNotFound_WhenCallingLogin_Returns404ProblemDetails()
        {
            ArrangeResult(OperationResult<LoginResultModel>.NotFound("Missing", "Nothing here."));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(notFound.Value);
            Assert.Equal("Not found", problem.Title);
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        }

        [Fact]
        public async Task GivenConflictCredentials_WhenCallingLogin_Returns409ProblemDetails()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Conflict("Conflicted", "Clash."));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(conflict.Value);
            Assert.Equal("Conflict", problem.Title);
            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        }

        [Fact]
        public async Task GivenValidationFailed_WhenCallingLogin_Returns400ValidationProbelmDetails()
        {
            ArrangeResult(OperationResult<LoginResultModel>.ValidationFailed("Identifier", "Identifier is required."));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Equal("Validation failed", problem.Title);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
            Assert.Equal(new[] { "Identifier is required." }, problem.Errors["Identifier"]);
        }

        [Fact]
        public async Task GivenValidationFailed_WhenCallingLogin_GroupsSeveralDescriptionUnderOneCode()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Failure(
                OperationStatus.ValidationFailed,
                new OperationError("Password", "Too short."),
                new OperationError("Password", "Needs a digit."),
                new OperationError("Identifier", "Required.")));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
            Assert.Equal(2, problem.Errors.Count);
            Assert.Equal(2, problem.Errors["Password"].Length);
            Assert.Single(problem.Errors["Identifier"]);
        }

        [Fact]
        public async Task GivenValidationFailed_WhenCallingLogin_TreatsCodesAsCaseSensitiveGroups()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Failure(
                OperationStatus.ValidationFailed,
                new OperationError("Password", "One."),
                new OperationError("password", "Two.")
                ));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
            Assert.Equal(2, problem.Errors.Count);
        }

        [Fact]
        public async Task GivenUnexpectedStatus_WhenCallingLogin_Returns500WithoutDetail()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Failure(
                OperationStatus.Unexpected,
                new OperationError("Boom", "Internal detail that must not leak.")));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Unexpected error", problem.Title);
            Assert.Null(problem.Detail);
        }

        [Fact]
        public async Task GivenMultipleErrors_WhenCallingLogin_JoinDescriptionWithSingleSpace()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Failure(
                OperationStatus.Unauthorized,
                new OperationError("A", "First."),
                new OperationError("B", "Second.")
                ));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var problem = (ProblemDetails)((ObjectResult)result).Value!;

            Assert.Equal("First. Second.", problem.Detail);
        }

        [Fact]
        public async Task GivenFailureWithNoErrors_WhenCallingLogin_ProducesAnEmptyDetail()
        {
            ArrangeResult(OperationResult<LoginResultModel>.Failure(OperationStatus.Unauthorized));

            IActionResult result = await CreateSut().Login(TestData.LoginDto(), CancellationToken.None);

            var problem = (ProblemDetails)((ObjectResult)result).Value!;
            Assert.Equal(string.Empty, problem.Detail);
        }

        [Fact]
        public async Task GivenOrchestratorThrowsException_WhenCallingLogin_Propagates()
        {
            _orchestrator
                .Setup(o => o.LoginAsync(It.IsAny<LoginModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Boom"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().Login(TestData.LoginDto(), CancellationToken.None));
        }

        [Fact]
        public async Task GivenNullRequestBody_WhenCallingLogin_ThrowsException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => CreateSut().Login(null!, CancellationToken.None));
        }

    }
}
