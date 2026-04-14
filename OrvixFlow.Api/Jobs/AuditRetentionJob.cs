using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Jobs;

public class AuditRetentionJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditRetentionJob> _logger;

    public AuditRetentionJob(AppDbContext db, ILogger<AuditRetentionJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting audit retention job...");

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var deleted = 0;

        try
        {
            var oldRecords = await _db.AuditTrails
                .IgnoreQueryFilters()
                .Where(a => a.Timestamp < cutoff)
                .ToListAsync();

            foreach (var record in oldRecords)
            {
                _db.AuditTrails.Remove(record);
                deleted++;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Audit retention job complete: deleted {Count} records older than 90 days", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit retention job failed");
            throw;
        }
    }
}