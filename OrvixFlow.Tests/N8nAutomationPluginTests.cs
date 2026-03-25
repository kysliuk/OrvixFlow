using System.Net;
using Microsoft.SemanticKernel;
using Moq;
using OrvixFlow.Infrastructure.Ai.Plugins;
using Moq.Protected;
using FluentAssertions;

namespace OrvixFlow.Tests;

public class N8nAutomationPluginTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly N8nAutomationPlugin _plugin;

    public N8nAutomationPluginTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5678")
        };

        _httpClientFactoryMock.Setup(f => f.CreateClient("n8n")).Returns(httpClient);
        _plugin = new N8nAutomationPlugin(_httpClientFactoryMock.Object);
    }

    [Theory]
    [InlineData("valid-path")]
    [InlineData("another_valid_123")]
    [InlineData("DRAFT-REPLY")]
    public async Task TriggerWorkflowAsync_WithValidPath_ShouldProceedToHttpCall(string validPath)
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}")
            });

        // Act
        var result = await _plugin.TriggerWorkflowAsync(validPath, "test message");

        // Assert
        result.Should().Contain("successfully triggered");
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith($"/webhook-test/{validPath}")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("../malicious")]
    [InlineData("path/with/slash")]
    [InlineData("path with space")]
    [InlineData("http://evil.com")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..\\traversal")]
    [InlineData("path;injection")]
    public async Task TriggerWorkflowAsync_WithInvalidPath_ShouldReturnErrorMessage(string invalidPath)
    {
        // Act
        var result = await _plugin.TriggerWorkflowAsync(invalidPath, "test message");

        // Assert
        result.Should().Be("Error: Invalid webhook path. Only alphanumeric characters, underscores, and hyphens are allowed.");

        // Ensure no HTTP call was made
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
