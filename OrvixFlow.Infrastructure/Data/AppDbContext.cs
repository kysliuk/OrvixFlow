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

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<WorkflowLog> WorkflowLogs => Set<WorkflowLog>();
    public DbSet<AuditTrail> AuditTrails => Set<AuditTrail>();
    
    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>();
    public DbSet<WorkflowPolicy> WorkflowPolicies => Set<WorkflowPolicy>();
    public DbSet<ActionRequest> ActionRequests => Set<ActionRequest>();

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

        // Global Query Filters for strict Multi-Tenancy
        modelBuilder.Entity<KnowledgeBase>().HasQueryFilter(k => k.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<WorkflowLog>().HasQueryFilter(w => w.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<AuditTrail>().HasQueryFilter(a => a.TenantId == _tenantProvider.GetTenantId());
        
        modelBuilder.Entity<InboxEvent>().HasQueryFilter(i => i.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<WorkflowPolicy>().HasQueryFilter(w => w.TenantId == _tenantProvider.GetTenantId());
        modelBuilder.Entity<ActionRequest>().HasQueryFilter(a => a.TenantId == _tenantProvider.GetTenantId());
    }
}
