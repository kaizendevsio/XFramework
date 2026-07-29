# XFramework

A modular .NET 10 enterprise framework with **Bolt**, a custom binary RPC and streaming protocol.

## Bolt Protocol

Bolt is a lightweight binary protocol for .NET-to-.NET and server-to-client communication. It supports RPC (request-response), fire-and-forget push, and bidirectional byte streaming over WebSockets.

### Features

| Feature | Description |
|---------|-------------|
| **RPC** | Request-response with pooled completion sources |
| **Streaming** | Bidirectional `IAsyncEnumerable<T>` streaming for video, audio, files, any bytes |
| **Hub Routing** | Raw-frame forwarding without payload deserialization |
| **Connection Pooling** | Auto-scales WebSocket connections under load, round-robin dispatch |
| **Typed Serialization** | MemoryPack auto-serialization for both RPC and stream payloads |
| **Resilience** | Exponential backoff reconnection, offline queue, dead letter queue |

### Wire Protocol

```
RPC Request:   [1:type] [16:requestId] [4:recipientHash] [4:senderHash] [4:commandHash] [4:payloadLen] [payload] = 33B header
RPC Response:  [1:type] [16:requestId] [2:statusCode] [4:payloadLen] [payload]                      = 23B header
Stream Open:   [1:type] [16:streamId]  [4:recipientHash] [4:commandHash]                            = 25B header
Stream Data:   [1:type] [16:streamId]  [4:payloadLen] [payload]                                     = 21B header
Stream Close:  [1:type] [16:streamId]  [2:statusCode]                                               = 19B header
```

### Streaming API

Bolt supports three streaming patterns:

**Raw bytes** — stream any binary data:
```csharp
var stream = await client.OpenStreamAsync("video-service", "upload");
await stream.SendAsync(jpegBytes);
await stream.SendAsync(moreBytes);
await stream.CloseAsync();
```

**Typed objects** — auto-serialized with MemoryPack:
```csharp
await stream.SendAsync<VideoFrame>(frame);

await foreach (var frame in stream.ReadAllAsync<VideoFrame>())
    ProcessFrame(frame);
```

**IAsyncEnumerable pipe** — plug any async producer directly into a stream:
```csharp
// Sender: pipe a producer into the stream
await client.StreamAsync("video-service", "process",
    GetVideoFramesAsync(), ct);  // IAsyncEnumerable<VideoFrame>

// Receiver: typed handler with IAsyncEnumerable
client.RegisterStreamHandler<VideoFrame>("process",
    async (frames, stream) =>
    {
        await foreach (var frame in frames)
            await ProcessFrameAsync(frame);
    });
```

## Performance

Bolt is designed around compact binary frames, pooled request completion, raw-frame Hub routing, and configurable connection pooling. Performance depends on workload, topology, transport configuration, payload serialization, and deployment environment.

The checked-in benchmarks are local regression and engineering tools. They do not establish that Bolt is universally faster or more memory-efficient than gRPC, HTTP, or SignalR. Comparative results should only be published from equivalent workloads and connection counts, with response validation, request-level latency distributions, per-process memory measurements, and reproducible environment details.

## Architecture

### Module Structure
```
src/Modules/XFramework.{Module}/
  {Module}.Api/              # ASP.NET Web API (VSA endpoints, services)
  {Module}.Domain.Shared/    # Shared contracts (requests, responses, entities)
  {Module}.Integration/      # Generated service wrappers for cross-module calls
```

### Key Technologies

- **.NET 10 / C# 14** with Vertical Slice Architecture (VSA)
- **Bolt Protocol** — custom binary RPC + streaming over WebSocket
- **MemoryPack** — compact binary serialization for payloads
- **Source Generators** - compile-time code generation for endpoints, Bolt handlers, and service wrappers
- **Entity Framework Core** with PostgreSQL
- **FluentValidation** — auto-discovered validators
- **Mapster** — compile-time object mapping
- **Testcontainers** — integration tests with real PostgreSQL

### Generated Endpoint Registration

Feature endpoints use source-generated registration from a single handler method:

```csharp
public static class HealthCheckEndpoint
{
    [BoltHandler]                                    // Generates Bolt handler
    [MapPost("/api/health/check", Tags = ["Health"])] // Generates REST endpoint
    public static Task<Result<HealthCheckResponse>> Handle(
        HealthCheckRequest request, CancellationToken ct)
    {
        // Business logic here
    }
}
```

### Modules

| Module | Description |
|--------|-------------|
| **IdentityServer** | Authentication, authorization, credentials, sessions |
| **Wallets** | Financial operations, transfers, transaction reversal |
| **Communications** | Message delivery, templates, contacts |
| **Community** | Social features, connections, content |
| **Inventario** | Product/inventory management |
| **Bolt Hub** | Bolt protocol hub + message routing |

## Getting Started

```bash
git clone https://github.com/kaizendevsio/XFramework.git
cd XFramework
dotnet build XFramework.slnx
```

## Documentation And Agent Guidance

- Start with [`docs/README.md`](docs/README.md) for the repository documentation map.
- Use [`docs/solutions/README.md`](docs/solutions/README.md) for the current solution knowledgebase.
- For agent-oriented coding guidance, use [`AGENTS.md`](AGENTS.md) and [`CLAUDE.md`](CLAUDE.md); they route to current VSA, Bolt, source-generator, data-access, caching, logging, and testing docs.
- Historical root markdown and old plan files are project memory only unless a current `docs/solutions/` document points to them for context.

### Running Tests

```bash
# Set Docker host for Testcontainers
export DOCKER_HOST=tcp://your-docker-host:2375
export TESTCONTAINERS_HOST_OVERRIDE=your-docker-host

# Integration tests
dotnet test src/Tests/IdentityServer.IntegrationTests/
dotnet test src/Tests/Wallets.IntegrationTests/

# Benchmarks
dotnet run --project src/Tests/IdentityServer.Benchmarks/ -c Release
```

## Running Benchmarks

### Standalone Bolt Benchmarks (no database required)

Local in-process transport benchmarks for Bolt, gRPC, and SignalR. Direct and Hub paths are reported separately, use one caller connection/channel, and validate returned values. Concurrent results measure batch completion time, not individual request latency; allocation columns include the in-process benchmark topology and are regression signals rather than client-only memory measurements.

```bash
cd src/Tests/Bolt.Tests

# All benchmarks (interactive menu)
dotnet run -c Release

# Single-request and concurrent-batch workloads (1 and 64 requests)
dotnet run -c Release -- --filter "*BoltBenchmarks*"

# Concurrent batch throughput (100 validated requests)
dotnet run -c Release -- --filter "*Throughput*"

# Single transport only
dotnet run -c Release -- --filter "*Bolt_Direct*"
dotnet run -c Release -- --filter "*Bolt_Hub*"
dotnet run -c Release -- --filter "*GRPC_Direct*"
dotnet run -c Release -- --filter "*GRPC_Hub*"
dotnet run -c Release -- --filter "*SignalR*"
```

### XFramework Integrated Benchmarks (requires Docker for PostgreSQL)

Path-level benchmarks using the XFramework HealthCheck workflow. These require Docker and are not equivalent cross-protocol microbenchmarks: the HTTP, generated Bolt wrapper, thin Bolt, and benchmark-only gRPC paths execute different application stacks. Compare a path only against its own previous runs.

```bash
cd src/Tests/IdentityServer.Benchmarks

# Set Docker host for Testcontainers
export DOCKER_HOST=tcp://your-docker-host:2375
export TESTCONTAINERS_HOST_OVERRIDE=your-docker-host

# Single-request path benchmarks
dotnet run -c Release -- --filter "*TransportBenchmarks*"

# Concurrent batches (1, 16, 64 requests)
dotnet run -c Release -- --filter "*Concurrent*"

# Validated 100-request batch throughput
dotnet run -c Release -- --filter "*Throughput*"
```

Results are saved to `BenchmarkDotNet.Artifacts/` as markdown, HTML, and CSV.

## License

Proprietary. All rights reserved.
