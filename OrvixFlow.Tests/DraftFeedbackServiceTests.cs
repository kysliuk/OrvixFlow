using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class DraftFeedbackServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly DraftFeedbackService _service;
    private readonly Guid _tenantId;

    public DraftFeedbackServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
        _service = new DraftFeedbackService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task RecordFeedbackAsync_SavesFeedback_WithEditDistance()
    {
        var actionRequestId = Guid.NewGuid();
        var original = "Hello, thank you for reaching out.";
        var final = "Hello, thank you for reaching out. We appreciate your business.";

        var feedback = await _service.RecordFeedbackAsync(_tenantId, actionRequestId, original, final);

        feedback.Id.Should().NotBeEmpty();
        feedback.TenantId.Should().Be(_tenantId);
        feedback.ActionRequestId.Should().Be(actionRequestId);
        feedback.OriginalDraft.Should().Be(original);
        feedback.FinalHumanDraft.Should().Be(final);
        feedback.EditDistance.Should().BeGreaterThan(0);
        feedback.EditDistance.Should().BeLessThan(1);
    }

    [Fact]
    public async Task RecordFeedbackAsync_IdenticalText_ReturnsZeroEditDistance()
    {
        var text = "Same text";

        var feedback = await _service.RecordFeedbackAsync(_tenantId, Guid.NewGuid(), text, text);

        feedback.EditDistance.Should().Be(0);
    }

    [Fact]
    public async Task RecordFeedbackAsync_CompletelyDifferentText_ReturnsHighEditDistance()
    {
        var feedback = await _service.RecordFeedbackAsync(_tenantId, Guid.NewGuid(), "abc", "xyz");

        feedback.EditDistance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordFeedbackAsync_PersistsToDatabase()
    {
        var actionRequestId = Guid.NewGuid();
        await _service.RecordFeedbackAsync(_tenantId, actionRequestId, "original", "final");

        var count = await _dbContext.DraftFeedbacks.CountAsync();
        count.Should().Be(1);

        var saved = await _dbContext.DraftFeedbacks.FirstAsync();
        saved.ActionRequestId.Should().Be(actionRequestId);
        saved.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task CalculateEditDistanceAsync_EmptyStrings_ReturnsZero()
    {
        var distance = await _service.CalculateEditDistanceAsync("", "");
        distance.Should().Be(0);
    }

    [Fact]
    public async Task CalculateEditDistanceAsync_OneEmptyString_ReturnsOne()
    {
        var distance = await _service.CalculateEditDistanceAsync("", "hello");
        distance.Should().Be(1);
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
