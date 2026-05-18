# Unified ZLogger Migration — Implementation Plan

> Historical plan migrated from `docs/superpowers/`. For new implementation planning, use `/ce-plan`; this checklist is retained as context.

**Goal:** Remove Serilog entirely and make ZLogger the single logging pipeline across all XFramework services, tests, and Blazor WASM.

**Architecture:** ZLogger sits on top of Microsoft.Extensions.Logging (M.E.L). Console output via `AddZLoggerConsole()`, Seq structured logging via custom `ZLoggerSeqSink` (channel-based CLEF batching). Scope data (CorrelationId, RequestPath, RequestMethod) flows from middleware through M.E.L `BeginScope` into the CLEF output. Global properties (Application, Environment, MachineName, RuntimeVersion) are computed once at startup and stamped into every CLEF event.

**Tech Stack:** ZLogger 2.5.10, Microsoft.Extensions.Logging, Seq (CLEF over HTTP)

**Spec:** `docs/solutions/architecture-patterns/unified-zlogger-logging-pipeline.md`

---

### Task 1: Enhance ZLoggerSeqSink — global properties + scope capture

This is the foundation — other tasks depend on the sink supporting global properties and scopes.

**Files:**
- Modify: `src/Infrastructure/XFramework.Integration/Logging/ZLoggerSeqSink.cs`

- [ ] **Step 1: Add global properties parameter to Register()**

Change the `Register` method signature to accept global properties and pass them through to the processor:

```csharp
public static void Register(
    ILoggingBuilder logging,
    string seqUrl,
    string? apiKey = null,
    LogLevel minimumLevel = LogLevel.Debug,
    string? applicationName = null,
    Dictionary<string, string>? globalProperties = null)
{
    logging.AddZLoggerLogProcessor(options =>
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(seqUrl.TrimEnd('/')) };
        if (!string.IsNullOrEmpty(apiKey))
            httpClient.DefaultRequestHeaders.Add("X-Seq-ApiKey", apiKey);

        return new SeqBatchProcessor(httpClient, minimumLevel, applicationName, globalProperties);
    });
}
```

Update the `SeqBatchProcessor` constructor to store global properties:

```csharp
private readonly Dictionary<string, string>? _globalProperties;

public SeqBatchProcessor(HttpClient httpClient, LogLevel minimumLevel, string? applicationName = null, Dictionary<string, string>? globalProperties = null)
{
    _httpClient = httpClient;
    _minimumLevel = minimumLevel;
    _applicationName = applicationName;
    _globalProperties = globalProperties;
    // ... rest unchanged
}
```

- [ ] **Step 2: Update FormatClef to write global properties**

In `FormatClef`, change the signature to accept global properties and write them after `Application`:

```csharp
private static string FormatClef(IZLoggerEntry entry, string? applicationName, Dictionary<string, string>? globalProperties)
```

After the existing `Application` write, add:

```csharp
if (globalProperties is not null)
{
    foreach (var (key, value) in globalProperties)
    {
        if (key != "Application") // already written above
            w.WriteString(key, value);
    }
}
```

Update the `Post` method call to pass global properties:

```csharp
public void Post(IZLoggerEntry entry)
{
    if (entry.LogInfo.LogLevel < _minimumLevel) return;
    var clef = FormatClef(entry, _applicationName, _globalProperties);
    _channel.Writer.TryWrite(clef);
}
```

- [ ] **Step 3: Add scope capture to FormatClef**

Add scope extraction after global properties but before structured params. ZLogger exposes scope data via `IZLoggerEntry.ScopeState` when `IncludeScopes` is enabled on the provider. The scope state is a linked list of scope objects.

Add this block in `FormatClef` between global properties and the paramProps loop:

```csharp
// Write scope properties (CorrelationId, RequestPath, etc.)
if (entry.ScopeState is not null)
{
    foreach (var scope in entry.ScopeState)
    {
        if (scope is IReadOnlyList<KeyValuePair<string, object?>> kvpList)
        {
            foreach (var kvp in kvpList)
            {
                if (kvp.Key == "{OriginalFormat}" || kvp.Value is null) continue;
                w.WriteString(kvp.Key, kvp.Value.ToString()!);
            }
        }
    }
}
```

Note: `entry.ScopeState` is only populated when `IncludeScopes = true` is set on the ZLogger processor options. This is enabled in Task 2.

- [ ] **Step 4: Build and verify compilation**

Run: `dotnet build src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/Logging/ZLoggerSeqSink.cs
git commit -m "feat(logging): add global properties and scope capture to ZLoggerSeqSink"
```

---

### Task 2: Update LoggingExtensions — global properties + IncludeScopes

**Files:**
- Modify: `src/Infrastructure/XFramework.Integration/Extensions/LoggingExtensions.cs`

- [ ] **Step 1: Build global properties dictionary and pass to Seq sink**

Replace the current `ZLoggerSeqSink.Register` call and add scope support to the console provider:

```csharp
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Logging;
using ZLogger;

namespace XFramework.Integration.Extensions;

public static class LoggingExtensions
{
    public static ILoggingBuilder AddXFrameworkLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Debug);

        // Console: plain text, Warning+ baseline
        logging.AddZLoggerConsole(options =>
        {
            options.IncludeScopes = true;
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"[{0} {1}] ", (in MessageTemplate template, in LogInfo info) =>
                    template.Format(info.Timestamp.Local.ToString("HH:mm:ss"), info.LogLevel));
            });
        });

        // Console filters: suppress framework noise, allow Bolt connection/RPC info
        logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>(level => level >= LogLevel.Warning);
        logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Microsoft.Hosting.Lifetime", LogLevel.Information);
        logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Bolt.Client", LogLevel.Information);

        // Seq: Debug+ for everything (if configured)
        var seqUrl = configuration["Seq:Url"];
        if (!string.IsNullOrEmpty(seqUrl))
        {
            var apiKey = configuration["Seq:ApiKey"];
            var appName = configuration["BoltConfiguration:ClientName"] ?? "Unknown";

            var globalProperties = new Dictionary<string, string>
            {
                ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                ["MachineName"] = Environment.MachineName,
                ["RuntimeVersion"] = Assembly.GetEntryAssembly()
                    ?.GetCustomAttribute<TargetFrameworkAttribute>()
                    ?.FrameworkName ?? "Unknown"
            };

            ZLoggerSeqSink.Register(logging, seqUrl, apiKey, LogLevel.Debug, appName, globalProperties);
        }

        return logging;
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/Extensions/LoggingExtensions.cs
git commit -m "feat(logging): pass global properties to Seq sink, enable scope capture"
```

---

### Task 3: Remove Serilog from XApplication bootstrap + Core

**Files:**
- Modify: `src/Kernel/XFramework.Core/Extensions/XApplication.cs`
- Modify: `src/Kernel/XFramework.Core/Extensions/InstallerExtensions.cs`
- Modify: `src/Kernel/XFramework.Core/GlobalUsings.cs`
- Delete: `src/Kernel/XFramework.Core/Loggers/ApplicationEnricher.cs`
- Modify: `src/Kernel/XFramework.Core/XFramework.Core.csproj`

- [ ] **Step 1: Remove UseSerilog() from XApplication.Configure<T>()**

In `src/Kernel/XFramework.Core/Extensions/XApplication.cs`, remove line 20 (`builder.Host.UseSerilog();`). The method becomes:

```csharp
public static WebApplicationBuilder Configure<T>()
{
    var builder = WebApplication.CreateBuilder();
    
    var configuration = builder.Configuration;
    var services = builder.Services;
    
    services.InstallServicesInAssembly<T>(configuration, builder.Environment);
    services.InstallSwagger(configuration);
    services.InstallOData(configuration);
    services.InstallJwt(configuration);
    services.InstallStandardServices<T>(configuration);
    services.InstallRuntimeServices(configuration);
    
    return builder;
}
```

- [ ] **Step 2: Remove Serilog config from InstallerExtensions**

In `src/Kernel/XFramework.Core/Extensions/InstallerExtensions.cs`:

Remove the `using Log = Serilog.Log;` alias at line 23.

In `InstallStandardServices<T>`, remove the Serilog configuration block (lines 93-104). The method becomes:

```csharp
public static void InstallStandardServices<T>(this IServiceCollection services, IConfiguration configuration)
{
    services.AddValidatorsFromAssembly(typeof(RequestBase).GetTypeInfo().Assembly);
    services.TryAddSingleton<IHelperService, HelperService>();
    services.TryAddSingleton<IJwtService, JwtService>();
    services.TryAddSingleton<CacheManager>();
    services.AddHttpClient();
    services.AddMemoryCache();
    services.AddAntiforgery();

    XFrameworkExtensions.LoadMapsterDefaults();
}
```

- [ ] **Step 3: Remove Serilog global usings**

In `src/Kernel/XFramework.Core/GlobalUsings.cs`, remove these three lines:

```
global using Serilog;
global using Serilog.Core;
global using Serilog.Events;
```

- [ ] **Step 4: Delete ApplicationEnricher.cs**

Delete `src/Kernel/XFramework.Core/Loggers/ApplicationEnricher.cs` entirely. Its useful properties (ApplicationName, Environment, MachineName, RuntimeVersion) are now global properties on the Seq sink (Task 2).

- [ ] **Step 5: Remove Serilog packages from XFramework.Core.csproj**

In `src/Kernel/XFramework.Core/XFramework.Core.csproj`, remove these 5 PackageReference lines:

```xml
<PackageReference Include="Serilog.AspNetCore" />
<PackageReference Include="Serilog.Enrichers.Span" />
<PackageReference Include="Serilog.Sinks.Async" />
<PackageReference Include="Serilog.Sinks.Console" />
<PackageReference Include="Serilog.Sinks.Seq" />
```

- [ ] **Step 6: Fix any remaining Serilog references in Core**

After removing the global usings, some files may fail to compile if they reference Serilog types. Search for and fix any remaining references:

Run: `grep -rn "Serilog\|LogEvent\|ILogEventEnricher\|LoggerConfiguration" src/Kernel/XFramework.Core/ --include="*.cs"`

Fix any remaining references by removing them (they should all be dead code after removing ApplicationEnricher and the InstallerExtensions block).

- [ ] **Step 7: Build Core project**

Run: `dotnet build src/Kernel/XFramework.Core/XFramework.Core.csproj`
Expected: Build succeeds

- [ ] **Step 8: Commit**

```bash
git add -A src/Kernel/XFramework.Core/
git commit -m "refactor(core): remove Serilog — ZLogger is the single logging pipeline"
```

---

### Task 4: Migrate CorrelationIdMiddleware to M.E.L scopes

**Files:**
- Modify: `src/Kernel/XFramework.Core/Middlewares/CorrelationIdMiddleware.cs`

- [ ] **Step 1: Replace Serilog LogContext with M.E.L BeginScope**

Replace the entire file contents with:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace XFramework.Core.Middlewares;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string CorrelationIdItemKey = "CorrelationId";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var correlationId = GetOrCreateCorrelationId(context);

        context.Items[CorrelationIdItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.Value ?? "",
            ["RequestMethod"] = context.Request.Method
        });

        await _next(context);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationIdFromHeader)
            && !string.IsNullOrWhiteSpace(correlationIdFromHeader))
        {
            return correlationIdFromHeader.ToString();
        }

        return Guid.NewGuid().ToString("D");
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    public static string? GetCorrelationId(this HttpContext context)
    {
        if (context?.Items.TryGetValue("CorrelationId", out var correlationId) == true)
        {
            return correlationId?.ToString();
        }

        return null;
    }
}
```

- [ ] **Step 2: Build Core**

Run: `dotnet build src/Kernel/XFramework.Core/XFramework.Core.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add src/Kernel/XFramework.Core/Middlewares/CorrelationIdMiddleware.cs
git commit -m "refactor(middleware): migrate CorrelationIdMiddleware from Serilog LogContext to M.E.L BeginScope"
```

---

### Task 5: Migrate Blazor WASM module from Serilog

**Files:**
- Modify: `src/Modules/XFramework.Blazor/Core/Features/BaseActionHandler.cs`
- Modify: `src/Modules/XFramework.Blazor/Components/Forms/XForm.razor`
- Modify: `src/Modules/XFramework.Blazor/Core/Extensions/InstallerExtensions.cs`
- Modify: `src/Modules/XFramework.Blazor/Core/Services/HandlerServices.cs`
- Modify: `src/Modules/XFramework.Blazor/XFramework.Blazor.csproj`

- [ ] **Step 1: Add ILoggerFactory to HandlerServices**

In `src/Modules/XFramework.Blazor/Core/Services/HandlerServices.cs`, add a logger factory parameter:

```csharp
using Microsoft.Extensions.Logging;

namespace XFramework.Blazor.Core.Services;

public record HandlerServices(
    IConfiguration Configuration,
    ISessionStorageService SessionStorageService,
    IHostEnvironment HostEnvironment,
    ILocalStorageService LocalStorageService,
    SweetAlertService SweetAlertService,
    NavigationManager NavigationManager,
    EndPointsModel EndPoints,
    IHttpClient HttpClient,
    HttpClient BaseHttpClient,
    IJSRuntime JsRuntime,
    IMediator Mediator,
    ISnackbar Snackbar,
    ILoggerFactory LoggerFactory
    );
```

- [ ] **Step 2: Migrate BaseActionHandler from Serilog to ILogger**

In `src/Modules/XFramework.Blazor/Core/Features/BaseActionHandler.cs`:

Remove `using Serilog;` (line 2).

Add a logger field to `BaseStateActionHandler`:

```csharp
protected ILogger Logger { get; set; } = null!;
```

In the three constructor-based subclasses (`StateActionHandler<TAction>`, `EventHandler<TAction>`, `StateActionHandler<TAction, TResponse>`), add after the existing assignments:

```csharp
Logger = handlerServices.LoggerFactory.CreateLogger(GetType());
```

Replace all `Log.Error(...)` and `Log.Information(...)` calls:

| Line | Old | New |
|------|-----|-----|
| 46 | `Log.Error("Error from response: {Message}", message);` | `Logger.LogError("Error from response: {Message}", message);` |
| 74 | `Log.Error("Error from response: {Message}", message);` | `Logger.LogError("Error from response: {Message}", message);` |
| 117 | `Log.Error("Error from response: {Message}", response.Message);` | `Logger.LogError("Error from response: {Message}", response.Message);` |
| 155 | `Log.Error("Error from response: {Message}", response.Message);` | `Logger.LogError("Error from response: {Message}", response.Message);` |
| 193 | `Log.Error("Error from response: {Message}", response.Message);` | `Logger.LogError("Error from response: {Message}", response.Message);` |
| 372 | `Log.Information(title);` | `Logger.LogInformation("{Title}", title);` |

- [ ] **Step 3: Migrate XForm.razor from Serilog to ILogger**

In `src/Modules/XFramework.Blazor/Components/Forms/XForm.razor`:

Remove `@using Serilog` (line 3). Add logger injection:

```razor
@inject ILogger<XForm<TItem>> Logger
```

Replace Log calls:

| Line | Old | New |
|------|-----|-----|
| 78 | `Log.Warning("On InternalSubmit..");` | `Logger.LogWarning("On InternalSubmit..");` |
| 84 | `Log.Warning("EditContext is valid");` | `Logger.LogWarning("EditContext is valid");` |
| 87 | `Log.Warning("OnValidSubmit has delegate");` | `Logger.LogWarning("OnValidSubmit has delegate");` |

- [ ] **Step 4: Remove AddSerilog() from Blazor InstallerExtensions**

In `src/Modules/XFramework.Blazor/Core/Extensions/InstallerExtensions.cs`, remove `using Serilog;` (line 1) and delete the entire `AddSerilog` method (lines 17-25). The file becomes:

```csharp
using XFramework.Domain.Shared.Extensions;

namespace XFramework.Blazor.Core.Extensions;

public static class InstallerExtensions
{
    public static void InstallBlazorBaseServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton(o => new DeviceAgentProvider(Environment.MachineName));
        services.AddScoped<HandlerServices>();
        
        services.InstallServicesInAssembly<XFramework.Blazor.Base>(configuration, hostEnvironment);
    }
}
```

- [ ] **Step 5: Remove Serilog package from Blazor csproj**

In `src/Modules/XFramework.Blazor/XFramework.Blazor.csproj`, remove:

```xml
<PackageReference Include="Serilog.Sinks.BrowserHttp" />
```

- [ ] **Step 6: Build Blazor module**

Run: `dotnet build src/Modules/XFramework.Blazor/XFramework.Blazor.csproj`
Expected: Build succeeds

- [ ] **Step 7: Commit**

```bash
git add src/Modules/XFramework.Blazor/
git commit -m "refactor(blazor): migrate from Serilog static Log to ILogger<T>"
```

---

### Task 6: Service appsettings cleanup — remove Serilog, add Seq

**Files:**
- Modify: `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.json` — remove Serilog block (lines 9-34)
- Modify: `src/Modules/XFramework.Wallets/Wallets.Api/appsettings.json` — remove Serilog block (lines 9-34)
- Modify: `src/Modules/XFramework.Messaging/Messaging.Api/appsettings.json` — remove Serilog block (lines 9-34)
- Modify: `src/Modules/XFramework.SmsGateway/SmsGateway.Api/appsettings.json` — remove Serilog block (lines 9-34)
- Modify: `src/Modules/XFramework.Community/Community.Api/appsettings.json` — remove Serilog block (lines 9-34)
- Modify: `src/Modules/XFramework.Inventario/Inventario.Api/appsettings.json` — remove Serilog block (lines 9-34)
- Modify: `src/Modules/XFramework.Coins/Server/Coins.Api/appsettings.json` — remove Serilog block (lines 7-32)
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.json` — remove Serilog block (lines 9-34)
- Modify: 8 `appsettings.Development.json` files — add Seq URL

- [ ] **Step 1: Remove Serilog blocks from all service appsettings.json**

For each service, remove the entire `"Serilog": { ... }` JSON block from `appsettings.json`. The `"Logging"` block already exists in each file and is what `AddXFrameworkLogging` reads.

- [ ] **Step 2: Add Seq config to all appsettings.Development.json**

Add the following to each service's `appsettings.Development.json` (if not already present):

```json
"Seq": {
  "Url": "http://100.75.11.49:5341"
}
```

Services to update:
1. `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.Development.json`
2. `src/Modules/XFramework.Wallets/Wallets.Api/appsettings.Development.json`
3. `src/Modules/XFramework.Messaging/Messaging.Api/appsettings.Development.json`
4. `src/Modules/XFramework.SmsGateway/SmsGateway.Api/appsettings.Development.json`
5. `src/Modules/XFramework.Community/Community.Api/appsettings.Development.json`
6. `src/Modules/XFramework.Inventario/Inventario.Api/appsettings.Development.json`
7. `src/Modules/XFramework.Coins/Server/Coins.Api/appsettings.Development.json`
8. `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.Development.json`

- [ ] **Step 3: Add AddXFrameworkLogging to Coins.Api**

In `src/Modules/XFramework.Coins/Server/Coins.Api/Program.cs`, add after `var builder = XApplication.Configure<Program>();`:

```csharp
builder.Logging.AddXFrameworkLogging(builder.Configuration);
```

Also add the using if not present:

```csharp
using XFramework.Integration.Extensions;
```

- [ ] **Step 4: Commit**

```bash
git add src/Modules/
git commit -m "chore(config): remove Serilog config, add Seq URL to all service appsettings"
```

---

### Task 7: Update test infrastructure — Serilog config keys

**Files:**
- Modify: `src/Tests/XFramework.TestInfrastructure/BoltTestHelper.cs`
- Modify: `src/Tests/IdentityServer.IntegrationTests/Infrastructure/IntegrationTestFixture.cs`
- Modify: `src/Tests/Wallets.IntegrationTests/Infrastructure/WalletsTestFixture.cs`
- Modify: `src/Tests/IdentityServer.Benchmarks/ConcurrentBenchmarks.cs`
- Modify: `src/Tests/IdentityServer.Benchmarks/ThroughputBenchmarks.cs`
- Modify: `src/Tests/IdentityServer.Benchmarks/TransportBenchmarks.cs`

- [ ] **Step 1: Replace Serilog config keys in all test files**

In every file listed above, replace all occurrences of:

```csharp
["Serilog:MinimumLevel:Default"] = "Warning"
```

with:

```csharp
["Logging:LogLevel:Default"] = "Warning"
```

And replace:

```csharp
["Serilog:MinimumLevel:Default"] = "Error"
```

with:

```csharp
["Logging:LogLevel:Default"] = "Error"
```

Exact locations:

| File | Line | Old Value | New Value |
|------|------|-----------|-----------|
| `BoltTestHelper.cs` | 31 | `["Serilog:MinimumLevel:Default"] = "Warning"` | `["Logging:LogLevel:Default"] = "Warning"` |
| `BoltTestHelper.cs` | 61 | `["Serilog:MinimumLevel:Default"] = "Warning",` | `["Logging:LogLevel:Default"] = "Warning",` |
| `BoltTestHelper.cs` | 172 | `["Serilog:MinimumLevel:Default"] = "Warning"` | `["Logging:LogLevel:Default"] = "Warning"` |
| `IntegrationTestFixture.cs` | 192 | `["Serilog:MinimumLevel:Default"] = "Warning",` | `["Logging:LogLevel:Default"] = "Warning",` |
| `IntegrationTestFixture.cs` | 259 | `["Serilog:MinimumLevel:Default"] = "Warning"` | `["Logging:LogLevel:Default"] = "Warning"` |
| `WalletsTestFixture.cs` | 135 | `["Serilog:MinimumLevel:Default"] = "Warning",` | `["Logging:LogLevel:Default"] = "Warning",` |
| `WalletsTestFixture.cs` | 183 | `["Serilog:MinimumLevel:Default"] = "Warning"` | `["Logging:LogLevel:Default"] = "Warning"` |
| `ConcurrentBenchmarks.cs` | 135 | `["Serilog:MinimumLevel:Default"] = "Error",` | `["Logging:LogLevel:Default"] = "Error",` |
| `ConcurrentBenchmarks.cs` | 324 | `["Serilog:MinimumLevel:Default"] = "Error"` | `["Logging:LogLevel:Default"] = "Error"` |
| `ThroughputBenchmarks.cs` | 288 | `["Serilog:MinimumLevel:Default"] = "Error"` | `["Logging:LogLevel:Default"] = "Error"` |
| `TransportBenchmarks.cs` | 389 | `["Serilog:MinimumLevel:Default"] = "Error",` | `["Logging:LogLevel:Default"] = "Error",` |
| `TransportBenchmarks.cs` | 440 | `["Serilog:MinimumLevel:Default"] = "Error"` | `["Logging:LogLevel:Default"] = "Error"` |

- [ ] **Step 2: Commit**

```bash
git add src/Tests/
git commit -m "chore(tests): replace Serilog config keys with M.E.L Logging:LogLevel"
```

---

### Task 8: Remove Serilog from Directory.Packages.props

**Files:**
- Modify: `Directory.Packages.props`

- [ ] **Step 1: Remove all Serilog package versions**

In `Directory.Packages.props`, remove these lines (73-80 area, under `<!-- Logging -->`):

```xml
<PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageVersion Include="Serilog.Enrichers.Span" Version="3.1.0" />
<PackageVersion Include="Serilog.Sinks.Async" Version="2.1.0" />
<PackageVersion Include="Serilog.Sinks.BrowserHttp" Version="1.0.0-dev-00032" />
<PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
<PackageVersion Include="Serilog.Sinks.Seq" Version="8.0.0" />
```

Keep the `<!-- Logging -->` comment and the `ZLogger` line.

- [ ] **Step 2: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore(deps): remove all Serilog packages from central package management"
```

---

### Task 9: Full solution build + verification

- [ ] **Step 1: Build entire solution**

Run: `dotnet build XFramework.slnx`
Expected: Build succeeds with no Serilog-related errors.

If there are build errors, they will be from files that still reference Serilog types. Fix each one:
- If it's a `using Serilog` → remove the using
- If it's a `Log.Something()` → replace with `ILogger<T>` injection + `logger.LogSomething()`
- If it's an `ILogEventEnricher` reference → delete the file

- [ ] **Step 2: Verify no Serilog references remain**

Run: `grep -rn "using Serilog" src/ --include="*.cs" --include="*.razor"`
Expected: No matches (zero results).

Run: `grep -rn "Serilog" src/ --include="*.csproj"`
Expected: No matches.

- [ ] **Step 3: Commit any remaining fixes**

```bash
git add -A
git commit -m "fix: resolve remaining Serilog references after migration"
```

(Skip this step if the build in Step 1 succeeded with no changes needed.)
