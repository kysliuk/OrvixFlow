using System;

namespace OrvixFlow.Core.Interfaces;

public interface ITenantProviderFactory
{
    ITenantProvider CreateProvider(Guid tenantId);
}
