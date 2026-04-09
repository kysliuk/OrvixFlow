using System;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for F-16: AutomationKey constant-time comparison
/// Verifies that timing attacks are prevented by using FixedTimeEquals.
/// </summary>
public class AutomationKeySecurityTests
{
    [Fact]
    public void FixedTimeEquals_ValidKey_ReturnsTrue()
    {
        // Arrange
        var configuredKey = "super-secret-n8n-dev-key";
        var providedKey = "super-secret-n8n-dev-key";
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        // Act
        var result = CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FixedTimeEquals_DifferentKeys_ReturnsFalse()
    {
        // Arrange
        var configuredKey = "super-secret-n8n-dev-key";
        var wrongKey = "wrong-key-attempt-12345";
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var wrongBytes = Encoding.UTF8.GetBytes(wrongKey);

        // Act
        var result = CryptographicOperations.FixedTimeEquals(configuredBytes, wrongBytes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FixedTimeEquals_DifferentLengthKeys_ReturnsFalse()
    {
        // Arrange - keys of different lengths should not match
        var configuredKey = "short";
        var longerKey = "this-key-is-much-longer";
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var longerBytes = Encoding.UTF8.GetBytes(longerKey);

        // Act
        var result = CryptographicOperations.FixedTimeEquals(configuredBytes, longerBytes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FixedTimeEquals_SingleCharDifference_ReturnsFalse()
    {
        // Arrange - a single character difference should be detected
        var correctKey = "super-secret-n8n-dev-key";
        var almostCorrect = "super-secret-n8n-dev-keY"; // Last char different
        var correctBytes = Encoding.UTF8.GetBytes(correctKey);
        var almostBytes = Encoding.UTF8.GetBytes(almostCorrect);

        // Act
        var result = CryptographicOperations.FixedTimeEquals(correctBytes, almostBytes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FixedTimeEquals_EmptyVsEmpty_ReturnsTrue()
    {
        // Arrange
        var emptyBytes = Encoding.UTF8.GetBytes(string.Empty);

        // Act
        var result = CryptographicOperations.FixedTimeEquals(emptyBytes, emptyBytes);

        // Assert
        result.Should().BeTrue();
    }
}
