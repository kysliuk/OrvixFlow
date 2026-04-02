using System;
using System.Threading.Tasks;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Core.Interfaces;

public interface IRagEmailService
{
    /// <summary>
    /// Processes an incoming email specifically for n8n RAG flow.
    /// Performs classification, retrieval, and draft generation.
    /// Returns the data in a structured N8nEmailPayload format.
    /// </summary>
    Task<N8nEmailPayload> ProcessRagEmailAsync(
        string senderEmail,
        string subject,
        string body,
        Guid tenantId,
        string messageId,
        Guid? traceId = null);
}
