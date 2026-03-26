using System;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Shadow;

/// <summary>
/// Audit-Log shadow module implementation.
/// Writes every AI decision to the AuditTrail table — not tenant-filtered on write,
/// so it captures data regardless of the current query-filter state.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db) => _db = db;

    public async Task RecordAsync(Guid tenantId, string action, string decisionDetails, Guid? userId = null)
    {
        _db.AuditTrails.Add(new AuditTrail
        {
            TenantId        = tenantId,
            Action          = action,
            DecisionDetails = decisionDetails,
            Timestamp       = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
