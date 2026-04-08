using System;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services;

public class TenantProviderFactory : ITenantProviderFactory
{
    public ITenantProvider CreateProvider(Guid tenantId)
    {
        return new BackgroundTenantProvider(tenantId);
    }
}
