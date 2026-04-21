using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Storage;

/// <summary>
/// Scans object storage and logs any keys that do not have a matching StoredObject row.
/// Read-only job: it never deletes objects and always requires human review before cleanup.
/// </summary>
public class OrphanDetectionJob
{
    private readonly AppDbContext _db;
    private readonly IAmazonS3? _s3;
    private readonly string? _bucket;
    private readonly ILogger<OrphanDetectionJob> _logger;

    public OrphanDetectionJob(
        IServiceProvider serviceProvider,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<OrphanDetectionJob> logger)
    {
        _db = db;
        _s3 = serviceProvider.GetService<IAmazonS3>();
        _bucket = configuration["Storage:MinIO:Bucket"];
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_s3 == null || string.IsNullOrWhiteSpace(_bucket))
        {
            _logger.LogInformation("Orphan detection skipped because remote object storage is not configured.");
            return;
        }

        _logger.LogInformation("Orphan detection starting for bucket '{Bucket}'.", _bucket);

        var allStoredKeys = await _db.StoredObjects
            .IgnoreQueryFilters()
            .Select(s => s.StorageKey)
            .ToListAsync(cancellationToken);

        var knownKeys = new HashSet<string>(allStoredKeys, StringComparer.Ordinal);
        var orphanCount = 0;
        string? continuationToken = null;

        do
        {
            var response = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                ContinuationToken = continuationToken,
                MaxKeys = 1000
            }, cancellationToken);

            foreach (var storageObject in response.S3Objects)
            {
                if (knownKeys.Contains(storageObject.Key))
                {
                    continue;
                }

                orphanCount++;
                _logger.LogWarning(
                    "ORPHAN: key={Key} size={Size} lastModified={LastModified}. No delete was performed.",
                    storageObject.Key,
                    storageObject.Size,
                    storageObject.LastModified);
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        _logger.LogInformation(
            "Orphan detection complete. Found {Count} orphaned objects. Human review required before cleanup.",
            orphanCount);
    }
}
