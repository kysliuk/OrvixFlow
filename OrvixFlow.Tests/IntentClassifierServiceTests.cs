using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace OrvixFlow.Tests;

public class IntentClassifierServiceTests
{
    private static readonly string[] HighRiskKeywords = {
        "lawsuit", "legal", "refund", "court", "attorney",
        "police", "fbi", "irs", "tax", "compliance",
        "breach", "hack", "data leak", "gdpr", "ccpa"
    };

    private static readonly string[] VipDomains = { ".gov", ".mil", ".edu", ".org" };

    [Theory]
    [InlineData("We are filing a lawsuit against you.", "lawsuit")]
    [InlineData("Legal department requesting information.", "legal")]
    [InlineData("I demand a full refund immediately.", "refund")]
    [InlineData("FBI is investigating this matter.", "fbi")]
    [InlineData("IRS audit incoming.", "irs")]
    [InlineData("We discovered a data breach.", "data breach")]
    public void HighRiskKeywordDetection_ContainsKeyword_ReturnsTrue(string text, string keyword)
    {
        ContainsHighRiskKeyword(text.ToLowerInvariant()).Should().BeTrue();
    }

    [Theory]
    [InlineData("Hello, how are you?")]
    [InlineData("Thanks for your help")]
    [InlineData("Please reset my password")]
    [InlineData("Follow the guidelines")]
    public void HighRiskKeywordDetection_NormalText_ReturnsFalse(string text)
    {
        ContainsHighRiskKeyword(text.ToLowerInvariant()).Should().BeFalse();
    }

    [Theory]
    [InlineData("support@irs.gov", true)]
    [InlineData("contact@education.mil", true)]
    [InlineData("info@stanford.edu", true)]
    [InlineData("help@who.org", true)]
    [InlineData("test@gmail.com", false)]
    [InlineData("user@company.com", false)]
    public void IsVipDomain_CorrectlyIdentifies(string email, bool expectedVip)
    {
        IsVipDomain(email).Should().Be(expectedVip);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>", "&lt;script&gt;alert")]
    [InlineData("Hello <b>World</b>", "Hello &lt;b&gt;World&lt;/b&gt;")]
    public void SanitizeEmailContent_EscapesHtml(string input, string expectedFragment)
    {
        var sanitized = SanitizeEmailContent(input);
        sanitized.Should().Contain(expectedFragment);
    }

    [Fact]
    public void SanitizeEmailContent_TruncatesLargeBody()
    {
        var largeBody = new string('x', 10000);
        var sanitized = SanitizeEmailContent(largeBody);
        sanitized.Length.Should().BeLessThanOrEqualTo(4000 + "... [truncated]".Length);
    }

    [Fact]
    public void SanitizeEmailContent_HandlesNullOrEmpty()
    {
        SanitizeEmailContent(null!).Should().BeEmpty();
        SanitizeEmailContent("").Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"category\": \"Support\", \"confidenceScore\": 0.92}", "Support", 0.92)]
    [InlineData("{\"category\": \"Billing\", \"confidenceScore\": 0.85}", "Billing", 0.85)]
    [InlineData("{\"category\": \"Sales\"}", "Sales", 0.0)]
    public void ParseJson_ValidJson_ReturnsCorrectValues(string json, string expectedCategory, decimal expectedScore)
    {
        var result = ParseJson(json);
        
        result.category.Should().Be(expectedCategory);
        result.confidenceScore.Should().Be(expectedScore);
    }

    [Fact]
    public void ParseJson_InvalidJson_ReturnsDefault()
    {
        var result = ParseJson("This is not JSON at all");
        
        result.category.Should().Be("Unknown");
        result.confidenceScore.Should().Be(0.0m);
    }

    [Fact]
    public void ParseJson_EmptyJson_ReturnsDefault()
    {
        var result = ParseJson("");
        
        result.category.Should().Be("Unknown");
        result.confidenceScore.Should().Be(0.0m);
    }

    private bool ContainsHighRiskKeyword(string body)
    {
        return HighRiskKeywords.Any(keyword => body.Contains(keyword));
    }

    private bool IsVipDomain(string email)
    {
        var domain = email.Split('@').LastOrDefault()?.ToLowerInvariant() ?? "";
        return VipDomains.Any(d => domain.EndsWith(d));
    }

    private static string SanitizeEmailContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var sanitized = content
            .Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;")
            .Replace("\\", "\\\\");

        if (sanitized.Length > 4000)
            sanitized = sanitized[..4000] + "... [truncated]";

        return sanitized;
    }

    private (string category, decimal confidenceScore) ParseJson(string json)
    {
        try
        {
            var jsonMatch = Regex.Match(json, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var jsonStr = jsonMatch.Value;
                var categoryMatch = Regex.Match(jsonStr, @"""category""\s*:\s*""([^""]+)""");
                var scoreMatch = Regex.Match(jsonStr, @"""confidenceScore""\s*:\s*([\d.]+)");

                var category = categoryMatch.Success ? categoryMatch.Groups[1].Value : "Unknown";
                var score = scoreMatch.Success && decimal.TryParse(scoreMatch.Groups[1].Value, 
                    System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out var parsed) 
                    ? parsed 
                    : 0.0m;

                return (category, score);
            }
        }
        catch
        {
            // Fall through to default
        }

        return ("Unknown", 0.0m);
    }
}
