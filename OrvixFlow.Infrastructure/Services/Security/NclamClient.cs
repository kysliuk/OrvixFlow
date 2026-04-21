using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nClam;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services.Security;

public class NclamClient : IClamAvClient
{
    private readonly ClamClient _clamClient;
    private readonly ILogger<NclamClient> _logger;

    public NclamClient(IOptions<ClamAvOptions> options, ILogger<NclamClient> logger)
    {
        var settings = options.Value;
        _clamClient = new ClamClient(settings.Host, settings.Port);
        _logger = logger;
    }

    public async Task<VirusScanResult> ScanAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _clamClient.SendAndScanFileAsync(stream, cancellationToken);

            return result.Result switch
            {
                ClamScanResults.Clean => VirusScanResult.Clean,
                ClamScanResults.VirusDetected => VirusScanResult.Infected,
                _ => VirusScanResult.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClamAV scan failed");
            return VirusScanResult.Error;
        }
    }
}

public class ClamAvOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public int ConnectTimeoutSeconds { get; set; } = 30;
    public int ReadWriteTimeoutSeconds { get; set; } = 60;
}
