using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

public class PlanService : IPlanService
{
    private readonly AppDbContext _dbContext;

    public PlanService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlanTemplate> CreatePlanAsync(PlanTemplate plan, IEnumerable<Guid>? moduleIds = null)
    {
        var existingPlan = await _dbContext.PlanTemplates
            .FirstOrDefaultAsync(p => p.Slug == plan.Slug);

        if (existingPlan != null)
        {
            throw new PlanSlugAlreadyExistsException(plan.Slug);
        }

        if (moduleIds != null)
        {
            var moduleIdList = moduleIds.ToList();
            var existingModules = await _dbContext.ModuleDefinitions
                .Where(m => moduleIdList.Contains(m.Id))
                .ToListAsync();

            if (existingModules.Count != moduleIdList.Count)
            {
                var missingIds = moduleIdList.Except(existingModules.Select(m => m.Id));
                throw new ModuleNotFoundException(missingIds.First());
            }

            plan.ModuleInclusions = moduleIdList
                .Select(moduleId => new PlanModuleInclusion
                {
                    PlanTemplateId = plan.Id,
                    ModuleDefinitionId = moduleId
                })
                .ToList();
        }

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        return plan;
    }

    public async Task<PlanTemplate?> GetPlanByIdAsync(Guid planId)
    {
        return await _dbContext.PlanTemplates
            .Include(p => p.Entitlements)
            .Include(p => p.ModuleInclusions)
            .ThenInclude(m => m.ModuleDefinition)
            .FirstOrDefaultAsync(p => p.Id == planId);
    }

    public async Task<PlanTemplate?> GetPlanBySlugAsync(string slug)
    {
        return await _dbContext.PlanTemplates
            .Include(p => p.Entitlements)
            .Include(p => p.ModuleInclusions)
            .ThenInclude(m => m.ModuleDefinition)
            .FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<IEnumerable<PlanTemplate>> GetAllPlansAsync(bool includeInactive = false)
    {
        var query = _dbContext.PlanTemplates
            .Include(p => p.Entitlements)
            .Include(p => p.ModuleInclusions)
            .ThenInclude(m => m.ModuleDefinition)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<PlanTemplate>> GetActivePlansAsync()
    {
        return await GetAllPlansAsync(includeInactive: false);
    }

    public async Task<PlanTemplate> UpdatePlanAsync(PlanTemplate plan)
    {
        var existingPlan = await _dbContext.PlanTemplates
            .FirstOrDefaultAsync(p => p.Id == plan.Id);

        if (existingPlan == null)
        {
            throw new PlanNotFoundException(plan.Id);
        }

        var duplicateSlug = await _dbContext.PlanTemplates
            .FirstOrDefaultAsync(p => p.Slug == plan.Slug && p.Id != plan.Id);

        if (duplicateSlug != null)
        {
            throw new PlanSlugAlreadyExistsException(plan.Slug);
        }

        existingPlan.Name = plan.Name;
        existingPlan.Description = plan.Description;
        existingPlan.MonthlyPriceCents = plan.MonthlyPriceCents;
        existingPlan.YearlyPriceCents = plan.YearlyPriceCents;
        existingPlan.Currency = plan.Currency;
        existingPlan.BillingInterval = plan.BillingInterval;
        existingPlan.MaxSeats = plan.MaxSeats;
        existingPlan.IsActive = plan.IsActive;
        existingPlan.IsFree = plan.IsFree;
        existingPlan.IsTrialAllowed = plan.IsTrialAllowed;
        existingPlan.TrialDays = plan.TrialDays;
        existingPlan.LegacyLocked = plan.LegacyLocked;

        await _dbContext.SaveChangesAsync();

        return existingPlan;
    }

    public async Task ArchivePlanAsync(Guid planId)
    {
        var plan = await _dbContext.PlanTemplates.FindAsync(planId);

        if (plan == null)
        {
            throw new PlanNotFoundException(planId);
        }

        plan.IsActive = false;
        plan.ArchivedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task ReactivatePlanAsync(Guid planId)
    {
        var plan = await _dbContext.PlanTemplates.FindAsync(planId);

        if (plan == null)
        {
            throw new PlanNotFoundException(planId);
        }

        plan.IsActive = true;
        plan.ArchivedAt = null;

        await _dbContext.SaveChangesAsync();
    }

    public async Task AddModuleToPlanAsync(Guid planId, Guid moduleId)
    {
        var plan = await _dbContext.PlanTemplates
            .Include(p => p.ModuleInclusions)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan == null)
        {
            throw new PlanNotFoundException(planId);
        }

        var module = await _dbContext.ModuleDefinitions.FindAsync(moduleId);
        if (module == null)
        {
            throw new ModuleNotFoundException(moduleId);
        }

        var existingInclusion = plan.ModuleInclusions
            .FirstOrDefault(m => m.ModuleDefinitionId == moduleId);

        if (existingInclusion != null)
        {
            return;
        }

        var newInclusion = new PlanModuleInclusion
        {
            PlanTemplateId = planId,
            ModuleDefinitionId = moduleId
        };
        _dbContext.PlanModuleInclusions.Add(newInclusion);

        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveModuleFromPlanAsync(Guid planId, Guid moduleId)
    {
        var inclusion = await _dbContext.PlanModuleInclusions
            .FirstOrDefaultAsync(m => m.PlanTemplateId == planId && m.ModuleDefinitionId == moduleId);

        if (inclusion == null)
        {
            return;
        }

        _dbContext.PlanModuleInclusions.Remove(inclusion);
        await _dbContext.SaveChangesAsync();
    }

    public async Task SyncModulesForPlanAsync(Guid planId, IEnumerable<Guid> moduleIds)
    {
        var plan = await _dbContext.PlanTemplates
            .Include(p => p.ModuleInclusions)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new PlanNotFoundException(planId);

        var currentModuleIds = plan.ModuleInclusions.Select(m => m.ModuleDefinitionId).ToHashSet();
        var newModuleIds = moduleIds.ToHashSet();

        var toRemove = plan.ModuleInclusions.Where(m => !newModuleIds.Contains(m.ModuleDefinitionId)).ToList();
        foreach (var m in toRemove)
            _dbContext.PlanModuleInclusions.Remove(m);

        foreach (var moduleId in newModuleIds)
        {
            if (!currentModuleIds.Contains(moduleId))
            {
                _dbContext.PlanModuleInclusions.Add(new PlanModuleInclusion
                {
                    PlanTemplateId = planId,
                    ModuleDefinitionId = moduleId
                });
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task SetEntitlementsAsync(Guid planId, PlanEntitlements entitlements)
    {
        var plan = await _dbContext.PlanTemplates
            .Include(p => p.Entitlements)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan == null)
        {
            throw new PlanNotFoundException(planId);
        }

        if (plan.Entitlements != null)
        {
            plan.Entitlements.MaxMonthlyTokens = entitlements.MaxMonthlyTokens;
            plan.Entitlements.MaxApiRequestsPerDay = entitlements.MaxApiRequestsPerDay;
            plan.Entitlements.MaxStorageMb = entitlements.MaxStorageMb;
            plan.Entitlements.MaxKnowledgeBases = entitlements.MaxKnowledgeBases;
        }
        else
        {
            entitlements.PlanTemplateId = planId;
            plan.Entitlements = entitlements;
            _dbContext.PlanEntitlements.Add(entitlements);
        }

        await _dbContext.SaveChangesAsync();
    }
}
