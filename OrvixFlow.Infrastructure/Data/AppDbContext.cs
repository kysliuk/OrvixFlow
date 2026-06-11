using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) 
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        base.OnConfiguring(optionsBuilder);
    }

    // From main
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<WorkflowLog> WorkflowLogs => Set<WorkflowLog>();
    public DbSet<AuditTrail> AuditTrails => Set<AuditTrail>();
    
    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>();
    public DbSet<WorkflowPolicy> WorkflowPolicies => Set<WorkflowPolicy>();
    public DbSet<ActionRequest> ActionRequests => Set<ActionRequest>();
    public DbSet<MailboxConnection> MailboxConnections => Set<MailboxConnection>();
    public DbSet<MailboxCredential> MailboxCredentials => Set<MailboxCredential>();
    public DbSet<AgentPersona> AgentPersonas => Set<AgentPersona>();

    public DbSet<DraftFeedback> DraftFeedbacks => Set<DraftFeedback>();

    // From DockerizationAndCompanyInfraFeature
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<UserCompanyMembership> UserCompanyMemberships => Set<UserCompanyMembership>();
    public DbSet<UserDepartmentMembership> UserDepartmentMemberships => Set<UserDepartmentMembership>();
    public DbSet<ModuleDefinition> ModuleDefinitions => Set<ModuleDefinition>();
    public DbSet<ModuleAssignment> ModuleAssignments => Set<ModuleAssignment>();
    public DbSet<ModulePermissionGrant> ModulePermissionGrants => Set<ModulePermissionGrant>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<BillingSubscription> BillingSubscriptions => Set<BillingSubscription>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<PlanTemplate> PlanTemplates => Set<PlanTemplate>();
    public DbSet<PlanModuleInclusion> PlanModuleInclusions => Set<PlanModuleInclusion>();
    public DbSet<PlanEntitlements> PlanEntitlements => Set<PlanEntitlements>();
    public DbSet<CompanySubscription> CompanySubscriptions => Set<CompanySubscription>();
    public DbSet<CompanyEntitlementOverride> CompanyEntitlementOverrides => Set<CompanyEntitlementOverride>();
    public DbSet<CompanyModuleOverride> CompanyModuleOverrides => Set<CompanyModuleOverride>();
    public DbSet<KnowledgeBaseDocument> KnowledgeBaseDocuments => Set<KnowledgeBaseDocument>();
    public DbSet<KnowledgeBaseImage> KnowledgeBaseImages => Set<KnowledgeBaseImage>();
    public DbSet<StoredObject> StoredObjects => Set<StoredObject>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TenantWebhookLimit> TenantWebhookLimits => Set<TenantWebhookLimit>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<NotificationQueue> NotificationQueues => Set<NotificationQueue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<KnowledgeBase>().Ignore(k => k.EmbeddingVector);
            modelBuilder.Entity<KnowledgeBase>().Ignore(k => k.SearchVector);
            modelBuilder.Entity<KnowledgeBaseImage>().Ignore(k => k.CaptionEmbedding);
        }

        // Required for pgvector
        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<KnowledgeBase>()
                .HasGeneratedTsVectorColumn(
                    p => p.SearchVector,
                    "english",
                    p => new { p.Title, p.RawContent })
                .HasIndex(p => p.SearchVector)
                .HasMethod("GIN");

            modelBuilder.Entity<KnowledgeBase>()
                .Property(x => x.EmbeddingVector)
                .HasColumnType("vector(1536)");

            modelBuilder.Entity<KnowledgeBase>()
                .HasIndex(x => x.EmbeddingVector)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            modelBuilder.Entity<KnowledgeBaseImage>()
                .Property(x => x.CaptionEmbedding)
                .HasColumnType("vector(1536)");

            modelBuilder.Entity<KnowledgeBaseImage>()
                .HasIndex(x => x.CaptionEmbedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        }

        // Tenant → User relationship
        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint on email
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.Token).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.LookupKey).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.FamilyId);

        // Organizations / Access control constraints & seeds
        modelBuilder.Entity<UserCompanyMembership>()
            .HasIndex(m => new { m.UserId, m.CompanyId })
            .IsUnique();
        modelBuilder.Entity<UserDepartmentMembership>()
            .HasIndex(m => new { m.UserId, m.CompanyId, m.DepartmentId })
            .IsUnique();
        modelBuilder.Entity<Department>()
            .HasIndex(d => new { d.CompanyId, d.Code })
            .IsUnique();
        modelBuilder.Entity<ModuleDefinition>()
            .HasIndex(m => m.Key)
            .IsUnique();
        modelBuilder.Entity<ModuleDefinition>().HasData(ModuleCatalog.BuildSeed());
        modelBuilder.Entity<PlanTemplate>().HasData(PlanCatalog.BuildPlanSeed());
        modelBuilder.Entity<PlanEntitlements>().HasData(PlanCatalog.BuildEntitlementsSeed());
        modelBuilder.Entity<PlanModuleInclusion>().HasData(PlanCatalog.BuildModuleInclusionsSeed());
        modelBuilder.Entity<ModuleAssignment>()
            .HasIndex(m => new { m.CompanyId, m.ModuleDefinitionId, m.DepartmentId, m.UserId, m.Scope });
        modelBuilder.Entity<UsageEvent>()
            .HasIndex(e => new { e.CompanyId, e.MetricType, e.OccurredAt });
        modelBuilder.Entity<BillingSubscription>()
            .HasIndex(s => s.CompanyId)
            .IsUnique();

        // EF Core Value Converters for typed enums (stored as strings in DB)

        // CompanySubscription - Status (SubscriptionState enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.Status)
            .HasConversion(
                v => v.ToClaimValue(),
                v => SubscriptionStateExtensions.ParseState(v));

        // CompanySubscription - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));

        // PlanTemplate - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<PlanTemplate>()
            .Property(p => p.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));

        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.Departments)
            .WithOne(d => d.Company)
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCompanyMembership>()
            .HasOne(m => m.User)
            .WithMany(u => u.CompanyMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserCompanyMembership>()
            .HasOne(m => m.Company)
            .WithMany(t => t.UserMemberships)
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserDepartmentMembership>()
            .HasOne(m => m.User)
            .WithMany(u => u.DepartmentMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserDepartmentMembership>()
            .HasOne(m => m.Company)
            .WithMany(t => t.UserDepartmentMemberships)
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserDepartmentMembership>()
            .HasOne(m => m.Department)
            .WithMany(d => d.UserMemberships)
            .HasForeignKey(m => m.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ModuleAssignment>()
            .HasOne(a => a.Company)
            .WithMany(t => t.ModuleAssignments)
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ModuleAssignment>()
            .HasOne(a => a.ModuleDefinition)
            .WithMany(d => d.Assignments)
            .HasForeignKey(a => a.ModuleDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ModuleAssignment>()
            .HasOne(a => a.Department)
            .WithMany(d => d.ModuleAssignments)
            .HasForeignKey(a => a.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ModuleAssignment>()
            .HasOne(a => a.User)
            .WithMany(u => u.ModuleAssignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ModulePermissionGrant>()
            .HasOne(g => g.ModuleAssignment)
            .WithMany(a => a.PermissionGrants)
            .HasForeignKey(g => g.ModuleAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Invitation
        modelBuilder.Entity<Invitation>()
            .HasIndex(i => i.Token).IsUnique();

        // EF Core Value Converters for typed enums (stored as strings in DB)

        // CompanySubscription - Status (SubscriptionState enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.Status)
            .HasConversion(
                v => v.ToClaimValue(),
                v => SubscriptionStateExtensions.ParseState(v));

        // CompanySubscription - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));

        // PlanTemplate - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<PlanTemplate>()
            .Property(p => p.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));
        modelBuilder.Entity<Invitation>()
            .HasIndex(i => new { i.CompanyId, i.Email });
        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.Company)
            .WithMany()
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.Department)
            .WithMany()
            .HasForeignKey(i => i.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // PlanTemplate
        modelBuilder.Entity<PlanTemplate>()
            .HasIndex(p => p.Slug)
            .IsUnique();

        // EF Core Value Converters for typed enums (stored as strings in DB)

        // CompanySubscription - Status (SubscriptionState enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.Status)
            .HasConversion(
                v => v.ToClaimValue(),
                v => SubscriptionStateExtensions.ParseState(v));

        // CompanySubscription - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));

        // PlanTemplate - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<PlanTemplate>()
            .Property(p => p.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));
        modelBuilder.Entity<PlanTemplate>()
            .HasOne(p => p.Entitlements)
            .WithOne(e => e.PlanTemplate)
            .HasForeignKey<PlanEntitlements>(e => e.PlanTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PlanTemplate>()
            .HasMany(p => p.ModuleInclusions)
            .WithOne(m => m.PlanTemplate)
            .HasForeignKey(m => m.PlanTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // PlanModuleInclusion
        modelBuilder.Entity<PlanModuleInclusion>()
            .HasIndex(m => new { m.PlanTemplateId, m.ModuleDefinitionId })
            .IsUnique();

        // EF Core Value Converters for typed enums (stored as strings in DB)

        // CompanySubscription - Status (SubscriptionState enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.Status)
            .HasConversion(
                v => v.ToClaimValue(),
                v => SubscriptionStateExtensions.ParseState(v));

        // CompanySubscription - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<CompanySubscription>()
            .Property(s => s.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));

        // PlanTemplate - BillingInterval (BillingInterval enum -> string)
        modelBuilder.Entity<PlanTemplate>()
            .Property(p => p.BillingInterval)
            .HasConversion(
                v => v.ToClaimValue(),
                v => BillingIntervalExtensions.ParseInterval(v));
        modelBuilder.Entity<PlanModuleInclusion>()
            .HasOne(m => m.ModuleDefinition)
            .WithMany(d => d.PlanInclusions)
            .HasForeignKey(m => m.ModuleDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        // PlanEntitlements
        modelBuilder.Entity<PlanEntitlements>()
            .HasIndex(e => e.PlanTemplateId)
            .IsUnique();

        // CompanySubscription
        modelBuilder.Entity<CompanySubscription>()
            .HasIndex(s => s.CompanyId)
            .IsUnique();
        modelBuilder.Entity<CompanySubscription>()
            .HasOne(s => s.Company)
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompanySubscription>()
            .HasOne(s => s.PlanTemplate)
            .WithMany(p => p.CompanySubscriptions)
            .HasForeignKey(s => s.PlanTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompanySubscription>()
            .HasOne(s => s.PendingPlan)
            .WithMany()
            .HasForeignKey(s => s.PendingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // KnowledgeBaseDocument Relationship
        modelBuilder.Entity<KnowledgeBaseDocument>()
            .HasMany(d => d.Chunks)
            .WithOne(c => c.Document)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // KnowledgeBaseDocument — Department FK (nullable, set null on dept delete)
        modelBuilder.Entity<KnowledgeBaseDocument>()
            .HasOne(d => d.Department)
            .WithMany()
            .HasForeignKey(d => d.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Composite index for tenant + department scoped queries
        modelBuilder.Entity<KnowledgeBaseDocument>()
            .HasIndex(d => new { d.TenantId, d.DepartmentId });

        modelBuilder.Entity<StoredObject>()
            .HasIndex(s => new { s.TenantId, s.EntityType, s.EntityId });

        modelBuilder.Entity<StoredObject>()
            .HasIndex(s => s.StorageKey);

        modelBuilder.Entity<KnowledgeBaseImage>()
            .HasOne(i => i.Document)
            .WithMany()
            .HasForeignKey(i => i.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<KnowledgeBaseImage>()
            .HasOne(i => i.Chunk)
            .WithMany()
            .HasForeignKey(i => i.ChunkId)
            .OnDelete(DeleteBehavior.SetNull);

        // MailboxConnection relationships
        modelBuilder.Entity<MailboxConnection>()
            .HasIndex(m => new { m.TenantId, m.EmailAddress })
            .IsUnique();
        modelBuilder.Entity<MailboxConnection>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MailboxCredential>()
            .HasOne(c => c.MailboxConnection)
            .WithMany()
            .HasForeignKey(c => c.MailboxConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MailboxConnection>()
            .HasOne<MailboxCredential>()
            .WithMany()
            .HasForeignKey(m => m.CredentialId)
            .OnDelete(DeleteBehavior.SetNull);

        // AgentPersona relationships
        modelBuilder.Entity<AgentPersona>()
            .HasIndex(a => a.TenantId)
            .IsUnique();

        // DraftFeedback relationships
        modelBuilder.Entity<DraftFeedback>()
            .HasOne<ActionRequest>()
            .WithMany()
            .HasForeignKey(d => d.ActionRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DraftFeedback>()
            .HasIndex(d => new { d.TenantId, d.ActionRequestId });

        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<MailboxConnection>()
                .HasIndex(m => new { m.TenantId, m.EmailAddress })
                .IsUnique();
            modelBuilder.Entity<AgentPersona>()
                .HasIndex(a => a.TenantId)
                .IsUnique();
        }

        // Global Query Filters for strict Multi-Tenancy (from both)
        modelBuilder.Entity<KnowledgeBase>().HasQueryFilter(k => k.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<WorkflowLog>().HasQueryFilter(w => w.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<AuditTrail>().HasQueryFilter(a => a.TenantId == _tenantProvider.GetTenantId());
        
        modelBuilder.Entity<InboxEvent>().HasQueryFilter(i => i.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<WorkflowPolicy>().HasQueryFilter(w => w.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<ActionRequest>().HasQueryFilter(a => a.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<MailboxConnection>().HasQueryFilter(m => m.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<MailboxCredential>().HasQueryFilter(m => m.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<AgentPersona>().HasQueryFilter(a => a.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<DraftFeedback>().HasQueryFilter(d => d.TenantId == _tenantProvider.GetTenantId());

        modelBuilder.Entity<Department>().HasQueryFilter(d => d.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<UserCompanyMembership>().HasQueryFilter(m => m.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<UserDepartmentMembership>().HasQueryFilter(m => m.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<ModuleAssignment>().HasQueryFilter(a => a.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<ModulePermissionGrant>().HasQueryFilter(g => g.ModuleAssignment != null && g.ModuleAssignment.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<UsageEvent>().HasQueryFilter(e => e.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<BillingSubscription>().HasQueryFilter(s => s.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<Invitation>().HasQueryFilter(i => i.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<CompanySubscription>().HasQueryFilter(s => s.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<KnowledgeBaseDocument>().HasQueryFilter(d => d.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<KnowledgeBaseImage>().HasQueryFilter(i => i.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<StoredObject>().HasQueryFilter(s => s.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<TenantWebhookLimit>().HasQueryFilter(t => t.TenantId == _tenantProvider.GetTenantId());

        // T3-4: NotificationQueue query filter and relationship
        modelBuilder.Entity<NotificationQueue>()
            .HasOne(n => n.Company)
            .WithMany()
            .HasForeignKey(n => n.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<NotificationQueue>()
            .HasIndex(n => new { n.Processed, n.Failed, n.IsProcessing, n.ProcessingStartedAt, n.CreatedAt });
        modelBuilder.Entity<NotificationQueue>().HasQueryFilter(n => n.CompanyId == _tenantProvider.GetTenantId());

        modelBuilder.Entity<CompanyEntitlementOverride>()
            .HasOne(o => o.Company)
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompanyEntitlementOverride>()
            .HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompanyEntitlementOverride>()
            .HasIndex(o => o.CompanyId)
            .IsUnique();

        modelBuilder.Entity<CompanyModuleOverride>()
            .HasOne(o => o.Company)
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompanyModuleOverride>()
            .HasOne(o => o.ModuleDefinition)
            .WithMany()
            .HasForeignKey(o => o.ModuleDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompanyModuleOverride>()
            .HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CompanyModuleOverride>()
            .HasIndex(o => new { o.CompanyId, o.ModuleDefinitionId })
            .IsUnique();

        modelBuilder.Entity<CompanyEntitlementOverride>().HasQueryFilter(o => o.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<CompanyModuleOverride>().HasQueryFilter(o => o.CompanyId == _tenantProvider.GetTenantId());

        // T4-3: Invoice Status (InvoiceStatus enum -> string)
        modelBuilder.Entity<Invoice>()
            .Property(i => i.Status)
            .HasConversion(
                v => v.ToClaimValue(),
                v => InvoiceStatusExtensions.ParseStatus(v));
    }
}
