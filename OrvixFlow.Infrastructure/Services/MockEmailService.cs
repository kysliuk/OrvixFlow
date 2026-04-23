using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services;

public class MockEmailService : IEmailService
{
    private static readonly Regex AuthLinkRegex = new(@"https?://[^'""\s<]+/(verify|invite)\?token=[^'""\s<]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        var authLink = TryExtractAuthLink(body);
        _logger.LogInformation(@"
╔════════════════════════ [ MOCK EMAIL ] ════════════════════════╗
║  To:      {To}
║  Subject: {Subject}
║  AuthLink: {AuthLink}
╠════════════════════════════ BODY ══════════════════════════════╣
{Body}
╚════════════════════════════════════════════════════════════════╝
", to, subject, authLink ?? "n/a", body);

        return Task.CompletedTask;
    }

    internal static string? TryExtractAuthLink(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var match = AuthLinkRegex.Match(body);
        return match.Success ? match.Value : null;
    }
}
