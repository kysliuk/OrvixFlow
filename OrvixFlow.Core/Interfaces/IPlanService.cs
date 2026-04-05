using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Core.Interfaces;

public interface IPlanService
{
    Task<PlanTemplate> CreatePlanAsync(PlanTemplate plan, IEnumerable<Guid>? moduleIds = null);
    Task<PlanTemplate?> GetPlanByIdAsync(Guid planId);
    Task<PlanTemplate?> GetPlanBySlugAsync(string slug);
    Task<IEnumerable<PlanTemplate>> GetAllPlansAsync(bool includeInactive = false);
    Task<IEnumerable<PlanTemplate>> GetActivePlansAsync();
    Task<PlanTemplate> UpdatePlanAsync(PlanTemplate plan);
    Task ArchivePlanAsync(Guid planId);
    Task ReactivatePlanAsync(Guid planId);
    Task AddModuleToPlanAsync(Guid planId, Guid moduleId);
    Task RemoveModuleFromPlanAsync(Guid planId, Guid moduleId);
    Task SetEntitlementsAsync(Guid planId, PlanEntitlements entitlements);
    Task SyncModulesForPlanAsync(Guid planId, IEnumerable<Guid> moduleIds);
}

public class PlanSlugAlreadyExistsException : SubscriptionException
{
    public string Slug { get; }
    public PlanSlugAlreadyExistsException(string slug) : base($"Plan with slug '{slug}' already exists")
    {
        Slug = slug;
    }
}

public class ModuleNotFoundException : SubscriptionException
{
    public Guid ModuleId { get; }
    public ModuleNotFoundException(Guid moduleId) : base($"Module with ID {moduleId} not found")
    {
        ModuleId = moduleId;
    }
}
