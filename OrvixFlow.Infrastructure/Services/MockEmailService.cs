using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services;

public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation(@"
╔════════════════════════ [ MOCK EMAIL ] ════════════════════════╗
║  To:      {To}
║  Subject: {Subject}
╠════════════════════════════ BODY ══════════════════════════════╣
{Body}
╚════════════════════════════════════════════════════════════════╝
", to, subject, body);

        return Task.CompletedTask;
    }
}
