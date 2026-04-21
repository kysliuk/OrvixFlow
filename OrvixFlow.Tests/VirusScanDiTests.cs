using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure;
using OrvixFlow.Infrastructure.Services.Security;
using OrvixFlow.Infrastructure.Storage;

namespace OrvixFlow.Tests;

public class VirusScanDiTests
{
    [Fact]
    public void AddInfrastructure_WhenProviderIsNoop_RegistersNoopVirusScanServiceExactlyOnce()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Mock",
                ["Storage:Provider"] = "Local",
                ["Storage:Local:BasePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                ["Security:VirusScan:Provider"] = "Noop",
                ["Security:VirusScan:ClamAv:Host"] = "clamav",
                ["Security:VirusScan:ClamAv:Port"] = "3310"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        var registrations = services
            .Where(service => service.ServiceType == typeof(IVirusScanService))
            .ToList();

        registrations.Should().HaveCount(1);
        registrations[0].ImplementationType.Should().Be(typeof(NoopVirusScanService));
    }

    [Fact]
    public void AddInfrastructure_WhenProviderIsClamAv_RegistersClamAvVirusScanServiceExactlyOnce()
    {
        var configuration = BuildConfiguration("ClamAv");
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        var registrations = services
            .Where(service => service.ServiceType == typeof(IVirusScanService))
            .ToList();

        registrations.Should().HaveCount(1);
        registrations[0].ImplementationType.Should().Be(typeof(ClamAvVirusScanService));
    }

    [Fact]
    public void AppSettings_DefaultVirusScanProvider_RemainsNoop()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GetAppSettingsPath())
            .Build();

        configuration["Security:VirusScan:Provider"].Should().Be("Noop");
        configuration["Security:VirusScan:ClamAv:Host"].Should().Be("localhost");
    }

    [Fact]
    public void AddInfrastructure_WhenProviderIsOverriddenToClamAv_UsesClamAvRegistrationOnly()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GetAppSettingsPath())
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Mock",
                ["Storage:Provider"] = "Local",
                ["Security:VirusScan:Provider"] = "ClamAv",
                ["Security:VirusScan:ClamAv:Host"] = "clamav",
                ["Security:VirusScan:ClamAv:Port"] = "3310"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        var registrations = services
            .Where(service => service.ServiceType == typeof(IVirusScanService))
            .ToList();

        registrations.Should().HaveCount(1);
        registrations[0].ImplementationType.Should().Be(typeof(ClamAvVirusScanService));
    }


    [Fact]
    public void AddInfrastructure_WhenStorageProviderIsLocal_ResolvesLegacyLocalFileStorage()
    {
        var configuration = BuildConfiguration("Noop");
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorage>();

        storage.Should().BeOfType<LocalFileStorage>();
    }

    [Fact]
    public void AddInfrastructure_WhenStorageProviderIsMinIo_ResolvesMinIoFileStorage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Mock",
                ["Storage:Provider"] = "MinIO",
                ["Storage:MinIO:Endpoint"] = "http://minio:9000",
                ["Storage:MinIO:Bucket"] = "orvixflow",
                ["Storage:MinIO:UseSSL"] = "false",
                ["MINIO_ACCESS_KEY"] = "minioadmin",
                ["MINIO_SECRET_KEY"] = "minioadmin",
                ["Security:VirusScan:Provider"] = "Noop"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorage>();

        storage.Should().BeOfType<MinIOFileStorage>();
    }

    [Fact]
    public void AddInfrastructure_WhenStorageProviderIsAzureBlob_ResolvesAzureBlobFileStorage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Mock",
                ["Storage:Provider"] = "AzureBlob",
                ["Storage:AzureBlob:ContainerName"] = "orvixflow",
                ["AZURE_STORAGE_CONNECTION_STRING"] = "UseDevelopmentStorage=true",
                ["Security:VirusScan:Provider"] = "Noop"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorage>();

        storage.Should().BeOfType<AzureBlobFileStorage>();
    }

    [Fact]
    public void AddInfrastructure_WhenStorageProviderIsUnsupported_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Mock",
                ["Storage:Provider"] = "S3",
                ["Security:VirusScan:Provider"] = "Noop"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();

        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported Storage:Provider*Expected Local, MinIO, or AzureBlob*");
    }

    [Fact]
    public void LocalFileStorage_IsMarkedObsoleteForProductionUse()
    {
        var attribute = typeof(LocalFileStorage).GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false)
            .Cast<ObsoleteAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull();
        attribute!.Message.Should().Contain("MinIOFileStorage").And.Contain("AzureBlobFileStorage");
    }

    [Fact]
    public async Task NoopVirusScanService_AlwaysReturnsTrue()
    {
        var service = new NoopVirusScanService();
        await using var stream = new MemoryStream(new byte[] { 0x4D, 0x5A });

        var result = await service.IsFileSafeAsync(stream, "test.exe");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ClamAvVirusScanService_WhenScannerThrows_ReturnsFalse()
    {
        var service = new ClamAvVirusScanService(new ThrowingClamAvClient(), NullLogger<ClamAvVirusScanService>.Instance);
        await using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });

        var result = await service.IsFileSafeAsync(stream, "unknown.bin");

        result.Should().BeFalse();
    }

    private static IConfiguration BuildConfiguration(string provider)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Mock",
                ["Storage:Provider"] = "Local",
                ["Storage:Local:BasePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                ["Security:VirusScan:Provider"] = provider,
                ["Security:VirusScan:ClamAv:Host"] = "clamav",
                ["Security:VirusScan:ClamAv:Port"] = "3310"
            })
            .Build();
    }

    private static string GetAppSettingsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "OrvixFlow.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(directory.FullName, "OrvixFlow.Api", "appsettings.json");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class ThrowingClamAvClient : IClamAvClient
    {
        public Task<VirusScanResult> ScanAsync(Stream stream, CancellationToken cancellationToken = default)
            => throw new SocketException();
    }
}
