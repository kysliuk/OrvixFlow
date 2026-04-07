namespace OrvixFlow.Core.Interfaces;

public enum VirusScanResult
{
    Clean,
    Infected,
    Error
}

public interface IClamAvClient
{
    Task<VirusScanResult> ScanAsync(Stream stream, CancellationToken cancellationToken = default);
}
