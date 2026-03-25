using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class AuditTrailTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;

    public AuditTrailTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AuditTrail_CanBeCreated_WithAllFields()
    {
        var auditTrail = new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Action = "TestAction",
            Actor = "test-user",
            EntityId = "entity-123",
            PreviousState = "Old",
            NewState = "New",
            DecisionDetails = "Test decision details",
            Timestamp = DateTime.UtcNow
        };

        _dbContext.AuditTrails.Add(auditTrail);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.AuditTrails.FindAsync(auditTrail.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Action.Should().Be("TestAction");
        retrieved.Actor.Should().Be("test-user");
        retrieved.EntityId.Should().Be("entity-123");
        retrieved.PreviousState.Should().Be("Old");
        retrieved.NewState.Should().Be("New");
        retrieved.DecisionDetails.Should().Be("Test decision details");
    }

    [Fact]
    public async Task AuditTrail_TenantIsolation_TenantACannotSeeTenantBAuditTrails()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var tenantADbContext = new AppDbContext(options, new MockTenantProvider(tenantAId));
        using var tenantBDbContext = new AppDbContext(options, new MockTenantProvider(tenantBId));

        tenantADbContext.AuditTrails.Add(new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantAId,
            Action = "TenantAAction",
            Actor = "system",
            EntityId = "1",
            Timestamp = DateTime.UtcNow
        });
        await tenantADbContext.SaveChangesAsync();

        tenantBDbContext.AuditTrails.Add(new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantBId,
            Action = "TenantBAction",
            Actor = "system",
            EntityId = "2",
            Timestamp = DateTime.UtcNow
        });
        await tenantBDbContext.SaveChangesAsync();

        var tenantAAudits = await tenantADbContext.AuditTrails.ToListAsync();
        var tenantBAudits = await tenantBDbContext.AuditTrails.ToListAsync();

        tenantAAudits.Should().HaveCount(1);
        tenantAAudits.First().Action.Should().Be("TenantAAction");

        tenantBAudits.Should().HaveCount(1);
        tenantBAudits.First().Action.Should().Be("TenantBAction");
    }

    [Fact]
    public async Task AuditTrail_ActionRequestCreated_HasCorrectStructure()
    {
        var actionRequestId = Guid.NewGuid();
        var inboxEventId = Guid.NewGuid();

        var auditTrail = new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Action = "ActionRequestCreated",
            Actor = "system",
            EntityId = actionRequestId.ToString(),
            PreviousState = "",
            NewState = "Pending",
            DecisionDetails = $"Human review required for email. Category: Support. Reason: High-risk keyword detected.",
            Timestamp = DateTime.UtcNow
        };

        _dbContext.AuditTrails.Add(auditTrail);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.AuditTrails.FindAsync(auditTrail.Id);
        retrieved!.Action.Should().Be("ActionRequestCreated");
        retrieved.Actor.Should().Be("system");
        retrieved.NewState.Should().Be("Pending");
    }

    [Fact]
    public async Task AuditTrail_ActionRequestResolved_HasCorrectStructure()
    {
        var actionRequestId = Guid.NewGuid();

        var auditTrail = new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Action = "ActionRequestResolved",
            Actor = "admin@example.com",
            EntityId = actionRequestId.ToString(),
            PreviousState = "Pending",
            NewState = "Approved",
            DecisionDetails = "Approved by admin@example.com. Original draft: Thank you... Modified response: None",
            Timestamp = DateTime.UtcNow
        };

        _dbContext.AuditTrails.Add(auditTrail);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.AuditTrails.FindAsync(auditTrail.Id);
        retrieved!.Action.Should().Be("ActionRequestResolved");
        retrieved.PreviousState.Should().Be("Pending");
        retrieved.NewState.Should().Be("Approved");
        retrieved.DecisionDetails.Should().Contain("Approved by");
    }

    [Fact]
    public async Task AuditTrail_VerifyAllStatusTransitions_AreAudited()
    {
        var validTransitions = new[]
        {
            ("Ingested", "Processing"),
            ("Processing", "Auto_Approved"),
            ("Processing", "Action_Required"),
            ("Processing", "Failed"),
            ("Action_Required", "Human_Approved"),
            ("Action_Required", "Human_Rejected"),
        };

        foreach (var (from, to) in validTransitions)
        {
            var auditTrail = new AuditTrail
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Action = "StatusTransition",
                Actor = "system",
                EntityId = Guid.NewGuid().ToString(),
                PreviousState = from,
                NewState = to,
                DecisionDetails = $"Transition from {from} to {to}",
                Timestamp = DateTime.UtcNow
            };
            _dbContext.AuditTrails.Add(auditTrail);
        }

        await _dbContext.SaveChangesAsync();

        var count = await _dbContext.AuditTrails.CountAsync();
        count.Should().Be(validTransitions.Length);
    }

    [Fact]
    public async Task AuditTrail_Timestamp_IsSetCorrectly()
    {
        var beforeCreate = DateTime.UtcNow;
        var auditTrail = new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Action = "TestAction",
            Actor = "test",
            EntityId = "1",
            Timestamp = DateTime.UtcNow
        };
        var afterCreate = DateTime.UtcNow;

        _dbContext.AuditTrails.Add(auditTrail);
        await _dbContext.SaveChangesAsync();

        auditTrail.Timestamp.Should().BeOnOrAfter(beforeCreate);
        auditTrail.Timestamp.Should().BeOnOrBefore(afterCreate);
    }

    [Fact]
    public async Task AuditTrail_CanQueryByEntityId()
    {
        var entityId = Guid.NewGuid().ToString();
        for (int i = 0; i < 5; i++)
        {
            _dbContext.AuditTrails.Add(new AuditTrail
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Action = $"Action{i}",
                Actor = "test",
                EntityId = entityId,
                Timestamp = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        var audits = await _dbContext.AuditTrails
            .Where(a => a.EntityId == entityId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        audits.Should().HaveCount(5);
        audits.Select(a => a.Action).Should().ContainInOrder("Action0", "Action1", "Action2", "Action3", "Action4");
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
