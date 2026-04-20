using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Api.Filters;
using OrvixFlow.Infrastructure.Ai.Jobs;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge")]
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

    [HttpPost("upload")]
    [RequireModule("knowledge-base")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] Guid? departmentId = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var maxFileSizeMb = _configuration.GetValue("AI:Ingestion:MaxFileSizeMb", 20);
        if (file.Length > maxFileSizeMb * 1024 * 1024)
        {
            return BadRequest($"File size exceeds the limit of {maxFileSizeMb} MB.");
        }

        string detectedMimeType;
        using (var stream = file.OpenReadStream())
        {
            detectedMimeType = FileSignatureValidator.DetectMimeTypeFromStream(stream)
                ?? string.Empty;

            if (!FileSignatureValidator.IsAllowedMimeType(detectedMimeType))
            {
                return BadRequest(
                    $"File content type is not allowed. Detected: '{detectedMimeType}', allowed: PDF, PNG, JPEG, plain text.");
            }
        }

        var allowedTypes = _configuration.GetSection("AI:Ingestion:AllowedMimeTypes").Get<string[]>()
                           ?? new[] { "text/plain", "application/pdf", "image/png", "image/jpeg" };
        var clientContentType = file.ContentType.ToLowerInvariant();
        if (!allowedTypes.Contains(clientContentType))
        {
            return BadRequest($"Content type '{clientContentType}' is not in the allowed list.");
        }

        using (var stream = file.OpenReadStream())
        {
            if (!await _virusScanService.IsFileSafeAsync(stream, file.FileName))
            {
                return BadRequest("File failed security scan.");
            }
        }

        if (!CanAccessDepartment(departmentId))
        {
            return Forbid();
        }

        var tenantId = _tenantProvider.GetTenantId();
        var document = new KnowledgeBaseDocument
        {
            TenantId = tenantId,
            DepartmentId = departmentId,
            FileName = file.FileName,
            ContentType = detectedMimeType,
            FileSizeBytes = file.Length,
            Status = "Pending"
        };

        _dbContext.KnowledgeBaseDocuments.Add(document);
        await _dbContext.SaveChangesAsync();

        using (var stream = file.OpenReadStream())
        {
            var storageContext = new StorageContext(tenantId, departmentId, document.Id, file.FileName);
            var storagePath = await _storage.SaveFileAsync(storageContext, stream);
            document.StoragePath = storagePath;
            await _dbContext.SaveChangesAsync();
        }

        _backgroundJobClient.Enqueue<FileIngestionJob>(job => job.ProcessFileAsync(
            document.Id,
            document.StoragePath,
            document.FileName,
            document.ContentType,
            _scope.UserId == Guid.Empty ? null : _scope.UserId,
            departmentId,
            tenantId));

        return Ok(new
        {
            documentId = document.Id,
            departmentId,
            status = "Processing",
            message = "File uploaded successfully and queued for indexing."
        });
    }

    [HttpGet("documents")]
    [RequireModule("knowledge-base")]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] Guid? departmentId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.KnowledgeBaseDocuments.AsQueryable();

        if (departmentId.HasValue)
        {
            if (!CanAccessDepartment(departmentId))
            {
                return Forbid();
            }

            query = query.Where(d => d.DepartmentId == departmentId);
        }
        else if (!_scope.HasCompanyWideAccess)
        {
            var allowedDepartmentIds = _scope.AllowedDepartmentIds;
            query = query.Where(d =>
                d.DepartmentId != null && allowedDepartmentIds.Contains(d.DepartmentId.Value));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                id = d.Id,
                fileName = d.FileName,
                contentType = d.ContentType,
                fileSizeBytes = d.FileSizeBytes,
                departmentId = d.DepartmentId,
                status = d.Status,
                createdAtUtc = d.CreatedAtUtc,
                indexedAtUtc = d.IndexedAtUtc,
                errorMessage = d.ErrorMessage
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("documents/{id:guid}/download")]
    [RequireModule("knowledge-base")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var document = await _dbContext.KnowledgeBaseDocuments
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
        {
            return NotFound();
        }

        if (!CanAccessDepartment(document.DepartmentId))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(document.StoragePath))
        {
            return NotFound(new { message = "File not yet available in storage." });
        }

        try
        {
            var stream = await _storage.GetFileAsync(document.StoragePath);
            return File(stream, document.ContentType, document.FileName);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            return NotFound(new { message = "File not found in storage. It may have been deleted." });
        }
    }

    [HttpDelete("documents/{id:guid}")]
    [RequireModule("knowledge-base")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var document = await _dbContext.KnowledgeBaseDocuments
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
        {
            return NotFound(new { message = "Document not found." });
        }

        if (!CanAccessDepartment(document.DepartmentId))
        {
            return Forbid();
        }

        if (!string.IsNullOrEmpty(document.StoragePath))
        {
            try
            {
                await _storage.DeleteFileAsync(document.StoragePath);
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices
                    .GetService<Microsoft.Extensions.Logging.ILogger<FileIngestionController>>();
                logger?.LogWarning(ex,
                    "Failed to delete storage object {StoragePath} for document {DocumentId}. Object may be orphaned.",
                    document.StoragePath,
                    document.Id);
            }
        }

        _dbContext.KnowledgeBases.RemoveRange(
            _dbContext.KnowledgeBases.Where(k => k.DocumentId == id));
        _dbContext.KnowledgeBaseImages.RemoveRange(
            _dbContext.KnowledgeBaseImages.Where(i => i.DocumentId == id));
        _dbContext.KnowledgeBaseDocuments.Remove(document);

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private bool CanAccessDepartment(Guid? departmentId)
    {
        if (departmentId == null)
        {
            return _scope.HasCompanyWideAccess;
        }

        return _scope.HasCompanyWideAccess
            || _scope.AllowedDepartmentIds.Contains(departmentId.Value);
    }
}
