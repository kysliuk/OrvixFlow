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

        var fullPath = Path.Combine(docDir, fileName);
        using var targetStream = File.Create(fullPath);
        await fileStream.CopyToAsync(targetStream);

        return fullPath;
    }

    public Task<Stream> GetFileAsync(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException("File not found in local storage", storagePath);
        }

        return Task.FromResult<Stream>(File.OpenRead(storagePath));
    }

    public Task DeleteFileAsync(string storagePath)
    {
        if (File.Exists(storagePath))
        {
            File.Delete(storagePath);
        }
        return Task.CompletedTask;
    }
}
