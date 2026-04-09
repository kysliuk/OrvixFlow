using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OrvixFlow.Infrastructure.Security;

/// <summary>
/// Validates file types by inspecting magic bytes (file signatures).
/// F-11 Fix: Replaces client-supplied Content-Type header validation
/// with actual file content inspection to prevent MIME type spoofing.
/// </summary>
public static class FileSignatureValidator
{
    /// <summary>
    /// Maps MIME types to their magic byte signatures.
    /// Signatures are compared from the start of the file.
    /// </summary>
    private static readonly Dictionary<string, List<byte[]>> _signatures = new()
    {
        // PDF: %PDF-1.4
        { "application/pdf", new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46 } } },

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        { "image/png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },

        // JPEG: FF D8 FF
        { "image/jpeg", new List<byte[]>
            {
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, // JFIF
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, // Exif
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 }, // SPIFF
                new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }, // Canon
                new byte[] { 0xFF, 0xD8, 0xFF, 0xEE }, // Adobe
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 }, // ICC
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 }, // Samsung
            }
        },

        // Plain text: no magic bytes (always allowed as fallback)
        { "text/plain", new List<byte[]> { Array.Empty<byte>() } },
    };

    private const int MaxHeaderBytes = 16;

    /// <summary>
    /// Detects the MIME type by inspecting the file's magic bytes.
    /// Returns null if the file signature is unknown or unsupported.
    /// </summary>
    /// <param name="header">The first bytes of the file (at least 4 bytes recommended).</param>
    /// <returns>The detected MIME type or null.</returns>
    public static string? DetectMimeType(byte[] header)
    {
        if (header == null || header.Length < 4)
            return null;

        foreach (var kvp in _signatures)
        {
            // text/plain has no magic bytes - it matches any content
            if (kvp.Value.Count == 1 && kvp.Value[0].Length == 0)
                continue;

            foreach (var signature in kvp.Value)
            {
                if (signature.Length == 0)
                    continue;

                if (header.Length >= signature.Length)
                {
                    bool matches = true;
                    for (int i = 0; i < signature.Length; i++)
                    {
                        if (header[i] != signature[i])
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                        return kvp.Key;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the first bytes from a stream and detects the MIME type.
    /// Does NOT consume the stream — resets position after reading.
    /// </summary>
    /// <param name="stream">The file stream (must be seekable).</param>
    /// <returns>The detected MIME type or null.</returns>
    public static string? DetectMimeTypeFromStream(Stream stream)
    {
        if (!stream.CanSeek)
            return null;

        var originalPosition = stream.Position;
        var header = new byte[MaxHeaderBytes];
        var bytesRead = stream.Read(header, 0, MaxHeaderBytes);
        stream.Position = originalPosition;

        if (bytesRead < 4)
            return null;

        // Trim to actually read bytes
        var actualHeader = new byte[bytesRead];
        Array.Copy(header, actualHeader, bytesRead);
        return DetectMimeType(actualHeader);
    }

    /// <summary>
    /// Allowed MIME types for file upload (configurable).
    /// </summary>
    private static readonly string[] AllowedMimeTypes = new[]
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "text/plain"
    };

    /// <summary>
    /// Validates that the detected MIME type is in the allowlist.
    /// </summary>
    /// <param name="mimeType">The MIME type to validate.</param>
    /// <returns>True if the type is allowed.</returns>
    public static bool IsAllowedMimeType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return false;

        foreach (var allowed in AllowedMimeTypes)
        {
            if (string.Equals(allowed, mimeType, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
