# XFramework

A modular .NET 10 enterprise framework with **Bolt** — a custom binary RPC and streaming protocol that's **47% faster than gRPC** with **90% less memory** and **40,000+ ops/sec peak throughput**.

## Bolt Protocol

Bolt is a lightweight binary protocol for .NET-to-.NET and server-to-client communication. It supports RPC (request-response), fire-and-forget push, and bidirectional byte streaming — all through a single WebSocket connection with zero-copy hub routing.

### Features

| Feature | Description |
|---------|-------------|
| **RPC** | Request-response with pooled completion sources, sub-3KB per call |
| **Streaming** | Bidirectional `IAsyncEnumerable<T>` streaming for video, audio, files, any bytes |
| **Hub Routing** | Zero-copy frame forwarding — hub reads 29-byte header, forwards raw bytes |
| **Connection Pooling** | Auto-scales WebSocket connections under load, round-robin dispatch |
| **Typed Serialization** | MemoryPack auto-serialization for both RPC and stream payloads |
| **Resilience** | Exponential backoff reconnection, offline queue, dead letter queue |
| **Zero GC** | No Gen0 collections under any load level |

### Wire Protocol

```
RPC Request:   [1:type] [16:requestId] [4:recipientHash] [4:commandHash] [4:payloadLen] [payload]  = 29B header
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

Benchmarked on .NET 10, Windows 11, Hyper-V. All transports routed through a hub for fair comparison (Client -> Hub -> Service -> Hub -> Client).

### Sequential (single RPC)

| Transport | Latency | Throughput | Memory/req | vs Bolt |
|-----------|---------|------------|------------|---------|
| HTTP (REST) | 264.9 us | 3,775 ops/s | 58.25 KB | 2.0x slower |
| SignalR | 254.7 us | 3,926 ops/s | 21.48 KB | 1.9x slower |
| gRPC | 254.7 us | 3,927 ops/s | 20.52 KB | 1.9x slower |
| QUIC (direct) | 200.0 us | 5,000 ops/s | 26.06 KB | 1.5x slower |
| **Bolt** | **133.7 us** | **7,481 ops/s** | **2.20 KB** | **baseline** |

### Concurrent (64 parallel RPCs)

| Transport | Latency | Throughput | Memory | vs Bolt |
|-----------|---------|------------|--------|---------|
| HTTP | 2,272 us | 440 ops/s | 3,045 KB | 18x more memory |
| SignalR | 14,280 us | 70 ops/s | 1,370 KB | collapses under load |
| gRPC | 1,460 us | 685 ops/s | 622 KB | 5x more memory |
| **Bolt** | **1,939 us** | **516 ops/s** | **124 KB** | **baseline** |

### Max Throughput (100 concurrent, saturated)

| Transport | Per-op Latency | Peak Throughput | Memory/op | vs Bolt |
|-----------|---------------|-----------------|-----------|---------|
| HTTP | 35.35 us | 28,289 ops/s | 47.46 KB | 30% slower |
| gRPC | 26.31 us | 38,006 ops/s | 21.06 KB | 6% slower |
| **Bolt** | **24.75 us** | **40,412 ops/s** | **2.12 KB** | **baseline** |

### Bolt vs gRPC — Head to Head

| Metric | Bolt | gRPC | Winner |
|--------|------|------|--------|
| Sequential latency | 133.7 us | 254.7 us | Bolt by 47% |
| Sequential memory | 2.20 KB | 20.52 KB | Bolt by 89% |
| Peak throughput | 40,412 ops/s | 38,006 ops/s | Bolt by 6% |
| Throughput memory | 2.12 KB | 21.06 KB | Bolt by 90% |
| GC pressure | Zero | Zero | Tie |

### Why Bolt is faster than gRPC

Both benchmarks use the same hub architecture (Client -> Hub -> Service). gRPC's overhead comes from:
- HTTP/2 HPACK header compression/decompression at each hop
- Protobuf encode/decode at each hop (hub must re-serialize)
- HTTP/2 stream framing overhead
- gRPC status/trailer processing

Bolt eliminates all of this:
- 29-byte binary header + MemoryPack payload (no HTTP overhead)
- Hub forwards raw bytes without decoding (zero-copy routing)
- FNV-1a hash routing (4-byte int comparison, no string matching)
- Connection pooling with round-robin dispatch under load
- Non-blocking handler dispatch for concurrent throughput
- Zero GC pressure across all concurrency levels

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
- **MemoryPack** — zero-allocation binary serialization for payloads
- **Source Generators** — compile-time code generation for endpoints, handlers, and service wrappers
- **Entity Framework Core** with PostgreSQL
- **FluentValidation** — auto-discovered validators
- **Mapster** — compile-time object mapping
- **Testcontainers** — integration tests with real PostgreSQL

### Dual Transport

Every endpoint supports two transports from a single method:

```csharp
public static class HealthCheckEndpoint
{
    [StreamFlowHandler]                              // Generates Bolt handler
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
| **Messaging** | Message delivery, templates, contacts |
| **Community** | Social features, connections, content |
| **Inventario** | Product/inventory management |
| **StreamFlow** | Bolt protocol hub + message routing |

## Getting Started

```bash
git clone https://github.com/kaizendevsio/XFramework.git
cd XFramework
dotnet build XFramework.slnx
```

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

Pure protocol benchmark — Bolt vs gRPC vs SignalR, all returning a simple "Hello" string.

```bash
cd src/Tests/Bolt.Tests

# All benchmarks (interactive menu)
dotnet run -c Release

# Sequential + concurrent (1 and 64 parallel)
dotnet run -c Release -- --filter "*BoltBenchmarks*"

# Max throughput (100 concurrent batch, peak ops/sec)
dotnet run -c Release -- --filter "*Throughput*"

# Single transport only
dotnet run -c Release -- --filter "*Bolt_Direct*"
dotnet run -c Release -- --filter "*Bolt_Hub*"
dotnet run -c Release -- --filter "*GRPC_Direct*"
dotnet run -c Release -- --filter "*GRPC_Hub*"
dotnet run -c Release -- --filter "*SignalR*"
```

### XFramework Integrated Benchmarks (requires Docker for PostgreSQL)

Tests with a real HealthCheck endpoint through the full XFramework stack.

```bash
cd src/Tests/IdentityServer.Benchmarks

# Set Docker host for Testcontainers
export DOCKER_HOST=tcp://your-docker-host:2375
export TESTCONTAINERS_HOST_OVERRIDE=your-docker-host

# Sequential (all 5 transports)
dotnet run -c Release -- --filter "*TransportBenchmarks*"

# Concurrent load (1, 16, 64 parallel)
dotnet run -c Release -- --filter "*Concurrent*"

# Max throughput
dotnet run -c Release -- --filter "*Throughput*"
```

Results are saved to `BenchmarkDotNet.Artifacts/` as markdown, HTML, and CSV.

## License

Proprietary. All rights reserved.
