using System;
using System.Threading.Tasks;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Core.Interfaces;

public interface IAgentService
{
    Task<AgentResponse> ProcessInternalAsync(string prompt, Guid tenantId);
}
