using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Ai;
using Xunit;

namespace OrvixFlow.Tests;

public class RagEmailServiceTests
{
    private readonly Mock<IIntentClassifierService> _classifierMock;
    private readonly Mock<IHybridVectorSearchService> _vectorSearchMock;
    private readonly Mock<IDraftGeneratorService> _draftGeneratorMock;
    private readonly Mock<IRagMetricsCollector> _metricsMock;
    private readonly Mock<ILogger<RagEmailService>> _loggerMock;
    private readonly RagEmailService _service;

    public RagEmailServiceTests()
    {
        _classifierMock = new Mock<IIntentClassifierService>();
        _vectorSearchMock = new Mock<IHybridVectorSearchService>();
        _draftGeneratorMock = new Mock<IDraftGeneratorService>();
        _metricsMock = new Mock<IRagMetricsCollector>();
        _loggerMock = new Mock<ILogger<RagEmailService>>();

        _service = new RagEmailService(
            _classifierMock.Object,
            _vectorSearchMock.Object,
            _draftGeneratorMock.Object,
            _metricsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessRagEmailAsync_ValidRequest_ReturnsStructuredPayload()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var messageId = "msg_123";
        var sender = "test@example.com";
        var subject = "Help";
        var body = "I need help with my account.";

        var classification = new EmailClassification
        {
            Category = "Support",
            ConfidenceScore = 0.95m,
            RequiresHumanReview = false
        };

        var context = new List<KnowledgeSnippet>
        {
            new KnowledgeSnippet { Content = "Use the settings page.", SimilarityScore = 0.9f }
        };

        var draftResult = new EmailDraftResult(
            "Hello, use the settings page.",
            false,
            new List<KnowledgeImageRef>()
        );

        _classifierMock.Setup(x => x.ClassifyEmailAsync(sender, subject, body, It.IsAny<string>()))
            .ReturnsAsync(classification);

        _vectorSearchMock.Setup(x => x.SearchAsync(It.IsAny<string>(), 5))
            .ReturnsAsync(context);

        _draftGeneratorMock.Setup(x => x.GenerateDraftAsync(sender, subject, body, classification, context, null))
            .ReturnsAsync(draftResult);

        // Act
        var result = await _service.ProcessRagEmailAsync(sender, subject, body, tenantId, messageId);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
        result.MessageId.Should().Be(messageId);
        result.Action.Should().Be("draft_ready");
        result.Email.BodyText.Should().Be(draftResult.DraftBody);
        result.Classification.Category.Should().Be("Support");
        result.Rag.SnippetsUsed.Should().Be(1);
        result.Flags.AutoSendAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRagEmailAsync_InsufficientContext_SetsActionToInsufficientContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var classification = new EmailClassification { Category = "Support", ConfidenceScore = 0.8m };
        var context = new List<KnowledgeSnippet>();
        var draftResult = new EmailDraftResult("I don't know.", true, new List<KnowledgeImageRef>());

        _classifierMock.Setup(x => x.ClassifyEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(classification);

        _vectorSearchMock.Setup(x => x.SearchAsync(It.IsAny<string>(), 5))
            .ReturnsAsync(context);

        _draftGeneratorMock.Setup(x => x.GenerateDraftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), classification, context, null))
            .ReturnsAsync(draftResult);

        // Act
        var result = await _service.ProcessRagEmailAsync("a@b.com", "Hi", "Body", tenantId, "m1");

        // Assert
        result.Action.Should().Be("insufficient_context");
        result.Flags.AutoSendAllowed.Should().BeFalse();
        result.Flags.HumanReviewRequired.Should().BeTrue();
    }
}
