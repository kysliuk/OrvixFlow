using FluentAssertions;
using OrvixFlow.Infrastructure.Services;

namespace OrvixFlow.Tests;

public class MockEmailServiceTests
{
    [Fact]
    public void TryExtractAuthLink_ReturnsVerifyLink_WhenPresentInHtml()
    {
        var body = "<a href='http://localhost:3000/verify?token=verify-token-123'>Verify</a>";

        var link = MockEmailService.TryExtractAuthLink(body);

        link.Should().Be("http://localhost:3000/verify?token=verify-token-123");
    }

    [Fact]
    public void TryExtractAuthLink_ReturnsInviteLink_WhenPresentInHtml()
    {
        var body = "<p>Fallback: http://localhost:3000/invite?token=invite-token-456</p>";

        var link = MockEmailService.TryExtractAuthLink(body);

        link.Should().Be("http://localhost:3000/invite?token=invite-token-456");
    }

    [Fact]
    public void TryExtractAuthLink_ReturnsNull_WhenNoAuthLinkExists()
    {
        var link = MockEmailService.TryExtractAuthLink("<p>No auth link here</p>");

        link.Should().BeNull();
    }
}
