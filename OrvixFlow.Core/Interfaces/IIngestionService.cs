using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IIngestionService
{
    Task IngestTextAsync(string content);
}
