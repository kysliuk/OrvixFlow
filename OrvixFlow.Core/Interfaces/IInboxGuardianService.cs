using System;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Core.Interfaces;

public class InboxMessage
{
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public interface IInboxGuardianService
{
    Task<AgentResponse> ProcessIncomingMessageAsync(InboxMessage message, Guid tenantId, AgentPersona? persona = null, Guid? userId = null, Guid? departmentId = null);
}
