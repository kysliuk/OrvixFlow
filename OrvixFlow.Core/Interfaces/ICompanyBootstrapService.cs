using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface ICompanyBootstrapService
{
    Task EnsureOwnerBootstrapAsync(Guid userId, Guid companyId);
    Task EnsureDefaultSubscriptionAsync(Guid companyId);
}
