using System;
using System.IO;
using System.Threading.Tasks;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Core.Interfaces;

public interface IFileStorage
{
    Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream);
    Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream);
    Task<Stream> GetFileAsync(string storagePath);
    Task DeleteFileAsync(string storagePath);
}
