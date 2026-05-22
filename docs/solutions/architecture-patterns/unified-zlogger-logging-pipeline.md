---
title: "Unified ZLogger Logging Pipeline"
date: 2026-04-25
category: architecture-patterns
module: XFramework.Integration
problem_type: architecture_pattern
component: tooling
severity: high
applies_when:
  - "Removing competing Serilog and ZLogger pipelines or standardizing Seq CLEF logging, scopes, and global properties"
tags: [zlogger, logging, seq, serilog, observability]
---

# Unified ZLogger Migration — Design Spec

**Status:** Current decision record. The migration described here has been applied in the active codebase: ZLogger through `AddXFrameworkLogging()` is the current logging pipeline, and Serilog references below are historical removal context.

## Problem

XFramework services have two competing logging pipelines:

1. **Serilog** — `XApplication.Configure<T>()` calls `builder.Host.UseSerilog()` which replaces `ILoggerFactory` with `SerilogLoggerFactory`. Serilog's Seq sink reads `SEQ_URL` env var (not set in docker-compose), falls back to `http://localhost:5341` (container's own loopback — wrong).
2. **ZLogger** — every service calls `AddXFrameworkLogging()` which registers ZLogger console + Seq providers. But `SerilogLoggerFactory` ignores all M.E.L providers, so ZLogger never receives a log.

**Result:** Services produce no Seq logs. ControlPanel.Server works because it uses plain `WebApplication.CreateBuilder(args)` (no `UseSerilog()`), so ZLogger is the actual pipeline.

## Solution

Remove Serilog entirely. Make ZLogger + `AddXFrameworkLogging()` the single logging pipeline for all services, tests, and Blazor WASM.

## Scope

| Area | Files | Change |
|------|-------|--------|
| XApplication bootstrap | `XApplication.cs`, `InstallerExtensions.cs` | Remove `UseSerilog()`, remove Serilog config block |
| Core global usings | `XFramework.Core/GlobalUsings.cs` | Remove 3 Serilog usings |
| ApplicationEnricher | `XFramework.Core/Loggers/ApplicationEnricher.cs` | Delete (dead code, Serilog-only interface) |
| Core packages | `XFramework.Core.csproj` | Remove 5 Serilog PackageReferences |
| CorrelationIdMiddleware | `CorrelationIdMiddleware.cs` | `LogContext.PushProperty` → `ILogger.BeginScope` with CorrelationId + RequestPath + RequestMethod |
| ZLoggerSeqSink | `ZLoggerSeqSink.cs` | Add global properties support + scope capture in CLEF output |
| LoggingExtensions | `LoggingExtensions.cs` | Pass global properties (Application, Environment, MachineName, RuntimeVersion), enable IncludeScopes |
| Blazor module | `BaseActionHandler.cs`, `XForm.razor`, `InstallerExtensions.cs`, `.csproj` | Static `Log.*` → `ILogger<T>`, delete `AddSerilog()`, remove Serilog package |
| Test infrastructure | `BoltTestHelper.cs`, `IntegrationTestFixture.cs`, `WalletsTestFixture.cs`, benchmarks | `Serilog:MinimumLevel:Default` → `Logging:LogLevel:Default` |
| Service appsettings | All service `appsettings.json` | Remove legacy `"Serilog"` config blocks |
| Seq config (dev) | All service `appsettings.Development.json` | Add `"Seq": { "Url": "http://100.75.11.49:5341" }` |
| Coins.Api | `Coins.Api/Program.cs` | Add missing `AddXFrameworkLogging()` call |
| Central packages | `Directory.Packages.props` | Remove all Serilog packages |

## Design Details

### 1. XApplication bootstrap cleanup

**`XApplication.Configure<T>()`** — remove `builder.Host.UseSerilog()` (line 20). Services already call `AddXFrameworkLogging()` after `Configure<T>()` returns.

**`InstallerExtensions.InstallStandardServices<T>()`** — remove lines 93-104 (Serilog `LoggerConfiguration` block that configures Console + Seq and assigns `Log.Logger`). Also remove the `using Log = Serilog.Log;` alias at line 23. The `_logger` field used by `DisplayRuntimeEnvironment()` stays — it resolves from DI at line 112 and will now be backed by ZLogger.

**`XFramework.Core/GlobalUsings.cs`** — remove:
```
global using Serilog;
global using Serilog.Core;
global using Serilog.Events;
```

**`ApplicationEnricher.cs`** — delete entirely. Implements `ILogEventEnricher` (Serilog-only). Useful properties move to ZLoggerSeqSink global properties.

**`XFramework.Core.csproj`** — remove:
```xml
<PackageReference Include="Serilog.AspNetCore" />
<PackageReference Include="Serilog.Enrichers.Span" />
<PackageReference Include="Serilog.Sinks.Async" />
<PackageReference Include="Serilog.Sinks.Console" />
<PackageReference Include="Serilog.Sinks.Seq" />
```

### 2. CorrelationIdMiddleware — M.E.L scopes

Replace `Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId)` with:

```csharp
using var scope = logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["RequestPath"] = context.Request.Path.Value ?? "",
    ["RequestMethod"] = context.Request.Method
});
await _next(context);
```

Inject `ILogger<CorrelationIdMiddleware>` via constructor. Remove `using Serilog.Context;`.

### 3. ZLoggerSeqSink enhancements

**Global properties** — `Register()` gets a new `Dictionary<string, string>? globalProperties` parameter. Written into every CLEF event by `FormatClef`. Computed once at startup:

- `Application` — from `BoltConfiguration:ClientName` (already exists)
- `Environment` — from `ASPNETCORE_ENVIRONMENT`
- `MachineName` — from `Environment.MachineName`
- `RuntimeVersion` — from entry assembly `TargetFrameworkAttribute`

**Scope capture** — `FormatClef` reads scope state from `IZLoggerEntry` and writes scope `KeyValuePair<string, object>` / `Dictionary<string, object>` entries as top-level CLEF properties.

**Property precedence** (lowest to highest): global properties → scope properties → structured template parameters.

### 4. LoggingExtensions update

`AddXFrameworkLogging()` builds the global properties dictionary and passes it to `ZLoggerSeqSink.Register()`. Enable `IncludeScopes = true` on console provider so scopes flow to all providers.

### 5. Blazor WASM module

- `BaseActionHandler.cs` — inject `ILogger<BaseActionHandler>`, replace 9 `Log.Error()`/`Log.Information()` calls
- `XForm.razor` — `@inject ILogger<XForm> Logger`, replace 3 `Log.Warning()` calls
- `InstallerExtensions.cs` — delete `AddSerilog()` method
- `XFramework.Blazor.csproj` — remove Serilog package reference

### 6. Test infrastructure

Replace all `"Serilog:MinimumLevel:Default"` config overrides with `"Logging:LogLevel:Default"`:

- `BoltTestHelper.cs` (3 occurrences)
- `IntegrationTestFixture.cs` (2 occurrences)
- `WalletsTestFixture.cs` (2 occurrences)
- `ConcurrentBenchmarks.cs` (2 occurrences)
- `ThroughputBenchmarks.cs` (1 occurrence)
- `TransportBenchmarks.cs` (2 occurrences)

### 7. Service appsettings cleanup

**Remove** legacy `"Serilog"` JSON blocks from all service `appsettings.json` files.

**Add** `"Seq": { "Url": "http://100.75.11.49:5341" }` to `appsettings.Development.json` for all services so dev-mode logs reach Seq on xeon-dev.

Docker appsettings already have `"Seq": { "Url": "http://seq:80" }` — no changes needed.

### 8. Coins.Api

Add missing `builder.Logging.AddXFrameworkLogging(builder.Configuration);` call after `XApplication.Configure<Program>()`.

### 9. Package cleanup

**`Directory.Packages.props`** — remove:
- `Serilog.AspNetCore`
- `Serilog.Enrichers.Span`
- `Serilog.Sinks.Async`
- `Serilog.Sinks.BrowserHttp`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.Seq`

## Verification

1. `dotnet build XFramework.slnx` — clean build, no Serilog references remain
2. Run ControlPanel.Server locally — verify Seq receives logs with CorrelationId, RequestPath, RequestMethod, Application, MachineName
3. Run IdentityServer locally — verify Seq receives structured logs (was broken before this change)
4. Check Seq dashboard for global properties appearing on all events
