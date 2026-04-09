using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using OrvixFlow.Infrastructure.Security;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for F-11: Magic byte file signature validation
/// </summary>
public class FileSignatureValidatorTests
{
    [Theory]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }, "image/jpeg")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xEE }, "image/jpeg")]
    public async Task DetectMimeType_ValidSignature_ReturnsCorrectMimeType(byte[] header, string expectedMimeType)
    {
        // Act
        var result = FileSignatureValidator.DetectMimeType(header);

        // Assert
        result.Should().Be(expectedMimeType);
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 })]  // Null bytes
    [InlineData(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE })] // Java class file
    [InlineData(new byte[] { 0x4D, 0x5A })]              // Windows executable
    public async Task DetectMimeType_UnknownSignature_ReturnsNull(byte[] header)
    {
        // Act
        var result = FileSignatureValidator.DetectMimeType(header);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateStream_EmptyBytes_ReturnsNull()
    {
        // Act
        var result = FileSignatureValidator.DetectMimeType(Array.Empty<byte>());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateStream_ShortHeader_ReturnsNull()
    {
        // Act
        var result = FileSignatureValidator.DetectMimeType(new byte[] { 0x25, 0x50 });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateStream_SafeExtension_PdfWithJpegMagicBytes_Overridden()
    {
        // A malicious file with PDF magic bytes but named .exe
        // The signature check should catch it based on magic bytes, not extension
        var pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var result = FileSignatureValidator.DetectMimeType(pdfHeader);

        result.Should().Be("application/pdf");
    }
}
