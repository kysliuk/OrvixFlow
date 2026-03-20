using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using Xunit;

namespace OrvixFlow.Tests;

public class AgentServiceTests
{
    [Fact]
    public async Task ProcessInternalAsync_Should_Return_Success_With_Message()
    {
        // Arrange
        var mockChatCompletion = new Mock<IChatCompletionService>();
        var mockTenantId = Guid.NewGuid();

        mockChatCompletion.Setup(s => s.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings>(),
            It.IsAny<Kernel>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChatMessageContent(AuthorRole.Assistant, "I am the AI orchestrator!") });

        var builder = Kernel.CreateBuilder();
        // Inject the mock so the Kernel uses it as the default ChatCompletionService
        builder.Services.AddSingleton<IChatCompletionService>(mockChatCompletion.Object);
        var kernel = builder.Build();

        var mockFactory = new Mock<System.Net.Http.IHttpClientFactory>();
        var agentService = new AgentService(kernel, 
            new OrvixFlow.Infrastructure.Ai.Plugins.KnowledgeBaseSearchPlugin(null!, null!),
            new OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin(mockFactory.Object));

        // Act
        var response = await agentService.ProcessInternalAsync("Hello AI", mockTenantId);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Message.Should().Be("I am the AI orchestrator!");
    }

    [Fact]
    public async Task ProcessInternalAsync_Should_Return_Failure_On_Exception()
    {
        // Arrange
        var mockChatCompletion = new Mock<IChatCompletionService>();
        var mockTenantId = Guid.NewGuid();

        mockChatCompletion.Setup(s => s.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings>(),
            It.IsAny<Kernel>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API Quota Exceeded"));

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(mockChatCompletion.Object);
        var kernel = builder.Build();

        var mockFactory = new Mock<System.Net.Http.IHttpClientFactory>();
        var agentService = new AgentService(kernel, 
            new OrvixFlow.Infrastructure.Ai.Plugins.KnowledgeBaseSearchPlugin(null!, null!),
            new OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin(mockFactory.Object));

        // Act
        var response = await agentService.ProcessInternalAsync("Hello AI", mockTenantId);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.ErrorMessage.Should().Contain("API Quota Exceeded");
    }
}
