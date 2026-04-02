using System.IO;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IVirusScanService
{
    Task<bool> IsFileSafeAsync(Stream fileStream, string fileName);
}
