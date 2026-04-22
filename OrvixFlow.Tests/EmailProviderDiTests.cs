using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure;
using OrvixFlow.Infrastructure.Services;

namespace OrvixFlow.Tests;

public class EmailProviderDiTests
{
    [Fact]
    public void AddInfrastructure_WhenEmailProviderIsConsole_ResolvesMockEmailService()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Console"
        });
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetRequiredService<IEmailService>();

        emailService.Should().BeOfType<MockEmailService>();
    }

    [Fact]
    public void AddInfrastructure_WhenEmailProviderIsSmtp_ResolvesSmtpEmailService()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Smtp",
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:SmtpPort"] = "587"
        });
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetRequiredService<IEmailService>();

        emailService.Should().BeOfType<SmtpEmailService>();
    }

    [Fact]
    public void AddInfrastructure_WhenEmailProviderIsResend_ResolvesResendEmailService()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Resend",
            ["Email:ResendApiKey"] = "re_test_key"
        });
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetRequiredService<IEmailService>();

        emailService.Should().BeOfType<ResendEmailService>();
    }

    [Fact]
    public void AddInfrastructure_WhenEmailProviderIsResendWithoutApiKey_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Resend"
        });
        var services = new ServiceCollection();

        services.AddLogging();

        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Email:ResendApiKey is required*");
    }

    [Fact]
    public void AddInfrastructure_WhenEmailProviderIsSmtpWithPartialCredentials_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Smtp",
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:SmtpPort"] = "587",
            ["Email:SmtpUser"] = "mailer"
        });
        var services = new ServiceCollection();

        services.AddLogging();

        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Email:SmtpUser and Email:SmtpPass must both be provided together*");
    }

    [Fact]
    public void AddInfrastructure_WhenEmailProviderIsUnsupported_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "SendGrid"
        });
        var services = new ServiceCollection();

        services.AddLogging();

        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported Email:Provider*");
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "Mock",
            ["Storage:Provider"] = "Local",
            ["Storage:Local:BasePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            ["Security:VirusScan:Provider"] = "Noop",
            ["Email:Provider"] = "Console",
            ["Email:FromEmail"] = "noreply@orvixflow.local",
            ["Email:FromName"] = "OrvixFlow Identity"
        };

        foreach (var pair in overrides)
        {
            settings[pair.Key] = pair.Value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
