using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

/// <summary>
/// Service for checking usage thresholds and queuing alerts.
/// </summary>
public interface IUsageAlertService
{
    /// <summary>
    /// Check if usage exceeds thresholds and queue appropriate alerts.
    /// </summary>
    /// <param name="companyId">The company to check.</param>
    /// <param name="metricType">The metric type (e.g., "ai-tokens", "storage-mb").</param>
    /// <param name="currentUsage">Current usage value.</param>
    /// <param name="limit">The limit/threshold.</param>
    Task CheckAndAlertAsync(Guid companyId, string metricType, decimal currentUsage, decimal limit);
    
    /// <summary>
    /// Check if an alert has already been sent this billing period.
    /// </summary>
    /// <param name="companyId">The company to check.</param>
    /// <param name="alertType">The alert type (e.g., "UsageWarning80", "UsageCritical100").</param>
    /// <returns>True if an alert was already sent this period.</returns>
    Task<bool> HasAlertBeenSentThisPeriodAsync(Guid companyId, string alertType);
}
