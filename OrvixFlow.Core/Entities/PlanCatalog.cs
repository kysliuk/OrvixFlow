using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public static class PlanCatalog
{
    public static readonly Guid FreeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid StarterId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid GrowthId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid BusinessId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid EnterpriseId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    public static IReadOnlyList<PlanTemplate> BuildPlanSeed()
    {
        return
        [
            new PlanTemplate
            {
                Id = FreeId,
                Name = "Free",
                Slug = "free",
                Description = "For individuals testing AI workflows.",
                MonthlyPriceCents = 0,
                YearlyPriceCents = 0,
                Currency = "USD",
                BillingInterval = BillingInterval.Monthly,
                SortOrder = 0,
                IsPubliclyVisible = true,
                MaxSeats = 2,
                IsActive = true,
                IsFree = true,
                IsTrialAllowed = false,
                TrialDays = 0,
                LegacyLocked = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanTemplate
            {
                Id = StarterId,
                Name = "Starter",
                Slug = "starter",
                Description = "For small teams automating support.",
                MonthlyPriceCents = 2900,
                YearlyPriceCents = 29000,
                Currency = "USD",
                BillingInterval = BillingInterval.Monthly,
                SortOrder = 1,
                IsPubliclyVisible = true,
                MaxSeats = 5,
                IsActive = true,
                IsFree = false,
                IsTrialAllowed = true,
                TrialDays = 14,
                LegacyLocked = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanTemplate
            {
                Id = GrowthId,
                Name = "Growth",
                Slug = "growth",
                Description = "For growing teams needing more power.",
                MonthlyPriceCents = 9900,
                YearlyPriceCents = 99000,
                Currency = "USD",
                BillingInterval = BillingInterval.Monthly,
                SortOrder = 2,
                IsPubliclyVisible = true,
                MaxSeats = 25,
                IsActive = true,
                IsFree = false,
                IsTrialAllowed = true,
                TrialDays = 14,
                LegacyLocked = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanTemplate
            {
                Id = BusinessId,
                Name = "Business",
                Slug = "business",
                Description = "For large teams with advanced needs.",
                MonthlyPriceCents = 29900,
                YearlyPriceCents = 299000,
                Currency = "USD",
                BillingInterval = BillingInterval.Monthly,
                SortOrder = 3,
                IsPubliclyVisible = true,
                MaxSeats = 100,
                IsActive = true,
                IsFree = false,
                IsTrialAllowed = true,
                TrialDays = 14,
                LegacyLocked = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanTemplate
            {
                Id = EnterpriseId,
                Name = "Enterprise",
                Slug = "enterprise",
                Description = "For large organizations with custom needs.",
                MonthlyPriceCents = 0,
                YearlyPriceCents = 0,
                Currency = "USD",
                BillingInterval = BillingInterval.Custom,
                SortOrder = 4,
                IsPubliclyVisible = false, // Enterprise is contact-sales only
                MaxSeats = null,
                IsActive = true,
                IsFree = false,
                IsTrialAllowed = false,
                TrialDays = 0,
                LegacyLocked = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        ];
    }

    public static IReadOnlyList<PlanEntitlements> BuildEntitlementsSeed()
    {
        return
        [
            new PlanEntitlements
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PlanTemplateId = FreeId,
                MaxMonthlyTokens = 50000,
                MaxApiRequestsPerDay = 500,
                MaxStorageMb = 100,
                MaxKnowledgeBases = 1,
                MaxInboxMessagesPerMonth = 50,      // Free tier limited inbox
                MaxMailboxConnections = 1,           // One email connection
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanEntitlements
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PlanTemplateId = StarterId,
                MaxMonthlyTokens = 100000,
                MaxApiRequestsPerDay = 1000,
                MaxStorageMb = 500,
                MaxKnowledgeBases = 5,
                MaxInboxMessagesPerMonth = 500,      // Starter tier inbox
                MaxMailboxConnections = 3,           // Up to 3 email connections
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanEntitlements
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                PlanTemplateId = GrowthId,
                MaxMonthlyTokens = 500000,
                MaxApiRequestsPerDay = 5000,
                MaxStorageMb = 5120,
                MaxKnowledgeBases = 25,
                MaxInboxMessagesPerMonth = 2000,      // Growth tier inbox
                MaxMailboxConnections = 10,           // Up to 10 email connections
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanEntitlements
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                PlanTemplateId = BusinessId,
                MaxMonthlyTokens = 2000000,
                MaxApiRequestsPerDay = 20000,
                MaxStorageMb = 51200,
                MaxKnowledgeBases = 100,
                MaxInboxMessagesPerMonth = 0,          // Unlimited for Business
                MaxMailboxConnections = 50,           // Up to 50 email connections
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanEntitlements
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                PlanTemplateId = EnterpriseId,
                MaxMonthlyTokens = 10000000,
                MaxApiRequestsPerDay = 100000,
                MaxStorageMb = 512000,
                MaxKnowledgeBases = 1000,
                MaxInboxMessagesPerMonth = 0,          // Unlimited for Enterprise
                MaxMailboxConnections = 0,             // Unlimited for Enterprise (0 = no limit)
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        ];
    }

    public static IReadOnlyList<PlanModuleInclusion> BuildModuleInclusionsSeed()
    {
        return
        [
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000001"), PlanTemplateId = FreeId, ModuleDefinitionId = ModuleCatalog.KnowledgeBaseId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000002"), PlanTemplateId = FreeId, ModuleDefinitionId = ModuleCatalog.InboxGuardianId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000003"), PlanTemplateId = StarterId, ModuleDefinitionId = ModuleCatalog.KnowledgeBaseId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000004"), PlanTemplateId = StarterId, ModuleDefinitionId = ModuleCatalog.InboxGuardianId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000005"), PlanTemplateId = StarterId, ModuleDefinitionId = ModuleCatalog.DocIntelId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000006"), PlanTemplateId = GrowthId, ModuleDefinitionId = ModuleCatalog.KnowledgeBaseId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000007"), PlanTemplateId = GrowthId, ModuleDefinitionId = ModuleCatalog.InboxGuardianId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000008"), PlanTemplateId = GrowthId, ModuleDefinitionId = ModuleCatalog.DocIntelId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000009"), PlanTemplateId = GrowthId, ModuleDefinitionId = ModuleCatalog.LeadQualifierId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000a"), PlanTemplateId = GrowthId, ModuleDefinitionId = ModuleCatalog.FinanceFlowId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000b"), PlanTemplateId = BusinessId, ModuleDefinitionId = ModuleCatalog.KnowledgeBaseId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000c"), PlanTemplateId = BusinessId, ModuleDefinitionId = ModuleCatalog.InboxGuardianId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000d"), PlanTemplateId = BusinessId, ModuleDefinitionId = ModuleCatalog.DocIntelId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000e"), PlanTemplateId = BusinessId, ModuleDefinitionId = ModuleCatalog.LeadQualifierId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000f"), PlanTemplateId = BusinessId, ModuleDefinitionId = ModuleCatalog.FinanceFlowId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000010"), PlanTemplateId = BusinessId, ModuleDefinitionId = ModuleCatalog.LegalScribeId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000011"), PlanTemplateId = BusinessId, ModuleDefinitionId = ModuleCatalog.SopGeneratorId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000012"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.KnowledgeBaseId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000013"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.InboxGuardianId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000014"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.DocIntelId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000015"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.LeadQualifierId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000016"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.FinanceFlowId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000017"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.LegalScribeId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000018"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.SopGeneratorId },
            new PlanModuleInclusion { Id = Guid.Parse("a0000000-0000-0000-0000-000000000019"), PlanTemplateId = EnterpriseId, ModuleDefinitionId = ModuleCatalog.DataGuardianId }
        ];
    }
}
