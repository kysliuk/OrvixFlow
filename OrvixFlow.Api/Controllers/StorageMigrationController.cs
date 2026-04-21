using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Storage;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/admin/storage-migration")]
[Authorize(Policy = "SuperAdminOnly")]
public class StorageMigrationController : ControllerBase
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly AppDbContext _db;

    public StorageMigrationController(IBackgroundJobClient backgroundJobClient, AppDbContext db)
    {
        _backgroundJobClient = backgroundJobClient;
        _db = db;
    }

    [HttpPost("dry-run")]
    public IActionResult StartDryRun()
    {
        _backgroundJobClient.Enqueue<LocalToMinioMigrationJob>(job => job.RunAsync(true, default));
        return Accepted(new { message = "Dry run started. Check Hangfire for progress." });
    }

    [HttpPost("run")]
    public IActionResult StartMigration()
    {
        _backgroundJobClient.Enqueue<LocalToMinioMigrationJob>(job => job.RunAsync(false, default));
        return Accepted(new { message = "Migration started. Check Hangfire for progress." });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var documentsNeedingMigration = await _db.KnowledgeBaseDocuments
            .IgnoreQueryFilters()
            .CountAsync(d => !string.IsNullOrWhiteSpace(d.StoragePath) && d.StoragePath.StartsWith("/"));

        var imagesNeedingMigration = await _db.KnowledgeBaseImages
            .IgnoreQueryFilters()
            .CountAsync(i => !string.IsNullOrWhiteSpace(i.StoragePath) && i.StoragePath.StartsWith("/"));

        return Ok(new
        {
            documentsNeedingMigration,
            imagesNeedingMigration,
            status = documentsNeedingMigration + imagesNeedingMigration == 0 ? "Complete" : "Pending"
        });
    }
}
