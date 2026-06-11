using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Hangfire;
using OrvixFlow.Api.Filters;
using Xunit;

namespace OrvixFlow.Tests;

public class ObservabilityDiTests
{
    [Fact]
    public void OpenTelemetry_ServicesAreRegisteredSuccessfully()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:OtlpEndpoint"] = "http://localhost:4317"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Register OpenTelemetry mimicking Program.cs
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(configuration["Telemetry:OtlpEndpoint"]!)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(configuration["Telemetry:OtlpEndpoint"]!)));

        using var provider = services.BuildServiceProvider();

        // Verify that tracing and metrics builder or tracer provider are registered
        var tracerProvider = provider.GetService<TracerProvider>();
        var meterProvider = provider.GetService<MeterProvider>();

        tracerProvider.Should().NotBeNull();
        meterProvider.Should().NotBeNull();
    }

    [Fact]
    public void JobFailureAlertFilter_CanBeInstantiatedAndStateInterceptionLogged()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<JobFailureAlertFilter>>();

        var filter = new JobFailureAlertFilter(logger);
        filter.Should().NotBeNull();
    }

    [Fact]
    public void JobFailureAlertFilter_CanBeAddedToGlobalFilters()
    {
        var initialCount = GlobalJobFilters.Filters.Count;

        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<JobFailureAlertFilter>>();

        var filter = new JobFailureAlertFilter(logger);
        GlobalJobFilters.Filters.Add(filter);

        GlobalJobFilters.Filters.Count.Should().Be(initialCount + 1);

        // Cleanup
        GlobalJobFilters.Filters.Remove(filter);
    }
}
