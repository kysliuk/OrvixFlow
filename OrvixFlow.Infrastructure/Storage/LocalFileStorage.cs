using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IConfiguration configuration)
    {
        _basePath = configuration["Storage:Local:BasePath"] ?? "/app/uploads";
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream)
    {
        var tenantDir = Path.Combine(_basePath, tenantId.ToString());
        var docDir = Path.Combine(tenantDir, documentId.ToString());
        
        if (!Directory.Exists(docDir))
        {
            Directory.CreateDirectory(docDir);
        }

        // F-12 FIX: Sanitize filename to prevent path traversal attacks.
        // Path.GetFileName() strips any directory components (e.g., "../../../etc/passwd").
        // Then we remove any remaining invalid characters.
        var safeFileName = Path.GetFileName(fileName);
        safeFileName = string.Concat(safeFileName.Split(Path.GetInvalidFileNameChars()));

        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "unnamed";

        // F-12 FIX: Generate a random internal filename to prevent filename collisions
        // and ensure the stored file name cannot be predicted or controlled by the uploader.
        var extension = Path.GetExtension(safeFileName);
        var storedName = $"{Guid.NewGuid()}{extension}";

        var fullPath = Path.Combine(docDir, storedName);

        // Security: Verify the resolved path is still under the expected base directory.
        // This guards against any remaining path traversal vectors.
        var resolvedPath = Path.GetFullPath(fullPath);
        var baseDir = Path.GetFullPath(_basePath);
        if (!resolvedPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path traversal attempt detected.");
        }

        using var targetStream = File.Create(resolvedPath);
        await fileStream.CopyToAsync(targetStream);

        return resolvedPath;
    }

    public Task<Stream> GetFileAsync(string storagePath)
    {
        // F-12 FIX: Validate the requested path is within the allowed base directory.
        var resolvedPath = Path.GetFullPath(storagePath);
        var baseDir = Path.GetFullPath(_basePath);
        if (!resolvedPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("File not found in local storage", storagePath);
        }

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("File not found in local storage", storagePath);
        }

        return Task.FromResult<Stream>(File.OpenRead(resolvedPath));
    }

    public Task DeleteFileAsync(string storagePath)
    {
        // F-12 FIX: Validate the path is within the allowed base directory before deleting.
        var resolvedPath = Path.GetFullPath(storagePath);
        var baseDir = Path.GetFullPath(_basePath);
        if (!resolvedPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            // Silently ignore deletions outside the base directory.
            // This is intentional — we should not expose whether a file exists outside our storage.
            return Task.CompletedTask;
        }

        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
        }
        return Task.CompletedTask;
    }
}
