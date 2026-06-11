using System;
using System.Threading.RateLimiting;

namespace OrvixFlow.Api.Security;

public static class RateLimitPolicies
{
    public static FixedWindowRateLimiterOptions CreateLoginOptions()
    {
        return new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };
    }

    public static FixedWindowRateLimiterOptions CreateRegisterOptions()
    {
        return new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };
    }
}
