using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public static class ModuleCatalog
{
    public static readonly Guid DocIntelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid FinanceFlowId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid InboxGuardianId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid LeadQualifierId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid LegalScribeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid DataGuardianId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid SopGeneratorId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid MeteredBillingId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid AuditLogId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    public static IReadOnlyList<ModuleDefinition> BuildSeed()
    {
        return
        [
            new ModuleDefinition { Id = DocIntelId, Key = "doc-intel", DisplayName = "Doc-Intel", Tier = "Utility", Visibility = "UserFacing", IsOperational = false, IsActive = true },
            new ModuleDefinition { Id = FinanceFlowId, Key = "finance-flow", DisplayName = "Finance-Flow", Tier = "Utility", Visibility = "UserFacing", IsOperational = false, IsActive = true },
            new ModuleDefinition { Id = InboxGuardianId, Key = "inbox-guardian", DisplayName = "Inbox-Guardian", Tier = "Utility", Visibility = "UserFacing", IsOperational = false, IsActive = true },
            new ModuleDefinition { Id = LeadQualifierId, Key = "lead-qualifier", DisplayName = "Lead-Qualifier", Tier = "Industry", Visibility = "UserFacing", IsOperational = false, IsActive = true },
            new ModuleDefinition { Id = LegalScribeId, Key = "legal-scribe", DisplayName = "Legal-Scribe", Tier = "Industry", Visibility = "UserFacing", IsOperational = false, IsActive = true },
            new ModuleDefinition { Id = DataGuardianId, Key = "data-guardian", DisplayName = "Data-Guardian", Tier = "Industry", Visibility = "UserFacing", IsOperational = false, IsActive = true },
            new ModuleDefinition { Id = SopGeneratorId, Key = "sop-generator", DisplayName = "SOP-Generator", Tier = "Industry", Visibility = "UserFacing", IsOperational = false, IsActive = true },
            new ModuleDefinition { Id = MeteredBillingId, Key = "metered-billing", DisplayName = "Metered-Billing", Tier = "Shadow", Visibility = "Restricted", IsOperational = true, IsActive = true },
            new ModuleDefinition { Id = AuditLogId, Key = "audit-log", DisplayName = "Audit-Log", Tier = "Shadow", Visibility = "Restricted", IsOperational = true, IsActive = true }
        ];
    }
}
