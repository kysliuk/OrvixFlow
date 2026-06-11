using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class N8nProvisioningWithCredentialTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;
    private readonly Mock<IN8nProvisioningService> _n8nProvisioningMock;
    private readonly Mock<IMailboxCredentialService> _credServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly MailboxConnectionsController _controller;

    public N8nProvisioningWithCredentialTests()
    {
        _tenantId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(options, tenantProvider);

        _n8nProvisioningMock = new Mock<IN8nProvisioningService>();
        _credServiceMock = new Mock<IMailboxCredentialService>();

        var services = new ServiceCollection();
        services.AddSingleton<AppDbContext>(_dbContext);
        services.AddSingleton<IN8nProvisioningService>(_n8nProvisioningMock.Object);
        services.AddSingleton<IMailboxCredentialService>(_credServiceMock.Object);
        services.AddSingleton<ILogger<MailboxConnectionsController>>(new Mock<ILogger<MailboxConnectionsController>>().Object);

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Mailbox:Google:ClientId"]).Returns("real_google_client_id");
        mockConfig.Setup(c => c["Mailbox:Google:ClientSecret"]).Returns("real_google_client_secret");
        services.AddSingleton<IConfiguration>(mockConfig.Object);

        _serviceProvider = services.BuildServiceProvider();

        _controller = new MailboxConnectionsController(
            _dbContext,
            tenantProvider,
            _n8nProvisioningMock.Object,
            _serviceProvider,
            new Mock<ILogger<MailboxConnectionsController>>().Object,
            _credServiceMock.Object,
            new Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Object,
            new Mock<System.Net.Http.IHttpClientFactory>().Object,
            mockConfig.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task ProvisionN8nWorkflowJob_WithCredential_RetrievesDecryptedTokensAndSendsToN8n()
    {
        var credId = Guid.NewGuid();
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "test@gmail.com",
            Provider = "Gmail",
            CredentialId = credId,
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        _credServiceMock.Setup(s => s.GetDecryptedTokensAsync(credId))
            .ReturnsAsync(("access_token_abc", "refresh_token_xyz"));

        _n8nProvisioningMock.Setup(s => s.CreateCredentialAsync(
            "Gmail",
            "test@gmail.com",
            It.Is<object>(obj => obj != null)))
            .ReturnsAsync("n8n_cred_id_123");

        _n8nProvisioningMock.Setup(s => s.ProvisionWorkflowAsync(
            It.IsAny<string>(),
            "test@gmail.com",
            _tenantId))
            .ReturnsAsync("n8n_workflow_id_999");

        await _controller.ProvisionN8nWorkflowJob(connection.Id, _tenantId);

        var updatedConnection = await _dbContext.MailboxConnections.FindAsync(connection.Id);
        updatedConnection.Should().NotBeNull();
        updatedConnection!.N8nCredentialId.Should().Be("n8n_cred_id_123");
        updatedConnection!.N8nWorkflowId.Should().Be("n8n_workflow_id_999");
        updatedConnection!.IsActive.Should().BeTrue();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
