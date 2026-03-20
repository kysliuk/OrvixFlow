using System;

namespace OrvixFlow.Core.Interfaces;

public interface ITenantProvider
{
    Guid GetTenantId();
}
