using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class GlobalRoleTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly string _dbName;

    public GlobalRoleTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var mockTenantProvider = new MockTenantProvider();
        _db = new AppDbContext(options, mockTenantProvider);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task SuperAdmin_UserRole_IsPlatformAdmin()
    {
        var role = UserRole.SuperAdmin;
        role.IsPlatformAdmin().Should().BeTrue();
    }

    [Fact]
    public async Task InternalOperator_UserRole_IsPlatformAdmin()
    {
        var role = UserRole.InternalOperator;
        role.IsPlatformAdmin().Should().BeTrue();
    }

    [Fact]
    public async Task CompanyOwner_UserRole_IsNotPlatformAdmin()
    {
        var role = UserRole.CompanyOwner;
        role.IsPlatformAdmin().Should().BeFalse();
    }

    [Fact]
    public async Task ParseRole_SuperAdmin_ReturnsSuperAdmin()
    {
        var role = UserRoleExtensions.ParseRole("SuperAdmin");
        role.Should().Be(UserRole.SuperAdmin);
    }

    [Fact]
    public async Task ParseRole_InternalOperator_ReturnsInternalOperator()
    {
        var role = UserRoleExtensions.ParseRole("InternalOperator");
        role.Should().Be(UserRole.InternalOperator);
    }

    [Fact]
    public async Task ParseRole_Member_ReturnsOperator()
    {
        var role = UserRoleExtensions.ParseRole("Member");
        role.Should().Be(UserRole.Operator);
    }

    [Fact]
    public async Task ToClaimValue_SuperAdmin_ReturnsSuperAdmin()
    {
        UserRole.SuperAdmin.ToClaimValue().Should().Be("SuperAdmin");
    }

    [Fact]
    public async Task ToClaimValue_CompanyOwner_ReturnsCompanyOwner()
    {
        UserRole.CompanyOwner.ToClaimValue().Should().Be("CompanyOwner");
    }

    [Fact]
    public async Task DbInitializer_CreatesSuperAdminWithCorrectGlobalRole()
    {
        var logger = new TestLogger();
        await DbInitializer.SeedAsync(_db, logger);

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == "superadmin@orvixflow.local");

        user.Should().NotBeNull();
        user!.Role.Should().Be("SuperAdmin");

        var membership = await _db.UserCompanyMemberships.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == user.Id);

        membership.Should().NotBeNull();
        membership!.CompanyRole.Should().Be("CompanyOwner");

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId);

        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Platform Admin Org");
    }

    [Fact]
    public async Task DbInitializer_IsIdempotent()
    {
        var logger = new TestLogger();
        await DbInitializer.SeedAsync(_db, logger);
        await DbInitializer.SeedAsync(_db, logger);

        var count = await _db.Users.IgnoreQueryFilters()
            .CountAsync(u => u.Email == "superadmin@orvixflow.local");

        count.Should().Be(1);
    }

    private class MockTenantProvider : OrvixFlow.Core.Interfaces.ITenantProvider
    {
        public Guid GetTenantId() => Guid.Empty;
    }

    private class TestLogger : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
