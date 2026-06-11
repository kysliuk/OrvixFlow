using System;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Moq;
using OrvixFlow.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace OrvixFlow.Tests;

public class MailboxCredentialEncryptionServiceTests
{
    private readonly string _validBase64Key;

    public MailboxCredentialEncryptionServiceTests()
    {
        byte[] keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        _validBase64Key = Convert.ToBase64String(keyBytes);
    }

    [Fact]
    public void Constructor_WithMissingKey_ThrowsInvalidOperationException()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["MAILBOX_CREDENTIAL_ENCRYPTION_KEY"]).Returns((string?)null);

        var act = () => new MailboxCredentialEncryptionService(mockConfig.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MAILBOX_CREDENTIAL_ENCRYPTION_KEY*");
    }

    [Fact]
    public void Constructor_WithInvalidKeyLength_ThrowsInvalidOperationException()
    {
        byte[] keyBytes = new byte[16];
        RandomNumberGenerator.Fill(keyBytes);
        var invalidKey = Convert.ToBase64String(keyBytes);

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["MAILBOX_CREDENTIAL_ENCRYPTION_KEY"]).Returns(invalidKey);

        var act = () => new MailboxCredentialEncryptionService(mockConfig.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*derive a 256-bit*");
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginalString()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["MAILBOX_CREDENTIAL_ENCRYPTION_KEY"]).Returns(_validBase64Key);

        var service = new MailboxCredentialEncryptionService(mockConfig.Object);
        var originalText = "super-secret-oauth-token-123456";

        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void Encrypt_CalledTwice_ProducesDifferentCiphertext()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["MAILBOX_CREDENTIAL_ENCRYPTION_KEY"]).Returns(_validBase64Key);

        var service = new MailboxCredentialEncryptionService(mockConfig.Object);
        var originalText = "super-secret-oauth-token-123456";

        var encrypted1 = service.Encrypt(originalText);
        var encrypted2 = service.Encrypt(originalText);

        encrypted1.Should().NotBe(encrypted2);
    }

    [Fact]
    public void Decrypt_WithTamperedPayload_ThrowsCryptographicException()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["MAILBOX_CREDENTIAL_ENCRYPTION_KEY"]).Returns(_validBase64Key);

        var service = new MailboxCredentialEncryptionService(mockConfig.Object);
        var originalText = "super-secret-oauth-token-123456";

        var encrypted = service.Encrypt(originalText);
        byte[] bytes = Convert.FromBase64String(encrypted);

        bytes[bytes.Length - 1] ^= 0x01;

        var tamperedEncrypted = Convert.ToBase64String(bytes);

        var act = () => service.Decrypt(tamperedEncrypted);

        act.Should().Throw<CryptographicException>();
    }
}
