using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using BackgroundTenantProvider = OrvixFlow.Infrastructure.Services.BackgroundTenantProvider;

namespace OrvixFlow.Api.Jobs;

public class InboxProcessingJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxProcessingJob> _logger;

    public InboxProcessingJob(IServiceProvider serviceProvider, ILogger<InboxProcessingJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task ProcessAsync(Guid inboxEventId, Guid tenantId)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>() as BackgroundTenantProvider;
        if (tenantProvider != null)
        {
            var field = typeof(BackgroundTenantProvider).GetField("_tenantId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(tenantProvider, tenantId);
        }

        var repository = scope.ServiceProvider.GetRequiredService<IInboxEventRepository>();
        var guardianService = scope.ServiceProvider.GetRequiredService<IInboxGuardianService>();
        var policyGateService = scope.ServiceProvider.GetRequiredService<IPolicyGateService>();
        var callbackService = scope.ServiceProvider.GetRequiredService<IWebhookCallbackService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inboxEvent = await repository.GetByIdAsync(inboxEventId);
        if (inboxEvent == null)
        {
            _logger.LogError("InboxEvent not found: {EventId}", inboxEventId);
            return;
        }

        var traceId = inboxEvent.TraceId ?? Guid.NewGuid().ToString("N");
        inboxEvent.TraceId = traceId;
        await repository.UpdateStatusAsync(inboxEventId, InboxEventStatus.Processing);

        _logger.LogInformation(
            "[{TraceId}] Processing email: {MessageId}, EventId: {EventId}, Tenant: {TenantId}",
            traceId, inboxEvent.MessageId, inboxEventId, tenantId);

        try
        {
            var persona = await dbContext.AgentPersonas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId);

            var message = new InboxMessage
            {
                SenderEmail = inboxEvent.SenderEmail,
                Subject = inboxEvent.Subject,
                Body = inboxEvent.BodyText
            };

            var response = await guardianService.ProcessIncomingMessageAsync(message, tenantId, persona);

            if (!response.IsSuccess)
            {
                await repository.UpdateStatusAsync(inboxEventId, InboxEventStatus.Failed);
                _logger.LogError(
                    "[{TraceId}] Failed to process email: {MessageId}, Error: {Error}",
                    traceId, inboxEvent.MessageId, response.ErrorMessage);
                return;
            }

            var metadata = response.Metadata as System.Collections.Generic.Dictionary<string, object?>;
            var category = metadata?["category"]?.ToString() ?? "Unknown";
            var confidenceScore = metadata?["confidenceScore"] is decimal d ? d 
                : metadata?["confidenceScore"] is double dbl ? (decimal)dbl 
                : 0.0m;
            var personaApplied = metadata?["personaApplied"] is true;

            var policyContext = new PolicyEvaluationContext
            {
                SenderEmail = inboxEvent.SenderEmail,
                Subject = inboxEvent.Subject,
                BodyText = inboxEvent.BodyText,
                Category = category,
                ConfidenceScore = confidenceScore
            };

            var policyDecision = await policyGateService.EvaluateAsync(policyContext, tenantId);

            _logger.LogInformation(
                "[{TraceId}] Policy decision for {MessageId}: {Decision} - {Reason}, PersonaApplied: {PersonaApplied}",
                traceId, inboxEvent.MessageId, policyDecision.Decision, policyDecision.Reason, personaApplied);

            switch (policyDecision.Decision)
            {
                case PolicyDecisionType.AutoExecute:
                    await repository.UpdateStatusAsync(inboxEventId, InboxEventStatus.AutoApproved);
                    if (!string.IsNullOrEmpty(inboxEvent.WebhookCallbackPath))
                    {
                        try
                        {
                            await callbackService.SendCallbackAsync(
                                inboxEvent.WebhookCallbackPath,
                                policyDecision,
                                inboxEventId,
                                response.Message);
                            _logger.LogInformation(
                                "[{TraceId}] Callback sent for auto-approve {MessageId}",
                                traceId, inboxEvent.MessageId);
                        }
                        catch (Exception callbackEx)
                        {
                            _logger.LogWarning(callbackEx,
                                "[{TraceId}] Callback failed for auto-approve {MessageId}, scheduling retry",
                                traceId, inboxEvent.MessageId);
                            ScheduleCallbackRetry(callbackService, inboxEvent.WebhookCallbackPath,
                                policyDecision, inboxEventId, response.Message, 1, traceId);
                        }
                    }
                    _logger.LogInformation(
                        "[{TraceId}] Email auto-approved: {MessageId}, Category: {Category}, Confidence: {Confidence}",
                        traceId, inboxEvent.MessageId, category, confidenceScore);
                    break;

                case PolicyDecisionType.HoldForApproval:
                    await repository.UpdateStatusAsync(inboxEventId, InboxEventStatus.ActionRequired);

                    var actionRequest = new ActionRequest
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        InboxEventId = inboxEventId,
                        EvaluatedCategory = category,
                        ConfidenceScore = confidenceScore,
                        DraftResponse = response.Message ?? string.Empty,
                        PolicyReason = policyDecision.Reason,
                        CreatedAtUtc = DateTime.UtcNow,
                        ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                        Status = "Pending",
                        RowVersion = 0
                    };
                    dbContext.ActionRequests.Add(actionRequest);

                    var auditTrail = new AuditTrail
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Action = "ActionRequestCreated",
                        Actor = "system",
                        EntityId = actionRequest.Id.ToString(),
                        PreviousState = "",
                        NewState = "Pending",
                        DecisionDetails = $"Human review required for email from {inboxEvent.SenderEmail}. Category: {category}. Reason: {policyDecision.Reason}. TraceId: {traceId}",
                        Timestamp = DateTime.UtcNow
                    };
                    dbContext.AuditTrails.Add(auditTrail);

                    await dbContext.SaveChangesAsync();

                    policyDecision.ActionRequestId = actionRequest.Id;

                    if (!string.IsNullOrEmpty(inboxEvent.WebhookCallbackPath))
                    {
                        try
                        {
                            await callbackService.SendCallbackAsync(
                                inboxEvent.WebhookCallbackPath,
                                policyDecision,
                                inboxEventId,
                                response.Message);
                            _logger.LogInformation(
                                "[{TraceId}] Callback sent for hold-approval {MessageId}",
                                traceId, inboxEvent.MessageId);
                        }
                        catch (Exception callbackEx)
                        {
                            _logger.LogWarning(callbackEx,
                                "[{TraceId}] Callback failed for hold-approval {MessageId}, scheduling retry",
                                traceId, inboxEvent.MessageId);
                            ScheduleCallbackRetry(callbackService, inboxEvent.WebhookCallbackPath,
                                policyDecision, inboxEventId, response.Message, 1, traceId);
                        }
                    }
                    _logger.LogInformation(
                        "[{TraceId}] Email requires human review: {MessageId}, Category: {Category}, Confidence: {Confidence}, ActionRequestId: {ActionId}",
                        traceId, inboxEvent.MessageId, category, confidenceScore, actionRequest.Id);
                    break;
            }
        }
        catch (Exception ex)
        {
            await repository.UpdateStatusAsync(inboxEventId, InboxEventStatus.Failed);
            _logger.LogError(ex, "[{TraceId}] Exception processing email: {MessageId}", traceId, inboxEvent.MessageId);
            throw;
        }
    }

    private void ScheduleCallbackRetry(IWebhookCallbackService callbackService, string webhookPath,
        PolicyDecision decision, Guid inboxEventId, string? draftResponse, int attempt, string traceId)
    {
        if (attempt > 3)
        {
            _logger.LogError("[{TraceId}] Callback retry exhausted for {WebhookPath} EventId: {EventId}",
                traceId, webhookPath, inboxEventId);
            return;
        }

        var delaySeconds = attempt * 30;
        BackgroundJob.Schedule(
            () => RetryCallbackAsync(callbackService, webhookPath, decision, inboxEventId, draftResponse, attempt, traceId),
            TimeSpan.FromSeconds(delaySeconds));
    }

    public async Task RetryCallbackAsync(IWebhookCallbackService callbackService, string webhookPath,
        PolicyDecision decision, Guid inboxEventId, string? draftResponse, int attempt, string traceId)
    {
        try
        {
            await callbackService.SendCallbackAsync(webhookPath, decision, inboxEventId, draftResponse);
            _logger.LogInformation("[{TraceId}] Callback retry succeeded on attempt {Attempt} for {WebhookPath}",
                traceId, attempt, webhookPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{TraceId}] Callback retry {Attempt} failed for {WebhookPath}, scheduling next retry",
                traceId, attempt, webhookPath);
            ScheduleCallbackRetry(callbackService, webhookPath, decision, inboxEventId, draftResponse, attempt + 1, traceId);
        }
    }
}
