using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPass),
                EnableSsl = _options.UseSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw; // Re-throw to allow higher-level handling if needed
        }
    }
}
