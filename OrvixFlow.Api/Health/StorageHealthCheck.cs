using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Api.Health;

public class StorageHealthCheck : IHealthCheck
{
    private readonly IAmazonS3? _s3;
    private readonly string _provider;
    private readonly string? _bucket;
    private readonly ILogger<StorageHealthCheck> _logger;

    public StorageHealthCheck(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<StorageHealthCheck> logger)
    {
        _s3 = serviceProvider.GetService<IAmazonS3>();
        _provider = configuration["Storage:Provider"] ?? "Local";
        _bucket = configuration["Storage:MinIO:Bucket"];
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(_provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("Storage provider is Local (no remote check required).");
        }

        if (_s3 == null || string.IsNullOrWhiteSpace(_bucket))
        {
            return HealthCheckResult.Unhealthy("Remote storage is configured but not fully initialized.");
        }

        try
        {
            await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                MaxKeys = 1
            }, cancellationToken);

            return HealthCheckResult.Healthy($"MinIO bucket '{_bucket}' is accessible.");
        }
        catch (AmazonS3Exception ex) when (string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.Ordinal))
        {
            return HealthCheckResult.Unhealthy($"MinIO bucket '{_bucket}' does not exist.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health check failed.");
            return HealthCheckResult.Unhealthy("MinIO unreachable.", ex);
        }
    }
}
