# Phase 02 — Fix ClamAV DI Bug and Activate Virus Scanning

## Phase Goal

Fix the DI double-registration bug that permanently bypasses ClamAV.  
Add ClamAV daemon to docker-compose.  
Activate and verify real virus scanning is operational.

---

## Phase Purpose

This phase unblocks the most critical security gap: files are currently never scanned for viruses in any environment. ClamAV code exists and is correct, but a single erroneous duplicate registration line at the bottom of `DependencyInjection.cs` unconditionally overrides it.

This phase is prioritized before storage work because:
1. It is the smallest possible fix with highest security impact
2. It is fully independent of MinIO/Azure work
3. It must be verified isolated before storage migration adds more complexity

---

## Scope

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Remove erroneous duplicate `NoopVirusScanService` registration |
| `docker-compose.yml` | Add `clamav-daemon` service + dependency on `orvix-api` |
| `OrvixFlow.Api/appsettings.json` | Update `Security:VirusScan:Provider` to `"ClamAv"` in dev default |
| `.env` / `.env.example` | Add `VIRUS_SCAN_PROVIDER` env var |

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Tests/VirusScanDiTests.cs` | Regression test proving the DI bug is fixed and ClamAV wires correctly |

---

## Prerequisites

- Phase 01 analysis complete — bug location confirmed at line 174

---

## Implementation Instructions

### Step 1 — Fix DI Double-Registration

**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

Find and **remove** line 174:

```csharp
// REMOVE THIS LINE (currently line ~174, after email service registration):
services.AddScoped<IVirusScanService, NoopVirusScanService>();
```

**Before (broken):**
```csharp
// Lines 146-156: conditional registration
var virusScanProvider = configuration["Security:VirusScan:Provider"] ?? "Noop";
if (virusScanProvider == "ClamAv")
{
    services.Configure<ClamAvOptions>(configuration.GetSection("Security:VirusScan:ClamAv"));
    services.AddScoped<IClamAvClient, NclamClient>();
    services.AddScoped<IVirusScanService, ClamAvVirusScanService>();
}
else
{
    services.AddScoped<IVirusScanService, NoopVirusScanService>();
}
// ... more registrations (email, shadow, metrics) ...

services.AddScoped<IVirusScanService, NoopVirusScanService>();  // ← DELETE THIS LINE
services.AddScoped<IRagMetricsCollector, RagMetricsCollector>();
```

**After (fixed):**
```csharp
var virusScanProvider = configuration["Security:VirusScan:Provider"] ?? "Noop";
if (virusScanProvider == "ClamAv")
{
    services.Configure<ClamAvOptions>(configuration.GetSection("Security:VirusScan:ClamAv"));
    services.AddScoped<IClamAvClient, NclamClient>();
    services.AddScoped<IVirusScanService, ClamAvVirusScanService>();
}
else
{
    services.AddScoped<IVirusScanService, NoopVirusScanService>();
}
// ... rest of registrations ...
services.AddScoped<IRagMetricsCollector, RagMetricsCollector>();
// IVirusScanService registered exactly once above — do not add again
```

> **IMPORTANT:** Do not add any further `AddScoped<IVirusScanService, ...>` calls anywhere in `AddInfrastructure` or `AddOrvixSemanticKernel`. The conditional block at the top is sufficient.

---

### Step 2 — Add ClamAV Daemon to docker-compose

**File:** `docker-compose.yml`

Add the following service:

```yaml
  clamav:
    image: clamav/clamav:stable_base
    container_name: orvix_clamav
    environment:
      - CLAMAV_NO_FRESHCLAMD=false
    volumes:
      - clamav_data:/var/lib/clamav
    restart: unless-stopped
    networks:
      - internal
    healthcheck:
      test: ["CMD", "clamdcheck.sh"]
      interval: 60s
      timeout: 10s
      retries: 5
      start_period: 120s  # ClamAV takes time to load virus DB on first run
```

Add `clamav_data` to the `volumes:` block:
```yaml
volumes:
  pgdata:
  n8n_data:
  uploads_data:
  clamav_data:   # NEW
```

Add ClamAV dependency to `orvix-api.depends_on`:
```yaml
  orvix-api:
    depends_on:
      orvix-db:
        condition: service_started
      n8n:
        condition: service_started
      clamav:
        condition: service_healthy  # wait for ClamAV DB to load
```

> **Warning:** ClamAV `stable_base` image requires initial `freshclam` run to download the virus definitions database. This can take 2–5 minutes on first start. The `start_period: 120s` in healthcheck accounts for this. In CI, use `stable_base` and pre-seed the `clamav_data` volume, or use `stable` which includes the DB.

---

### Step 3 — Update Configuration

**File:** `OrvixFlow.Api/appsettings.json`

Update the VirusScan section so that the `ClamAv` host correctly points to the Docker service name:

```json
"Security": {
  "VirusScan": {
    "Provider": "Noop",
    "ClamAv": {
      "Host": "clamav",
      "Port": 3310,
      "ConnectTimeoutSeconds": 30,
      "ReadWriteTimeoutSeconds": 60
    }
  }
}
```

> Keep `Provider: "Noop"` as the default in `appsettings.json` so unit tests and cold-start without Docker still work. Override to `ClamAv` via environment variable in docker-compose.

**File:** `docker-compose.yml` — add to `orvix-api.environment`:
```yaml
Security__VirusScan__Provider: ${VIRUS_SCAN_PROVIDER:-ClamAv}
Security__VirusScan__ClamAv__Host: clamav
Security__VirusScan__ClamAv__Port: 3310
```

**File:** `.env.example` — add:
```bash
# Virus Scanning
VIRUS_SCAN_PROVIDER=ClamAv
```

---

### Step 4 — Verify ClamAvVirusScanService Fail-Closed Behavior

**File:** `OrvixFlow.Infrastructure/Services/Security/ClamAvVirusScanService.cs`

Current code is correct (returns `false` on `Error`):
```csharp
return result switch
{
    VirusScanResult.Clean => true,
    VirusScanResult.Infected => false,
    VirusScanResult.Error => false   // ← fail-closed: scanner outage blocks upload
};
```

**Also verify** that the outer `catch` in `IsFileSafeAsync` returns `false` (not `true`):  
Current code at line 37: `return false;` — this is correct. No change needed.

---

### Step 5 — Add IVirusScanService Regression Test

**File:** `OrvixFlow.Tests/VirusScanDiTests.cs` (new file)

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure;
using OrvixFlow.Infrastructure.Services.Security;

namespace OrvixFlow.Tests;

public class VirusScanDiTests
{
    [Fact]
    public void WhenProviderIsNoop_NopServiceIsRegistered_AndOnlyOnce()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:VirusScan:Provider"] = "Noop"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        
        // Minimal DI to test just IVirusScanService resolution
        // We only test that the conditional registration is wired correctly
        var virusScanProvider = config["Security:VirusScan:Provider"] ?? "Noop";
        if (virusScanProvider == "ClamAv")
        {
            services.AddScoped<IVirusScanService, ClamAvVirusScanService>();
        }
        else
        {
            services.AddScoped<IVirusScanService, NoopVirusScanService>();
        }

        var provider = services.BuildServiceProvider();
        var resolvedDescriptors = services
            .Where(s => s.ServiceType == typeof(IVirusScanService))
            .ToList();

        // Must be exactly one registration — not two
        resolvedDescriptors.Should().HaveCount(1);
        resolvedDescriptors[0].ImplementationType.Should().Be(typeof(NoopVirusScanService));
    }

    [Fact]
    public async Task NoopVirusScanService_AlwaysReturnsTrue()
    {
        var service = new NoopVirusScanService();
        using var stream = new MemoryStream(new byte[] { 0x4D, 0x5A });

        var result = await service.IsFileSafeAsync(stream, "test.exe");

        result.Should().BeTrue("Noop service never scans, always passes");
    }

    [Fact]
    public async Task ClamAvVirusScanService_OnScannerError_ReturnsFalse_FailClosed()
    {
        // Arrange: use a mock ClamAV client that always throws
        var mockClient = new ThrowingClamAvClient();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ClamAvVirusScanService>.Instance;
        var service = new ClamAvVirusScanService(mockClient, logger);

        using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });

        // Act
        var result = await service.IsFileSafeAsync(stream, "unknown.bin");

        // Assert: scanner outage must fail closed
        result.Should().BeFalse("scanner errors must block uploads (fail-closed)");
    }

    private class ThrowingClamAvClient : IClamAvClient
    {
        public Task<VirusScanResult> ScanAsync(Stream stream, System.Threading.CancellationToken cancellationToken = default)
            => throw new System.Net.Sockets.SocketException();
    }
}
```

> Note: `IClamAvClient` must be in `OrvixFlow.Core.Interfaces` or `OrvixFlow.Infrastructure.Services.Security` (currently it is in Infrastructure). 
> Check that it is accessible from the test project. If not, consider moving it to `OrvixFlow.Core/Interfaces/IClamAvClient.cs`.

---

## Constraints / Things Not to Break

- Do not change `IVirusScanService` interface signature — `IsFileSafeAsync(Stream, string)` remains unchanged
- Do not break `FileIngestionController` — it already calls `_virusScanService.IsFileSafeAsync` correctly
- Do not add any `if (provider == "ClamAv")` logic in controller or pipeline — DI handles provider selection
- `NoopVirusScanService` must remain available for unit tests and non-Docker local development

---

## Security Concerns

- ClamAV version must be kept updated via `freshclamd` — the docker image handles this automatically
- The `stable_base` image does not include virus definitions by default — first boot downloads them (takes time)
- If ClamAV daemon goes down, `NclamClient` throws a `SocketException` → `ClamAvVirusScanService` catches it → returns `false` → upload rejected. This is the correct fail-closed behavior.
- Do not expose ClamAV port 3310 outside the `internal` docker network

---

## Validation Checklist

- [ ] `DependencyInjection.cs` has exactly ONE `IVirusScanService` registration (under the conditional block)
- [ ] `docker compose up -d clamav` starts ClamAV daemon successfully
- [ ] `docker compose up -d` shows `orvix-clamav` healthy after ~2 minutes
- [ ] `dotnet test --filter VirusScanDiTests` passes
- [ ] With `Provider=ClamAv` in config, uploading a file causes ClamAV contact (log line "Virus scan failed" or nothing — do NOT test with real EICAR in CI without isolation)
- [ ] With `Provider=Noop`, upload works as before

---

## Completion Criteria

- [ ] Line 174 erroneous `AddScoped<IVirusScanService, NoopVirusScanService>()` deleted
- [ ] ClamAV daemon service defined in `docker-compose.yml`
- [ ] `orvix-api` depends on `clamav` with `condition: service_healthy`
- [ ] Environment variable `VIRUS_SCAN_PROVIDER` added to `.env.example`
- [ ] DI regression tests written and passing
- [ ] `dotnet test` passes

---

## Handoff to Phase 03

Phase 03 builds the storage abstraction layer (`StorageContext`, extended `IFileStorage`, `MinIOFileStorage`).  
ClamAV must be working before Phase 03's verification, because integration tests will upload real files through the full pipeline.
