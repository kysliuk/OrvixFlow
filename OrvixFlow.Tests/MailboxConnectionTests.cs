using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class MailboxConnectionTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;

    public MailboxConnectionTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Create_Connection_SavesToDb()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "test@example.com",
            Provider = "Gmail",
            IsActive = true
        };

        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.MailboxConnections.FindAsync(connection.Id);
        saved.Should().NotBeNull();
        saved!.EmailAddress.Should().Be("test@example.com");
        saved.Provider.Should().Be("Gmail");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task QueryFilter_IsolatesByTenant()
    {
        var otherTenant = Guid.NewGuid();

        _dbContext.MailboxConnections.Add(new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "tenant1@example.com",
            Provider = "Gmail"
        });
        _dbContext.MailboxConnections.Add(new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenant,
            UserId = Guid.NewGuid(),
            EmailAddress = "tenant2@example.com",
            Provider = "Outlook"
        });
        await _dbContext.SaveChangesAsync();

        var results = await _dbContext.MailboxConnections.ToListAsync();
        results.Should().HaveCount(1);
        results[0].EmailAddress.Should().Be("tenant1@example.com");
    }

    [Fact]
    public async Task ToggleConnection_ActivatesAndDeactivates()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "test@example.com",
            Provider = "Gmail",
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        connection.IsActive = true;
        connection.ConnectedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.MailboxConnections.FindAsync(connection.Id);
        saved!.IsActive.Should().BeTrue();
        saved.ConnectedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Query_ReturnsOnlyActiveConnections()
    {
        _dbContext.MailboxConnections.Add(new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "active@example.com",
            Provider = "Gmail",
            IsActive = true
        });
        _dbContext.MailboxConnections.Add(new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "inactive@example.com",
            Provider = "Outlook",
            IsActive = false
        });
        await _dbContext.SaveChangesAsync();

        var active = await _dbContext.MailboxConnections.Where(c => c.IsActive).ToListAsync();
        active.Should().HaveCount(1);
        active[0].EmailAddress.Should().Be("active@example.com");
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
