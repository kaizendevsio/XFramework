# XFramework

A modular .NET 10 enterprise framework with **Bolt** — a custom binary RPC protocol that's **47% faster than gRPC** with **90% less memory** and **40,000+ ops/sec peak throughput**.

## Bolt Protocol Performance

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
- **Bolt Protocol** — custom binary RPC over WebSocket (replaces SignalR)
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

```bash
# Sequential (all transports)
dotnet run -c Release -- --filter "*TransportBenchmarks*"

# Concurrent load (1, 16, 64 parallel)
dotnet run -c Release -- --filter "*Concurrent*"

# Max throughput
dotnet run -c Release -- --filter "*Throughput*"
```

## License

Proprietary. All rights reserved.
