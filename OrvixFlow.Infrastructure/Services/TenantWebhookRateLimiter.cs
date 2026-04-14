using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

public class TenantWebhookRateLimiter : ITenantWebhookRateLimiter
{
    private readonly AppDbContext _db;
    private readonly ILogger<TenantWebhookRateLimiter> _logger;
    private static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(1);

    public TenantWebhookRateLimiter(AppDbContext db, ILogger<TenantWebhookRateLimiter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> IsAllowedAsync(Guid tenantId)
    {
        var result = await CheckAndIncrementAsync(tenantId);
        return result.IsAllowed;
    }

    public async Task IncrementAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var limit = await _db.TenantWebhookLimits.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        
        if (limit == null)
        {
            limit = new TenantWebhookLimit
            {
                TenantId = tenantId,
                CallbackCount = 1,
                WindowStartUtc = now,
                Limit = 100,
                LastResetUtc = now
            };
            _db.TenantWebhookLimits.Add(limit);
        }
        else
        {
            if (now - limit.WindowStartUtc > WindowDuration)
            {
                limit.CallbackCount = 1;
                limit.WindowStartUtc = now;
            }
            else
            {
                limit.CallbackCount++;
            }
        }
        
        await _db.SaveChangesAsync();
    }

    public async Task<(bool IsAllowed, int Remaining, int Limit)> CheckAndIncrementAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var limit = await _db.TenantWebhookLimits.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        
        if (limit == null)
        {
            var newLimit = new TenantWebhookLimit
            {
                TenantId = tenantId,
                CallbackCount = 1,
                WindowStartUtc = now,
                Limit = 100,
                LastResetUtc = now
            };
            _db.TenantWebhookLimits.Add(newLimit);
            await _db.SaveChangesAsync();
            return (true, 99, 100);
        }

        if (now - limit.WindowStartUtc > WindowDuration)
        {
            limit.CallbackCount = 1;
            limit.WindowStartUtc = now;
            await _db.SaveChangesAsync();
            return (true, limit.Limit - 1, limit.Limit);
        }

        if (limit.CallbackCount >= limit.Limit)
        {
            _logger.LogWarning(
                "Webhook rate limit exceeded for tenant {TenantId}. Count: {Count}, Limit: {Limit}",
                tenantId, limit.CallbackCount, limit.Limit);
            return (false, 0, limit.Limit);
        }

        limit.CallbackCount++;
        await _db.SaveChangesAsync();
        return (true, limit.Limit - limit.CallbackCount, limit.Limit);
    }
}