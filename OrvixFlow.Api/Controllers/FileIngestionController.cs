using System;
using System.IO;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Api.Filters;
using OrvixFlow.Api.Jobs;
using OrvixFlow.Infrastructure.Ai.Jobs;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

using Microsoft.AspNetCore.RateLimiting;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge/upload")]
[Authorize]
[EnableRateLimiting("upload")]
public class FileIngestionController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorage _storage;
    private readonly ITenantProvider _tenantProvider;
    private readonly IScopeContext _scope;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IConfiguration _configuration;
    private readonly IVirusScanService _virusScanService;

    public FileIngestionController(
        AppDbContext dbContext,
        IFileStorage storage,
        ITenantProvider tenantProvider,
        IScopeContext scope,
        IBackgroundJobClient backgroundJobClient,
        IConfiguration configuration,
        IVirusScanService virusScanService)
    {
        _dbContext = dbContext;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _scope = scope;
        _backgroundJobClient = backgroundJobClient;
        _configuration = configuration;
        _virusScanService = virusScanService;
    }

    [HttpPost]
    [RequireModule("knowledge-base")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // 1. Validate File Size
        var maxFileSizeMb = _configuration.GetValue("AI:Ingestion:MaxFileSizeMb", 20);
        if (file.Length > maxFileSizeMb * 1024 * 1024)
        {
            return BadRequest($"File size exceeds the limit of {maxFileSizeMb} MB.");
        }

        // 2. Validate MIME Type
        var allowedTypes = _configuration.GetSection("AI:Ingestion:AllowedMimeTypes").Get<string[]>() 
                           ?? new[] { "text/plain", "application/pdf", "image/png", "image/jpeg" };
        if (!System.Linq.Enumerable.Contains(allowedTypes, file.ContentType.ToLower()))
        {
            return BadRequest($"Content type '{file.ContentType}' is not allowed.");
        }

        // 3. Virus Scan
        using (var stream = file.OpenReadStream())
        {
            if (!await _virusScanService.IsFileSafeAsync(stream, file.FileName))
            {
                return BadRequest("File failed security scan.");
            }
        }

        var tenantId = _tenantProvider.GetTenantId();
        
        // 1. Create document record
        var document = new KnowledgeBaseDocument
        {
            TenantId = tenantId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            Status = "Pending"
        };

        _dbContext.KnowledgeBaseDocuments.Add(document);
        await _dbContext.SaveChangesAsync();

        // 2. Save raw file locally before background processing
        using (var stream = file.OpenReadStream())
        {
            var storagePath = await _storage.SaveFileAsync(tenantId, document.Id, file.FileName, stream);
            document.StoragePath = storagePath;
            await _dbContext.SaveChangesAsync();
        }

        // 3. Enqueue background ingestion job
        _backgroundJobClient.Enqueue<FileIngestionJob>(job => job.ProcessFileAsync(
            document.Id, 
            document.StoragePath, 
            document.FileName, 
            document.ContentType, 
            _scope.UserId == Guid.Empty ? null : _scope.UserId, 
            null));

        return Ok(new
        {
            documentId = document.Id,
            status = "Processing",
            message = "File uploaded successfully and queued for indexing."
        });
    }
}
