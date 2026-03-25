using System;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services;

public class BackgroundTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;

    public BackgroundTenantProvider(Guid tenantId)
    {
        _tenantId = tenantId;
    }

    public Guid GetTenantId() => _tenantId;
}

public class ScopedTenantProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ScopedTenantProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ITenantProvider CreateProvider(Guid tenantId)
    {
        return new BackgroundTenantProvider(tenantId);
    }
}
