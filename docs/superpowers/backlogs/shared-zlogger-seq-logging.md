# Backlog: Extract ZLogger + Seq Logging to Shared Infrastructure

**Created:** 2026-04-23
**Priority:** Medium
**Status:** Ready

## Context

The Control Panel (`ControlPanel.Server`) has a working ZLogger + Seq logging setup:
- **ZLogger** for zero-alloc structured logging via `Microsoft.Extensions.Logging`
- **Seq** for centralized log aggregation (running at `http://100.75.11.49:5341`)
- **ZLoggerSeqSink** — custom async batching processor that POSTs CLEF to Seq via `Channel<string>` with bounded backpressure
- **BoltDriver** logs every RPC call with structured `Request`/`Response` properties (JSON bodies, status, elapsed, sizes)
- Console output is clean (Warning+ only, Bolt noise suppressed)

This currently lives in:
- `src/Presentation/ControlPanel.Server/Services/ZLoggerSeqSink.cs` — the Seq batching sink
- `src/Presentation/ControlPanel.Server/Program.cs` — logging configuration

## Goal

Move the logging infrastructure to `XFramework.Integration` (or a new `XFramework.Logging` project) so all microservices can use it with a single line:

```csharp
builder.Services.AddXFrameworkLogging(builder.Configuration);
```

## What to Do

### 1. Add ZLogger to shared infrastructure

- Add `ZLogger` package reference to `XFramework.Integration.csproj`
- Move `ZLoggerSeqSink.cs` to `XFramework.Integration/Logging/ZLoggerSeqSink.cs`

### 2. Create `AddXFrameworkLogging` extension method

Location: `XFramework.Integration/Extensions/LoggingExtensions.cs`

```csharp
public static class LoggingExtensions
{
    public static ILoggingBuilder AddXFrameworkLogging(
        this ILoggingBuilder logging, 
        IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Debug);

        // Console: plain text, Warning+ only
        logging.AddZLoggerConsole(options => { /* plain text formatter */ });
        logging.AddFilter<ZLoggerConsoleLoggerProvider>("Microsoft", LogLevel.Warning);
        logging.AddFilter<ZLoggerConsoleLoggerProvider>("System", LogLevel.Warning);
        logging.AddFilter<ZLoggerConsoleLoggerProvider>("XFramework.Integration", LogLevel.None);
        logging.AddFilter<ZLoggerConsoleLoggerProvider>("Bolt", LogLevel.None);
        logging.AddFilter<ZLoggerConsoleLoggerProvider>("Microsoft.Hosting.Lifetime", LogLevel.Information);

        // Seq: Debug+ with full structured logging
        var seqUrl = configuration["Seq:Url"];
        if (!string.IsNullOrEmpty(seqUrl))
        {
            var apiKey = configuration["Seq:ApiKey"];
            ZLoggerSeqSink.Register(logging, seqUrl, apiKey, LogLevel.Debug);
        }

        return logging;
    }
}
```

### 3. Update each service's Program.cs

Replace existing Serilog/console logging setup with:
```csharp
builder.Logging.AddXFrameworkLogging(builder.Configuration);
```

**Services to update:**
- `IdentityServer.Api/Program.cs`
- `Wallets.Api/Program.cs`
- `Messaging.Api/Program.cs`
- `SmsGateway.Api/Program.cs`
- `Bolt.Hub/Program.cs`
- `ControlPanel.Server/Program.cs` (simplify — already has the pattern)

### 4. Add `Seq:Url` to each service's appsettings

```json
{
  "Seq": {
    "Url": "http://seq:80"
  }
}
```

Docker services use `http://seq:80` (internal network). Local dev uses `http://100.75.11.49:5341`.

### 5. Update docker-compose.yml

Seq container is already added. Just ensure services have network access to it (they already do — same Docker network).

### 6. Consider removing Serilog

Once all services use ZLogger, the Serilog packages can be removed from `Directory.Packages.props`:
- `Serilog`
- `Serilog.AspNetCore`
- `Serilog.Enrichers.Span`
- `Serilog.Extensions.Logging`
- `Serilog.Sinks.Async`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.Seq`

Keep `Serilog.Sinks.BrowserHttp` if still used by Blazor WASM.

## Reference Implementation

The working implementation is in:
- `src/Presentation/ControlPanel.Server/Services/ZLoggerSeqSink.cs` — batched Seq sink
- `src/Presentation/ControlPanel.Server/Program.cs` — logging config (lines 11-28)
- `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs` — structured RPC logging

## ZLoggerSeqSink Key Design Decisions

- **Channel-based batching**: `BoundedChannel<string>(10_000, DropOldest)` — single reader, multi-writer
- **Batch size**: Up to 100 entries per HTTP POST (newline-delimited CLEF)
- **Flush interval**: ~500ms max latency (waits for channel, not timer-based)
- **Backpressure**: Drop oldest when channel full (prefer fresh logs over stale)
- **Graceful shutdown**: Drains remaining entries on `DisposeAsync`
- **CLEF format**: `@mt` stripped of inline JSON blobs, properties written via `WriteJsonParameterKeyValues`

## Acceptance Criteria

- [ ] All services log to Seq with structured properties
- [ ] Console output is clean across all services (Warning+ only)
- [ ] BoltDriver RPC traces visible in Seq for all services (not just Control Panel)
- [ ] Serilog dependencies removed where no longer needed
- [ ] No performance regression — ZLogger + batched sink should be faster than Serilog
