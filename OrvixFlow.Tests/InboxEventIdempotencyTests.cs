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

public class InboxEventIdempotencyTests : IDisposable
{
    private readonly string _databaseName;

    public InboxEventIdempotencyTests()
    {
        _databaseName = Guid.NewGuid().ToString();
    }

    public void Dispose()
    {
    }

    [Fact]
    public async Task CreateWithIdempotencyCheck_NewMessage_CreatesEvent()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();

        var (inboxEvent, isDuplicate) = await repository.CreateWithIdempotencyCheckAsync(
            messageId, tenantId, "test@example.com", "Test User", "Test Subject", "Test body");

        isDuplicate.Should().BeFalse();
        inboxEvent.Should().NotBeNull();
        inboxEvent!.MessageId.Should().Be(messageId);
        inboxEvent.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CreateWithIdempotencyCheck_Duplicate_ReturnsExisting()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();

        var (first, firstIsDuplicate) = await repository.CreateWithIdempotencyCheckAsync(
            messageId, tenantId, "test@example.com", "Test User", "Test Subject", "Test body");

        var (second, secondIsDuplicate) = await repository.CreateWithIdempotencyCheckAsync(
            messageId, tenantId, "different@example.com", "Different User", "Different Subject", "Different body");

        firstIsDuplicate.Should().BeFalse();
        secondIsDuplicate.Should().BeTrue();
        second!.Id.Should().Be(first!.Id);
        second.SenderEmail.Should().Be(first.SenderEmail);
    }

    [Fact]
    public async Task CreateWithIdempotencyCheck_DifferentTenant_CreatesEvent()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();

        var (eventA, isDuplicateA) = await repository.CreateWithIdempotencyCheckAsync(
            messageId, tenantA, "test@example.com", "Test User", "Test Subject", "Test body");

        var (eventB, isDuplicateB) = await repository.CreateWithIdempotencyCheckAsync(
            messageId, tenantB, "test@example.com", "Test User", "Test Subject", "Test body");

        isDuplicateA.Should().BeFalse();
        isDuplicateB.Should().BeFalse();
        eventA!.Id.Should().NotBe(eventB!.Id);
    }

    [Fact]
    public async Task CreateWithIdempotencyCheck_WithWebhookPath_StoresWebhookPath()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantId = Guid.NewGuid();
        var webhookPath = "my-callback-path";

        var (inboxEvent, _) = await repository.CreateWithIdempotencyCheckAsync(
            Guid.NewGuid().ToString(), tenantId, "test@example.com", "Test", "Test", "Body", webhookPath);

        inboxEvent!.WebhookCallbackPath.Should().Be(webhookPath);
    }

    [Fact]
    public async Task GetById_ValidId_ReturnsEvent()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantId = Guid.NewGuid();

        var (created, _) = await repository.CreateWithIdempotencyCheckAsync(
            Guid.NewGuid().ToString(), tenantId, "test@example.com", "Test", "Subject", "Body");

        var retrieved = await repository.GetByIdAsync(created!.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetById_InvalidId_ReturnsNull()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);

        var retrieved = await repository.GetByIdAsync(Guid.NewGuid());

        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetByMessageId_ValidMessageId_ReturnsEvent()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var messageId = Guid.NewGuid().ToString();

        await repository.CreateWithIdempotencyCheckAsync(
            messageId, Guid.NewGuid(), "test@example.com", "Test", "Subject", "Body");

        var retrieved = await repository.GetByMessageIdAsync(messageId);

        retrieved.Should().NotBeNull();
        retrieved!.MessageId.Should().Be(messageId);
    }

    [Fact]
    public async Task UpdateStatus_ValidId_Updates()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);

        var (created, _) = await repository.CreateWithIdempotencyCheckAsync(
            Guid.NewGuid().ToString(), Guid.NewGuid(), "test@example.com", "Test", "Subject", "Body");

        await repository.UpdateStatusAsync(created!.Id, InboxEventStatus.Processing);

        var updated = await repository.GetByIdAsync(created.Id);
        updated!.Status.Should().Be(InboxEventStatus.Processing);
    }

    [Fact]
    public async Task UpdateStatus_InvalidId_NoOp()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);

        var exception = await Record.ExceptionAsync(() =>
            repository.UpdateStatusAsync(Guid.NewGuid(), InboxEventStatus.Processing));

        exception.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentCreate_SameMessageId_OnlyOneCreated()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();

        var first = await repository.CreateWithIdempotencyCheckAsync(
            messageId, tenantId, "user1@example.com", "User 1", "Subject", "Body");

        var second = await repository.CreateWithIdempotencyCheckAsync(
            messageId, tenantId, "user2@example.com", "User 2", "Subject", "Body");

        first.IsDuplicate.Should().BeFalse();
        second.IsDuplicate.Should().BeTrue();
        second.Item1!.Id.Should().Be(first.Item1!.Id);

        var countInDb = await dbContext.InboxEvents.IgnoreQueryFilters()
            .CountAsync(e => e.MessageId == messageId);
        countInDb.Should().Be(1);
    }

    [Fact]
    public async Task SequentialCreate_SameMessageId_FirstSucceedsSecondIsDuplicate()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();

        var results = new List<(InboxEvent?, bool)>();
        for (int i = 0; i < 5; i++)
        {
            var result = await repository.CreateWithIdempotencyCheckAsync(
                messageId, tenantId, $"user{i}@example.com", $"User {i}", "Subject", "Body");
            results.Add(result);
        }

        var created = results.Count(r => !r.Item2);
        var duplicates = results.Count(r => r.Item2);

        created.Should().Be(1);
        duplicates.Should().Be(4);
    }

    [Fact]
    public async Task StatusTransitions_FollowsValidPath()
    {
        using var dbContext = CreateDbContext();
        var repository = new InboxEventRepository(dbContext);
        var tenantId = Guid.NewGuid();

        var (created, _) = await repository.CreateWithIdempotencyCheckAsync(
            Guid.NewGuid().ToString(), tenantId, "test@example.com", "Test", "Subject", "Body");

        created!.Status.Should().Be(InboxEventStatus.Ingested);

        await repository.UpdateStatusAsync(created.Id, InboxEventStatus.Processing);
        var processing = await repository.GetByIdAsync(created.Id);
        processing!.Status.Should().Be(InboxEventStatus.Processing);

        await repository.UpdateStatusAsync(created.Id, InboxEventStatus.AutoApproved);
        var approved = await repository.GetByIdAsync(created.Id);
        approved!.Status.Should().Be(InboxEventStatus.AutoApproved);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName + Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options, new MockTenantProvider());
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        public Guid GetTenantId() => _tenantId;
    }
}
