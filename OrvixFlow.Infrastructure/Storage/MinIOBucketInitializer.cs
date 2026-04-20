using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Infrastructure.Storage;

public class MinIOBucketInitializer : IHostedService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<MinIOBucketInitializer> _logger;

    public MinIOBucketInitializer(IAmazonS3 s3, string bucket, ILogger<MinIOBucketInitializer> logger)
    {
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _bucket);
            if (!exists)
            {
                await _s3.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = _bucket
                }, cancellationToken);

                _logger.LogInformation("MinIO bucket '{Bucket}' created successfully.", _bucket);
                return;
            }

            _logger.LogInformation("MinIO bucket '{Bucket}' already exists.", _bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MinIO bucket '{Bucket}'. API startup aborted.", _bucket);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
