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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<KnowledgeBase>().Ignore(k => k.EmbeddingVector);
        }

        // Required for pgvector
        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            modelBuilder.HasPostgresExtension("vector");
        }

        // Tenant → User relationship
        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint on email
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

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
        modelBuilder.Entity<ModuleAssignment>()
            .HasIndex(m => new { m.CompanyId, m.ModuleDefinitionId, m.DepartmentId, m.UserId, m.Scope });
        modelBuilder.Entity<UsageEvent>()
            .HasIndex(e => new { e.CompanyId, e.MetricType, e.OccurredAt });
        modelBuilder.Entity<BillingSubscription>()
            .HasIndex(s => s.CompanyId)
            .IsUnique();

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

        // Global Query Filters for strict Multi-Tenancy (from both)
        modelBuilder.Entity<KnowledgeBase>().HasQueryFilter(k => k.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<WorkflowLog>().HasQueryFilter(w => w.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<AuditTrail>().HasQueryFilter(a => a.TenantId == _tenantProvider.GetTenantId());
        
        modelBuilder.Entity<InboxEvent>().HasQueryFilter(i => i.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<WorkflowPolicy>().HasQueryFilter(w => w.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<ActionRequest>().HasQueryFilter(a => a.TenantId == _tenantProvider.GetTenantId());

        modelBuilder.Entity<Department>().HasQueryFilter(d => d.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<UserCompanyMembership>().HasQueryFilter(m => m.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<UserDepartmentMembership>().HasQueryFilter(m => m.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<ModuleAssignment>().HasQueryFilter(a => a.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<ModulePermissionGrant>().HasQueryFilter(g => g.ModuleAssignment != null && g.ModuleAssignment.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<UsageEvent>().HasQueryFilter(e => e.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<BillingSubscription>().HasQueryFilter(s => s.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<Invitation>().HasQueryFilter(i => i.CompanyId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<CompanySubscription>().HasQueryFilter(s => s.CompanyId == _tenantProvider.GetTenantId());
    }
}
