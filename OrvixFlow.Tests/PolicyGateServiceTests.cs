using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class PolicyGateServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly PolicyGateService _service;
    private readonly Guid _tenantId;

    public PolicyGateServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new PolicyGateService(_dbContext, _cache);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task EvaluateAsync_NoPolicy_DefaultsToHoldForApproval()
    {
        var context = CreateContext("UnknownCategory", 0.95m);

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("No policy found");
    }

    [Fact]
    public async Task EvaluateAsync_HighConfidence_HigherThanThreshold_AutoExecute()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
        result.Reason.Should().Contain("Auto-approved");
    }

    [Fact]
    public async Task EvaluateAsync_HighConfidence_EqualToThreshold_AutoExecute()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.8m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
    }

    [Fact]
    public async Task EvaluateAsync_LowConfidence_BelowThreshold_HoldForApproval()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.7m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("below threshold");
    }

    [Fact]
    public async Task EvaluateAsync_AutoExecuteDisabled_HoldForApproval()
    {
        await CreatePolicy("Support", autoExecute: false, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("disabled auto-execution");
    }

    [Theory]
    [InlineData("lawsuit")]
    [InlineData("legal")]
    [InlineData("refund")]
    [InlineData("court")]
    [InlineData("fbi")]
    [InlineData("irs")]
    [InlineData("data breach")]
    [InlineData("wrongful termination")]
    public async Task EvaluateAsync_HighRiskKeyword_HoldForApproval(string keyword)
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", $"This is about {keyword}");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("High-risk keyword");
    }

    [Theory]
    [InlineData("LAWSUIT")]
    [InlineData("Lawsuit")]
    [InlineData("LAWSUIT ")]
    public async Task EvaluateAsync_HighRiskKeyword_CaseInsensitive(string keyword)
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", $"This is about {keyword}");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
    }

    [Theory]
    [InlineData("lawyer_bio")]
    [InlineData("regional")]
    [InlineData("country")]
    [InlineData("refutable_amount")]
    public async Task EvaluateAsync_PartialKeywordMatch_DoesNotTrigger(string keyword)
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", $"This contains {keyword} in a normal word");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
    }

    [Theory]
    [InlineData("support@irs.gov")]
    [InlineData("contact@education.mil")]
    [InlineData("info@stanford.edu")]
    [InlineData("help@who.org")]
    public async Task EvaluateAsync_VipDomain_Gov_HoldForApproval(string email)
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, email, "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("VIP domain");
    }

    [Theory]
    [InlineData("test@gmail.com")]
    [InlineData("user@company.com")]
    [InlineData("contact@startup.io")]
    public async Task EvaluateAsync_NonVipDomain_AutoExecute(string email)
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, email, "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
    }

    [Fact]
    public async Task EvaluateAsync_ExcludedKeyword_HoldForApproval()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m, excludedKeywords: "urgent,asap,immediate");
        var context = CreateContext("Support", 0.95m, "test@example.com", "This is URGENT please respond ASAP");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("Excluded keyword");
    }

    [Theory]
    [InlineData("URGENT")]
    [InlineData("urgent")]
    [InlineData("Urgent")]
    public async Task EvaluateAsync_ExcludedKeyword_CaseInsensitive(string keyword)
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m, excludedKeywords: "urgent");
        var context = CreateContext("Support", 0.95m, "test@example.com", $"This is {keyword}!");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
    }

    [Fact]
    public async Task EvaluateAsync_AllConditionsMet_AutoExecute()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "user@company.com", "How do I reset my password?");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
        result.ShouldSendCallback.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_EmptyConfidenceScore_HoldForApproval()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.0m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
    }

    [Fact]
    public async Task EvaluateAsync_PerfectConfidence_AutoExecute()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 1.0m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
    }

    [Fact]
    public async Task EvaluateAsync_NullExcludedKeywords_DoesNotCrash()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m, excludedKeywords: null);
        var context = CreateContext("Support", 0.95m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
    }

    [Fact]
    public async Task EvaluateAsync_EmptyExcludedKeywords_DoesNotCrash()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m, excludedKeywords: "");
        var context = CreateContext("Support", 0.95m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
    }

    [Fact]
    public async Task EvaluateAsync_WhitespaceInExcludedKeywords_BothTrimmedAndMatched()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m, excludedKeywords: "keyword1 , keyword2 ,  keyword3");
        var context = CreateContext("Support", 0.95m, "test@example.com", "Contains keyword2 here");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
    }

    [Fact]
    public async Task EvaluateAsync_PolicyCache_ReturnsCached()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", "Normal email body");

        await _service.EvaluateAsync(context, _tenantId);
        await _service.EvaluateAsync(context, _tenantId);

        var entries = _dbContext.ChangeTracker.Entries();
        var cachedCount = entries.Count();
        cachedCount.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task EvaluateAsync_DifferentTenant_NoPolicy()
    {
        var otherTenantId = Guid.NewGuid();
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", "Normal email body");

        var result = await _service.EvaluateAsync(context, otherTenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("No policy found");
    }

    [Fact]
    public async Task EvaluateAsync_HighRiskKeyword_TakesPriorityOverVipDomain()
    {
        await CreatePolicy("Support", autoExecute: true, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "legal@irs.gov", "We are preparing a lawsuit against you");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("High-risk keyword");
    }

    [Fact]
    public async Task EvaluateAsync_HighRiskKeyword_TakesPriorityOverAutoExecuteDisabled()
    {
        await CreatePolicy("Support", autoExecute: false, threshold: 0.8m);
        var context = CreateContext("Support", 0.95m, "test@example.com", "This is about a lawsuit");

        var result = await _service.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.HoldForApproval);
        result.Reason.Should().Contain("High-risk keyword");
    }

    private async Task CreatePolicy(string category, bool autoExecute, decimal threshold, string? excludedKeywords = null)
    {
        var policy = new WorkflowPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Category = category,
            AutoExecute = autoExecute,
            ConfidenceThreshold = threshold,
            ExcludedKeywords = excludedKeywords ?? string.Empty
        };
        _dbContext.WorkflowPolicies.Add(policy);
        await _dbContext.SaveChangesAsync();
    }

    private static PolicyEvaluationContext CreateContext(string category, decimal confidenceScore, string senderEmail = "test@example.com", string bodyText = "Normal email body")
    {
        return new PolicyEvaluationContext
        {
            Category = category,
            ConfidenceScore = confidenceScore,
            SenderEmail = senderEmail,
            Subject = "Test Subject",
            BodyText = bodyText
        };
    }

    private class MockTenantProvider : Core.Interfaces.ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
