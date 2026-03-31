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

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge/upload")]
[Authorize]
public class FileIngestionController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorage _storage;
    private readonly ITenantProvider _tenantProvider;
    private readonly IScopeContext _scope;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public FileIngestionController(
        AppDbContext dbContext,
        IFileStorage storage,
        ITenantProvider tenantProvider,
        IScopeContext scope,
        IBackgroundJobClient backgroundJobClient)
    {
        _dbContext = dbContext;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _scope = scope;
        _backgroundJobClient = backgroundJobClient;
    }

    [HttpPost]
    [RequireModule("knowledge-base")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
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
