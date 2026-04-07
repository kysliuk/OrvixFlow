using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services.Security;

public class ClamAvVirusScanService : IVirusScanService
{
    private readonly IClamAvClient _clamClient;
    private readonly ILogger<ClamAvVirusScanService> _logger;

    public ClamAvVirusScanService(IClamAvClient clamClient, ILogger<ClamAvVirusScanService> logger)
    {
        _clamClient = clamClient;
        _logger = logger;
    }

    public async Task<bool> IsFileSafeAsync(Stream fileStream, string fileName)
    {
        try
        {
            fileStream.Position = 0;
            var result = await _clamClient.ScanAsync(fileStream, CancellationToken.None);

            return result switch
            {
                VirusScanResult.Clean => true,
                VirusScanResult.Infected => false,
                VirusScanResult.Error => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virus scan failed for {FileName}, rejecting file", fileName);
            return false;
        }
    }
}
