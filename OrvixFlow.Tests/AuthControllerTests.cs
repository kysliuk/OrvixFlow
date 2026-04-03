using System.Threading.Tasks;
using FluentAssertions;
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
            .ReturnsAsync(new AuthResult(true, "token123", null, new UserProfile(System.Guid.NewGuid(), System.Guid.NewGuid(), System.Guid.NewGuid(), "test@example.com", "Test User", "CompanyOwner", "Trialing", new System.Collections.Generic.List<CompanyMembershipSummary>())));

        var req = new LoginRequest("test@example.com", "password");

        // Act
        var result = await _controller.Login(req);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
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
}
