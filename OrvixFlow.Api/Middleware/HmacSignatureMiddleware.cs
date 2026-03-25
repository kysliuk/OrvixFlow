using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Middleware;

public class HmacSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacSignatureMiddleware> _logger;
    private const string SignatureHeader = "X-Orvix-Signature";
    private const string SignaturePrefix = "sha256=";

    public HmacSignatureMiddleware(RequestDelegate next, ILogger<HmacSignatureMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/webhook/inbox", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(SignatureHeader, out var signatureHeader))
        {
            _logger.LogWarning("Missing signature header for inbox webhook");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing X-Orvix-Signature header" });
            return;
        }

        var signature = signatureHeader.ToString();
        if (signature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            signature = signature[SignaturePrefix.Length..];
        }

        var tenantProvider = context.RequestServices.GetRequiredService<ITenantProvider>();
        Guid tenantId;
        try
        {
            tenantId = tenantProvider.GetTenantId();
        }
        catch
        {
            if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantIdHeader) 
                && Guid.TryParse(tenantIdHeader, out var parsedId))
            {
                tenantId = parsedId;
            }
            else
            {
                _logger.LogWarning("Invalid tenant context for inbox webhook");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid tenant context" });
                return;
            }
        }

        var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant?.WebhookSecret == null)
        {
            _logger.LogWarning("Webhook secret not configured for tenant {TenantId}", tenantId);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Webhook secret not configured" });
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var expectedSignature = ComputeHmacSha256(body, tenant.WebhookSecret);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant())))
        {
            _logger.LogWarning("Invalid HMAC signature for tenant {TenantId}", tenantId);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid signature" });
            return;
        }

        await _next(context);
    }

    private static string ComputeHmacSha256(string payload, string base64Secret)
    {
        var keyBytes = Convert.FromBase64String(base64Secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
