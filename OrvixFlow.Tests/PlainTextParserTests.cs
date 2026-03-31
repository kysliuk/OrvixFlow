using System.IO;
using System.Text;
using System.Threading.Tasks;
using OrvixFlow.Infrastructure.Ai.Parsers;
using Xunit;

namespace OrvixFlow.Tests;

public class PlainTextParserTests
{
    [Fact]
    public async Task ParseAsync_SimpleText_ReturnsContent()
    {
        // Arrange
        var parser = new PlainTextParser();
        var content = "Hello world! This is a test file.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await parser.ParseAsync(stream, "test.txt");

        // Assert
        Assert.Equal("test.txt", result.Title);
        Assert.Single(result.TextChunks);
        Assert.Equal(content, result.TextChunks[0].Content);
    }

    [Fact]
    public void CanParse_TxtAndHtml_ReturnsTrue()
    {
        var parser = new PlainTextParser();
        Assert.True(parser.CanParse("text/plain"));
        Assert.True(parser.CanParse("text/html"));
        Assert.False(parser.CanParse("application/pdf"));
    }
}
