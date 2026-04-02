using System.IO;
using System.Threading.Tasks;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services.Security;

public class NoopVirusScanService : IVirusScanService
{
    public Task<bool> IsFileSafeAsync(Stream fileStream, string fileName)
    {
        // By default, assume safe. In production, this would be replaced by ClamAV or similar.
        return Task.FromResult(true);
    }
}
