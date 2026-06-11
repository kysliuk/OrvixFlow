using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Api.Filters;

public class JobFailureAlertFilter : JobFilterAttribute, IApplyStateFilter
{
    private readonly ILogger<JobFailureAlertFilter> _logger;

    public JobFailureAlertFilter(ILogger<JobFailureAlertFilter> logger)
    {
        _logger = logger;
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is FailedState failedState)
        {
            _logger.LogCritical(
                failedState.Exception,
                "Hangfire job {JobId} ({JobName}) failed. Reason: {Reason}",
                context.BackgroundJob.Id,
                context.BackgroundJob.Job.Method.Name,
                failedState.Reason);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}
