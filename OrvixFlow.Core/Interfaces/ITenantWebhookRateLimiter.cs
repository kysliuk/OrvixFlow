using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface ITenantWebhookRateLimiter
{
    Task<bool> IsAllowedAsync(Guid tenantId);
    Task IncrementAsync(Guid tenantId);
    Task<(bool IsAllowed, int Remaining, int Limit)> CheckAndIncrementAsync(Guid tenantId);
}