using System.Threading.Tasks;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Interfaces;
using Xunit;

namespace OrvixFlow.Tests;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<AuthController>>();
        _controller = new AuthController(_authServiceMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Register_WithValidInput_ShouldReturnOk()
    {
        // Arrange
        _authServiceMock.Setup(x => x.RegisterAsync("test@example.com", "password", "Test User"))
            .ReturnsAsync(new AuthResult(true, "token123", null, new UserProfile(System.Guid.NewGuid(), System.Guid.NewGuid(), System.Guid.NewGuid(), "test@example.com", "Test User", "Owner", "Trialing", new System.Collections.Generic.List<CompanyMembershipSummary>())));

        var req = new RegisterRequest("test@example.com", "password", "Test User");

        // Act
        var result = await _controller.Register(req);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Register_WithEmptyEmailOrPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var req = new RegisterRequest("", "password", "Test User");

        // Act
        var result = await _controller.Register(req);

        // Assert
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        _authServiceMock.Setup(x => x.RegisterAsync("test@example.com", "password", "Test User"))
            .ReturnsAsync(new AuthResult(false, Error: "An account with this email already exists."));

        var req = new RegisterRequest("test@example.com", "password", "Test User");

        // Act
        var result = await _controller.Register(req);

        // Assert
        var conflictResult = result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Register_WithValidationFailure_ShouldReturnBadRequest()
    {
        _authServiceMock.Setup(x => x.RegisterAsync("test@example.com", "weak", "Test User"))
            .ReturnsAsync(new AuthResult(false, Error: "Password must be at least 12 characters long."));

        var result = await _controller.Register(new RegisterRequest("test@example.com", "weak", "Test User"));

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Register_WhenExceptionOccurs_ShouldReturn500()
    {
        // Arrange
        _authServiceMock.Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new System.Exception("Database connection failed"));

        var req = new RegisterRequest("test@example.com", "password", "Test User");

        // Act
        var result = await _controller.Register(req);

        // Assert
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Login_WithValidInput_ShouldReturnOk()
    {
        // Arrange
        _authServiceMock.Setup(x => x.LoginAsync("test@example.com", "password"))
            .ReturnsAsync(new AuthResult(true, "token123", null, new UserProfile(System.Guid.NewGuid(), System.Guid.NewGuid(), System.Guid.NewGuid(), "test@example.com", "Test User", "CompanyOwner", "Trialing", new System.Collections.Generic.List<CompanyMembershipSummary>()), "refresh-123"));

        var req = new LoginRequest("test@example.com", "password");

        // Act
        var result = await _controller.Login(req);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var payload = okResult.Value;
        payload.Should().NotBeNull();
        payload!.GetType().GetProperty("refreshToken")!.GetValue(payload).Should().Be("refresh-123");
    }

    [Fact]
    public async Task Logout_WithMissingRefreshToken_ShouldReturnBadRequest()
    {
        var result = await _controller.Logout(new LogoutRequest(""));

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task LogoutAll_WithInvalidUserContext_ShouldReturnUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "not-a-guid")], "TestAuth"))
            }
        };

        var result = await _controller.LogoutAll();

        var unauthorized = result as UnauthorizedObjectResult;
        unauthorized.Should().NotBeNull();
        unauthorized!.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task LogoutAll_WithValidUserContext_ShouldInvokeService()
    {
        var userId = System.Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], "TestAuth"))
            }
        };

        var result = await _controller.LogoutAll();

        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        _authServiceMock.Verify(x => x.LogoutAllAsync(userId), Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        _authServiceMock.Setup(x => x.LoginAsync("test@example.com", "wrong"))
            .ReturnsAsync(new AuthResult(false, Error: "Invalid email or password."));

        var req = new LoginRequest("test@example.com", "wrong");

        // Act
        var result = await _controller.Login(req);

        // Assert
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task OAuthProvision_WithExistingAccountConflict_ShouldReturnConflict()
    {
        _authServiceMock.Setup(x => x.ProvisionOAuthUserAsync("test@example.com", "Test User", "google", "ext-1"))
            .ReturnsAsync(new AuthResult(false, Error: "An account with this email already exists. Please sign in with your original authentication method."));

        var result = await _controller.OAuthProvision(new OAuthProvisionRequest("test@example.com", "Test User", "google", "ext-1"));

        var conflict = result as ConflictObjectResult;
        conflict.Should().NotBeNull();
        conflict!.StatusCode.Should().Be(409);
    }

    [Fact]
    public void Me_ReadsJwtEmailClaim_WhenClaimMappingIsCleared()
    {
        var claims = new[]
        {
            new Claim("sub", System.Guid.NewGuid().ToString()),
            new Claim("email", "jwt@example.com"),
            new Claim("TenantId", System.Guid.NewGuid().ToString()),
            new Claim("Role", "Operator")
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        var result = _controller.Me() as OkObjectResult;

        result.Should().NotBeNull();
        var payload = result!.Value!;
        payload.GetType().GetProperty("email")!.GetValue(payload).Should().Be("jwt@example.com");
    }
}
