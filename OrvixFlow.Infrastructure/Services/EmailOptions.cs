namespace OrvixFlow.Infrastructure.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Console"; // "Console", "Smtp", or "Resend"
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 25;
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public string? ResendApiKey { get; set; }
    public string ResendBaseUrl { get; set; } = "https://api.resend.com";
    public string FromEmail { get; set; } = "noreply@orvixflow.local";
    public string FromName { get; set; } = "OrvixFlow Identity";
    public bool UseSsl { get; set; } = true;
}
