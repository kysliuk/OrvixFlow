using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class MailboxConnectionCredentialControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly MailboxConnectionsController _controller;
    private readonly Guid _tenantId;
    private readonly Guid _userId;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<IMailboxCredentialService> _mailboxCredentialServiceMock;

    public MailboxConnectionCredentialControllerTests()
    {
        _tenantId = Guid.NewGuid();
        _userId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(options, tenantProvider);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mailboxCredentialServiceMock = new Mock<IMailboxCredentialService>();

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Frontend:BaseUrl"]).Returns("http://localhost:3000");

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        _controller = new MailboxConnectionsController(
            _dbContext,
            tenantProvider,
            new Mock<IN8nProvisioningService>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<MailboxConnectionsController>>().Object,
            _mailboxCredentialServiceMock.Object,
            _memoryCache,
            httpClientFactoryMock.Object,
            mockConfig.Object);

        SetupControllerContext();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _memoryCache.Dispose();
    }

    private void SetupControllerContext(string role = "CompanyAdmin")
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new("ActiveCompanyId", _tenantId.ToString()),
            new("sub", _userId.ToString()),
            new("Role", role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Authorize_WhenNotConfigured_ReturnsMockRedirectUrl()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = _userId,
            EmailAddress = "test@gmail.com",
            Provider = "Gmail",
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Authorize(connection.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;

        var isMockProp = value.GetType().GetProperty("isMock");
        ((bool)isMockProp!.GetValue(value)!).Should().BeTrue();

        var authUrlProp = value.GetType().GetProperty("authorizationUrl");
        var authUrl = (string)authUrlProp!.GetValue(value)!;
        authUrl.Should().Contain("/mailbox-callback");
        authUrl.Should().Contain("code=mock_code");
    }

    [Fact]
    public async Task Callback_WithInvalidState_ReturnsBadRequest()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = _userId,
            EmailAddress = "test@gmail.com",
            Provider = "Gmail",
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var request = new MailboxConnectionsController.CallbackRequest
        {
            Code = "mock_code",
            State = "wrong_state"
        };

        var result = await _controller.Callback(connection.Id, request);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;

        var errorProp = value.GetType().GetProperty("error");
        ((string)errorProp!.GetValue(value)!).Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Callback_WithValidMockCode_StoresMockCredentialAndSucceeds()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = _userId,
            EmailAddress = "test@gmail.com",
            Provider = "Gmail",
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var state = "correct_state";
        _memoryCache.Set($"oauth_state_{connection.Id}", state);

        var request = new MailboxConnectionsController.CallbackRequest
        {
            Code = "mock_code",
            State = state
        };

        _mailboxCredentialServiceMock.Setup(s => s.StoreCredentialAsync(
            _tenantId,
            connection.Id,
            "Gmail",
            "mock_subject_id",
            It.Is<string>(t => t.StartsWith("mock_access_token_")),
            It.Is<string>(t => t.StartsWith("mock_refresh_token_")),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<DateTime>()))
            .ReturnsAsync(new MailboxCredential())
            .Verifiable();

        var result = await _controller.Callback(connection.Id, request);

        result.Should().BeOfType<OkObjectResult>();
        _mailboxCredentialServiceMock.Verify();
    }

    [Fact]
    public async Task ManageEndpoints_WhenUserIsNeitherAdminNorOwner_ReturnsForbidden()
    {
        SetupControllerContext("Operator");

        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "other@gmail.com",
            Provider = "Gmail",
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Authorize(connection.Id);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
