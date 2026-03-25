using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

/// <summary>
/// Shadow module: Audit-Log
/// Records every important AI decision with tamper-resistant traceability.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Record a single AI decision or system action.
    /// </summary>
    /// <param name="tenantId">Company performing the action.</param>
    /// <param name="action">Short action key, e.g. "inbox-guardian.classify".</param>
    /// <param name="decisionDetails">JSON or descriptive text of the decision made.</param>
    /// <param name="userId">Optional: user who triggered the action.</param>
    Task RecordAsync(Guid tenantId, string action, string decisionDetails, Guid? userId = null);
}
