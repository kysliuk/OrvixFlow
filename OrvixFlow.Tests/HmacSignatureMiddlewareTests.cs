using System;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace OrvixFlow.Tests;

public class HmacSignatureTests
{
    [Fact]
    public void ComputeHmacSha256_ValidPayload_ReturnsCorrectHash()
    {
        var payload = @"{""MessageId"": ""test-123"", ""SenderEmail"": ""test@example.com""}";
        var base64Secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var hash = ComputeHmacSha256(payload, base64Secret);

        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64);
    }

    [Fact]
    public void ComputeHmacSha256_EmptyPayload_ReturnsHash()
    {
        var payload = "";
        var base64Secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var hash = ComputeHmacSha256(payload, base64Secret);

        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64);
    }

    [Fact]
    public void ComputeHmacSha256_UnicodePayload_ReturnsHash()
    {
        var payload = @"{""name"": ""日本語テスト"", ""emoji"": ""🎉""}";
        var base64Secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var hash = ComputeHmacSha256(payload, base64Secret);

        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64);
    }

    [Fact]
    public void ComputeHmacSha256_SamePayload_SameHash()
    {
        var payload = @"{""test"": ""data""}";
        var base64Secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var hash1 = ComputeHmacSha256(payload, base64Secret);
        var hash2 = ComputeHmacSha256(payload, base64Secret);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHmacSha256_DifferentPayload_DifferentHash()
    {
        var payload1 = @"{""test"": ""data1""}";
        var payload2 = @"{""test"": ""data2""}";
        var base64Secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var hash1 = ComputeHmacSha256(payload1, base64Secret);
        var hash2 = ComputeHmacSha256(payload2, base64Secret);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHmacSha256_DifferentSecret_DifferentHash()
    {
        var payload = @"{""test"": ""data""}";
        var secret1 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
        var secret2 = Convert.ToBase64String(new byte[] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 });

        var hash1 = ComputeHmacSha256(payload, secret1);
        var hash2 = ComputeHmacSha256(payload, secret2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void CryptographicOperations_FixedTimeEquals_TrueMatch()
    {
        var bytes1 = Encoding.UTF8.GetBytes("abc123");
        var bytes2 = Encoding.UTF8.GetBytes("abc123");

        var result = CryptographicOperations.FixedTimeEquals(bytes1, bytes2);

        result.Should().BeTrue();
    }

    [Fact]
    public void CryptographicOperations_FixedTimeEquals_FalseMatch()
    {
        var bytes1 = Encoding.UTF8.GetBytes("abc123");
        var bytes2 = Encoding.UTF8.GetBytes("xyz789");

        var result = CryptographicOperations.FixedTimeEquals(bytes1, bytes2);

        result.Should().BeFalse();
    }

    [Fact]
    public void HashIsLowercaseHex()
    {
        var payload = @"{""test"": ""data""}";
        var base64Secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var hash = ComputeHmacSha256(payload, base64Secret);

        hash.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    private static string ComputeHmacSha256(string payload, string base64Secret)
    {
        var keyBytes = Convert.FromBase64String(base64Secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
