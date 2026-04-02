using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrvixFlow.Tests;

public class RagPipelineIntegrationTests
{
    private readonly Mock<IIntentClassifierService> _mockClassifier = new();
    private readonly Mock<IHybridVectorSearchService> _mockSearch = new();
    private readonly Mock<IDraftGeneratorService> _mockDraftGen = new();
    private readonly Mock<IRagMetricsCollector> _mockMetrics = new();
    private readonly Mock<ILogger<RagEmailService>> _mockLogger = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task ProcessRagEmailAsync_FullFlow_ReturnsCorrectPayload()
    {
        // Arrange
        var messageId = "msg_123";
        var sender = "test@example.com";
        var subject = "Help me";
        var body = "I need pricing info";

        _mockClassifier.Setup(m => m.ClassifyEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new EmailClassification { Category = "Support", ConfidenceScore = 0.9m });

        _mockSearch.Setup(m => m.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<KnowledgeSnippet> 
            { 
                new KnowledgeSnippet { Content = "Pricing is $10" } 
            });

        _mockDraftGen.Setup(m => m.GenerateDraftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailClassification>(), It.IsAny<List<KnowledgeSnippet>>(), It.IsAny<OrvixFlow.Core.Entities.AgentPersona?>()))
            .ReturnsAsync(new EmailDraftResult("The price is $10", false, new List<KnowledgeImageRef>(), 0.9f));

        var service = new RagEmailService(
            _mockClassifier.Object,
            _mockSearch.Object,
            _mockDraftGen.Object,
            _mockMetrics.Object,
            _mockLogger.Object);

        // Act
        var result = await service.ProcessRagEmailAsync(sender, subject, body, _tenantId, messageId);

        // Assert
        result.Should().NotBeNull();
        result.Email.BodyText.Should().Be("The price is $10");
        result.Action.Should().Be("draft_ready");
        result.Rag.SnippetsUsed.Should().Be(1);
        
        _mockMetrics.Verify(m => m.RecordRetrievalMetricsAsync(
            _tenantId, 
            It.IsAny<Guid>(), 
            1, 
            0, 
            It.IsAny<double>(), 
            "gpt-4o"), Times.Once);
    }
}
