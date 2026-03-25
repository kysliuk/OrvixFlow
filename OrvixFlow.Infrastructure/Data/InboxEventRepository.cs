using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Data;

public interface IInboxEventRepository
{
    Task<(InboxEvent? Event, bool IsDuplicate)> CreateWithIdempotencyCheckAsync(
        string messageId,
        Guid tenantId,
        string senderEmail,
        string senderName,
        string subject,
        string bodyText,
        string? webhookCallbackPath = null);
    
    Task<InboxEvent?> GetByIdAsync(Guid id);
    Task<InboxEvent?> GetByMessageIdAsync(string messageId);
    Task UpdateStatusAsync(Guid id, string status);
    Task<(IEnumerable<InboxEvent> Items, int TotalCount)> ListAsync(Guid tenantId, string? status, int limit, int offset);
}

public class InboxEventRepository : IInboxEventRepository
{
    private readonly AppDbContext _dbContext;

    public InboxEventRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(InboxEvent? Event, bool IsDuplicate)> CreateWithIdempotencyCheckAsync(
        string messageId,
        Guid tenantId,
        string senderEmail,
        string senderName,
        string subject,
        string bodyText,
        string? webhookCallbackPath = null)
    {
        var existing = await _dbContext.InboxEvents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.MessageId == messageId && e.TenantId == tenantId);

        if (existing != null)
        {
            return (existing, IsDuplicate: true);
        }

        var inboxEvent = new InboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = messageId,
            SenderEmail = senderEmail,
            SenderName = senderName,
            Subject = subject,
            BodyText = bodyText,
            WebhookCallbackPath = webhookCallbackPath,
            ReceivedAtUtc = DateTime.UtcNow,
            Status = InboxEventStatus.Ingested
        };

        _dbContext.InboxEvents.Add(inboxEvent);
        await _dbContext.SaveChangesAsync();

        return (inboxEvent, IsDuplicate: false);
    }

    public async Task<InboxEvent?> GetByIdAsync(Guid id)
    {
        return await _dbContext.InboxEvents.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<InboxEvent?> GetByMessageIdAsync(string messageId)
    {
        return await _dbContext.InboxEvents.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.MessageId == messageId);
    }

    public async Task UpdateStatusAsync(Guid id, string status)
    {
        var inboxEvent = await _dbContext.InboxEvents.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
        if (inboxEvent != null)
        {
            inboxEvent.Status = status;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<(IEnumerable<InboxEvent> Items, int TotalCount)> ListAsync(Guid tenantId, string? status, int limit, int offset)
    {
        var query = _dbContext.InboxEvents.Where(e => e.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        return (items, totalCount);
    }
}

public static class InboxEventStatus
{
    public const string Ingested = "Ingested";
    public const string Processing = "Processing";
    public const string ActionRequired = "Action_Required";
    public const string AutoApproved = "Auto_Approved";
    public const string HumanApproved = "Human_Approved";
    public const string HumanRejected = "Human_Rejected";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
