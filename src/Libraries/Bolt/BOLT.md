# Bolt

A compact binary RPC and streaming protocol for .NET with Hub routing, bidirectional calls, and streaming support.

## Why Bolt?

Bolt was built to answer a simple question: *what if we stripped away every layer of overhead between two .NET services and just sent raw bytes?*

The result is a protocol that:
- Uses a compact **33-byte request header** instead of HTTP/2 frames, HPACK headers, and Protobuf encoding
- Routes messages through a hub by reading only the header — **the payload is never decoded** during routing
- Scales via **connection pooling** — multiple WebSocket connections per client, distributed round-robin

## Performance

The tables below are retained only as historical local microbenchmark observations. They are not an equivalent or production-grade comparison: the Bolt and gRPC cases use different connection counts and response-processing work, concurrent batch results are not request-level latency measurements, and the tests run in-process over plaintext localhost. They must not be cited as evidence that Bolt is universally faster or more memory-efficient than gRPC or SignalR.

Current benchmarks are useful for regression detection within a fixed workload. Performance certification requires equivalent typed and raw work, tuned transports, TLS or mTLS, request-level latency histograms, open- and closed-loop load, allocations and retained memory, network impairment, failure injection, and reproducible raw artifacts.

"Hub" means the message is routed through a central server (Client -> Hub -> Service -> Hub -> Client). "Direct" means the client connects straight to the service.

### Sequential Latency (single request)

| Transport | Latency | Ops/sec | Memory/req |
|-----------|---------|---------|------------|
| **Bolt Direct** | **71 us** | **14,014** | **1.24 KB** |
| **Bolt Hub** | **121 us** | **8,233** | **1.55 KB** |
| gRPC Direct | 136 us | 7,343 | 8.64 KB |
| SignalR Hub | 159 us | 6,296 | 5.94 KB |
| gRPC Hub | 279 us | 3,587 | 19.71 KB |

### Concurrent Load (64 parallel requests)

| Transport | Latency | Ops/sec | Memory |
|-----------|---------|---------|--------|
| **Bolt Hub** | **1,329 us** | **752** | **88 KB** |
| gRPC Direct | 1,396 us | 716 | 584 KB |
| Bolt Direct | 1,513 us | 661 | 68 KB |
| gRPC Hub | 1,679 us | 595 | 1,304 KB |
| SignalR Hub | 5,129 us | 195 | 378 KB |

### Peak Throughput (100 concurrent batch)

| Transport | Per-op Latency | Peak Ops/sec | Memory/op |
|-----------|---------------|-------------|-----------|
| **Bolt Direct** | **12.7 us** | **78,709** | **978 B** |
| **Bolt Hub** | **16.7 us** | **60,014** | **1,313 B** |
| gRPC Hub | 17.4 us | 57,515 | 20,780 B |
| gRPC Direct | 17.3 us | 57,659 | 9,268 B |
| SignalR Hub | 68.5 us | 14,589 | 5,956 B |

### Scalability (many concurrent clients)

Each Bolt client uses 2 WebSocket connections. Each gRPC client uses 1 HTTP/2 channel.

| Clients | Bolt Latency | Bolt Memory | gRPC Latency | gRPC Memory |
|---------|-------------|-------------|-------------|-------------|
| 10 | 425 us | 12.6 KB | 855 us | 198 KB |
| 50 | 1,148 us | 64 KB | 2,523 us | 1,002 KB |
| 100 | 2,131 us | 126 KB | 4,869 us | 2,001 KB |

This historical result excludes important setup and retained-memory costs and is not a general scalability claim.

## Historical Bolt and gRPC Observations

This table summarizes the same non-equivalent localhost run and does not identify a universal winner.

| Metric | Bolt | gRPC | Historical local delta |
|--------|------|------|--------|
| Sequential latency (hub) | 121 us | 279 us | Bolt by 57% |
| Concurrent latency (hub) | 1,329 us | 1,679 us | Bolt by 21% |
| Peak throughput (hub) | 60,014 ops/s | 57,515 ops/s | Bolt by 4% |
| Peak throughput (direct) | 78,709 ops/s | 57,659 ops/s | Bolt by 37% |
| Memory per request | 1.3 KB | 20 KB | Bolt by 94% |
| Memory at 100 clients | 126 KB | 2,001 KB | Bolt by 94% |
| GC pressure | Allocations observed | Allocations observed | Not established |
| Streaming | IAsyncEnumerable | IAsyncEnumerable | Tie |
| Browser support | WebSocket (native) | gRPC-Web (proxy) | Bolt |
| Serialization | MemoryPack (binary) | Protobuf (binary) | Workload-dependent |
| Schema required | No | Yes (.proto) | Bolt (simpler) |
| Hub routing | Built-in | Not built-in | Bolt |

## How It Works

### Wire Protocol

Every Bolt frame starts with a 1-byte type followed by a fixed-size header. The hub only reads the header for routing — the payload bytes are forwarded without decoding.

XFramework Hub currently caps each frame at 8 MiB as a provisional containment limit. Large logical RPC responses remain supported through Bolt's chunked streaming path; callers must not send a single frame above the negotiated or configured limit.

```
RPC Request:  [1:type] [16:requestId] [4:recipientHash] [4:senderHash] [4:commandHash] [4:payloadLen] [payload]   33B header
RPC Response: [1:type] [16:requestId] [2:statusCode] [4:payloadLen] [payload]                       23B header
Stream Open:  [1:type] [16:streamId]  [4:recipientHash] [4:commandHash]                             25B header
Stream Data:  [1:type] [16:streamId]  [4:payloadLen] [payload]                                      21B header
Stream Close: [1:type] [16:streamId]  [2:statusCode]                                                19B header
```

Routing uses FNV-1a hashes (4-byte integer comparison) instead of string matching.

### Registration Identity Binding

Authenticated service connections can bind the Bolt register identity to the service identity from the authenticated principal. The reusable server exposes `BoltServerOptions.RegistrationIdentityBindingMode` with `Off`, `Audit`, and `Enforce` modes. XFramework Bolt Hub configures this through `BoltConfiguration:RegistrationIdentityBindingMode`, reserves the central `XFrameworkServiceNames.All` identities, and explicitly uses `Enforce` in every non-Development configuration. Development explicitly uses `Audit` only for compatibility diagnostics.

For service clients, the expected Bolt registration is `clientName == authenticated service name` and `clientId == SHA256(clientName)`. Non-service user/browser clients keep the normal registration path unless they attempt to claim a reserved service identity by name, prefix, or reserved deterministic service client ID.

XFramework Hub intentionally fixes the required service scope to `bolt.service` and resolves service identity from `client_id`, `service`, `azp`, then `sub`. Non-Development deployments must not override `BoltConfiguration:RegistrationIdentityBindingMode=Enforce`. An identity mismatch is a deployment or caller defect and must fail closed.

### WebSocket Query Tokens

The XFramework Hub accepts `access_token` from the query string only on `/bolt/ws`. Its first application middleware removes that parameter before authentication continues and sanitizes the request surfaces used by downstream application logs and telemetry while preserving all other query parameters.

This sanitation cannot protect logs emitted before the first application middleware, including web-server request-line diagnostics. Reverse proxies, ingress controllers, load balancers, WAFs, and CDN access logs must disable query-string capture for `/bolt/ws` or explicitly redact `access_token`. Bolt WebSocket connections carrying query tokens must use TLS (`wss://`) outside local development.

### Architecture

```
                    ┌──────────────┐
  Client A ───WS──▶│              │──WS──▶ Service B
  Client A ───WS──▶│  Bolt Server │──WS──▶ Service B
                    │    (Hub)     │
  Client C ───WS──▶│              │──WS──▶ Service D
  Client C ───WS──▶│              │──WS──▶ Service D
                    └──────────────┘
```

Each client can open multiple WebSocket connections to the hub. The hub round-robins requests across all connections for a given service, eliminating single-connection bottlenecks.

For direct mode (no hub), the client connects straight to the service:

```
  Client ───WS──▶ Service (handles requests locally)
```

### Transport Design Compared with gRPC

**gRPC's overhead at each hop:**
1. HTTP/2 HPACK header encode/decode
2. Protobuf serialize/deserialize
3. HTTP/2 stream frame management
4. gRPC status and trailer processing

**Bolt uses different framing and routing work:**
1. Compact binary headers — no HTTP framing
2. MemoryPack payloads in the current .NET typed API; relative serializer performance is workload-dependent
3. Hub forwards raw bytes — zero decode at the routing layer
4. FNV-1a hash routing — 4-byte integer comparison

**Transport design compared with SignalR**

SignalR adds overhead from:
1. MessagePack encoding for the SignalR protocol layer (on top of your payload)
2. Hub method resolution by string name
3. Connection management overhead
4. No native connection pooling — single connection per client

The historical concurrent result is not a fair basis for a general SignalR performance claim.

## Features

### XFramework Generated Handlers

XFramework API modules normally expose Bolt-callable feature handlers through source generation rather than manual `RegisterHandler` calls. Put `[BoltHandler]` on a static VSA handler whose first parameter implements `IBoltRequest<TRequest, TResponse>`. If the same method also has `[MapPost]`, `[MapGet]`, `[MapPut]`, `[MapPatch]`, or `[MapDelete]`, `BoltHandlerGenerator` emits both the Bolt `IBoltHandler` and the Minimal API adapter. Module `Program.cs` maps generated REST routes with `app.MapGeneratedEndpoints()`; Bolt handler registration is discovered at startup by the XFramework integration hosted service.

```csharp
public sealed record AuthenticateIdentityRequest(string Email, string Password) :
    IBoltRequest<AuthenticateIdentityRequest, Result<AuthenticateIdentityResponse>>;

public static class AuthenticateEndpoint
{
    [MapPost("/api/auth/authenticate", Tags = ["Auth"])]
    [BoltHandler]
    public static Task<Result<AuthenticateIdentityResponse>> Handle(
        AuthenticateIdentityRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.AuthenticateAsync(request, ct);
}
```

Use manual `BoltServer.RegisterHandler` only for low-level protocol tests, direct-mode samples, or infrastructure that is not a VSA feature.

### RPC (Request-Response)

```csharp
// Typed — auto-serializes request and deserializes response with MemoryPack
var response = await client.SendAsync<GreetRequest, GreetResponse>("greeting-service", "greet",
    new GreetRequest { Name = "World" });

// Command (no response body)
var status = await client.SendAsync("service", "delete-item",
    new DeleteRequest { Id = itemId });

// Raw bytes (when you need full control)
var (statusCode, data) = await client.InvokeAsync("service", "command", rawBytes);
```

### Bidirectional Streaming

Stream any binary data — video, audio, files, sensor data:

```csharp
// Sender
var stream = await client.OpenStreamAsync("video-service", "upload");
await stream.SendAsync(frame1);
await stream.SendAsync(frame2);
await stream.CloseAsync();

// Receiver
client.RegisterStreamHandler("upload", async (stream) =>
{
    await foreach (var chunk in stream.ReadAllAsync())
        ProcessChunk(chunk);
});
```

### Typed Streaming with IAsyncEnumerable

Auto-serialization with MemoryPack:

```csharp
// Pipe an async producer into a stream
await client.StreamAsync("analytics", "ingest",
    GetSensorReadingsAsync(), ct);  // IAsyncEnumerable<SensorReading>

// Receive typed objects
client.RegisterStreamHandler<SensorReading>("ingest",
    async (readings, stream) =>
    {
        await foreach (var reading in readings)
            await StoreAsync(reading);
    });
```

### Connection Pooling

Multiple WebSocket connections per client, auto-scaling under load:

```csharp
var options = new BoltClientOptions
{
    MinConnections = 2,        // Start with 2 connections
    MaxConnections = 8,        // Scale up to 8 under load
    ScaleUpThreshold = 16,     // Scale when pending sends > 16
    RpcTimeoutSeconds = 30
};
```

### Direct Mode

Server handles requests locally — no routing hop:

```csharp
// Server
var server = app.Services.GetRequiredService<BoltServer>();
server.RegisterHandler("hello", async (payload, requestId) =>
{
    var request = MemoryPackSerializer.Deserialize<HelloRequest>(payload.Span);
    var response = new HelloResponse { Message = $"Hello {request.Name}" };
    return (HttpStatusCode.OK, MemoryPackSerializer.Serialize(response));
});
app.UseWebSockets();
app.MapBolt("/bolt");

// Client
var client = new BoltClient(new Uri("ws://server/bolt"), "my-client", "MyClient", options, logger);
await client.ConnectAsync();
var (status, data) = await client.InvokeAsync("_", "hello", payload);
```

## Packages

| Package | Description | Dependencies |
|---------|-------------|-------------|
| `Bolt.Protocol` | Wire format, codec, buffers | None |
| `Bolt.Server` | Hub server middleware for ASP.NET Core | Bolt.Protocol |
| `Bolt.Client` | RPC + streaming client with DI support | Bolt.Protocol, MemoryPack |

## Quick Start

### Server

```csharp
var builder = WebApplication.CreateBuilder();

builder.Services.AddBoltServer();
// Or with options:
// builder.Services.AddBoltServer(o => o.InvocationTimeoutMs = 60000);

var app = builder.Build();
app.UseWebSockets();
app.MapBolt("/bolt");
app.Run();
```

### Client (via DI — recommended for Blazor, ASP.NET, hosted apps)

```csharp
builder.Services.AddBoltClient(bolt => bolt
    .WithServer("ws://localhost:5000/bolt")
    .WithClientId("my-service")
    .WithClientName("MyService")
    .WithMinConnections(2)
    .WithMaxConnections(8)
    .WithTimeout(30)
    .HandleRpc("greet", async (payload, id) =>
    {
        var name = MemoryPackSerializer.Deserialize<string>(payload.Span);
        var reply = MemoryPackSerializer.Serialize($"Hello {name}");
        return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)reply);
    })
    .HandleStream("live-data", async (stream) =>
    {
        await foreach (var chunk in stream.ReadAllAsync())
            ProcessUpdate(chunk);
    })
);
```

The client auto-connects on app startup via `IHostedService` and disconnects on shutdown. Then inject it anywhere:

```csharp
public class GreetingService(BoltClient bolt)
{
    public async Task<HelloMsg> Greet(string name)
    {
        return await bolt.SendAsync<HelloMsg, HelloMsg>("greeting-service", "greet",
            new HelloMsg { Text = name });
    }
}
```

### Client (manual — for console apps or when you need full control)

```csharp
var client = new BoltClient(
    new Uri("ws://localhost:5000/bolt"),
    "my-service", "MyService",
    new BoltClientOptions { MinConnections = 2 }, logger);

client.RegisterHandler("greet", async (payload, id) =>
{
    var name = MemoryPackSerializer.Deserialize<string>(payload.Span);
    var reply = MemoryPackSerializer.Serialize($"Hello {name}");
    return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)reply);
});

await client.ConnectWithRetryAsync();
```

### Direct Mode (server handles requests locally, no hub routing)

```csharp
// Server
builder.Services.AddBoltServer();
var app = builder.Build();

app.Services.GetRequiredService<BoltServer>().RegisterHandler("hello", async (payload, id) =>
{
    var msg = MemoryPackSerializer.Deserialize<HelloMsg>(payload.Span)!;
    var reply = MemoryPackSerializer.Serialize(new HelloMsg { Text = $"Hello {msg.Text}" });
    return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)reply);
});

app.UseWebSockets();
app.MapBolt("/bolt");

// Client connects directly — no hub needed
builder.Services.AddBoltClient(bolt => bolt
    .WithServer("ws://server:5000/bolt")
    .WithClientId("caller")
);

// Usage — typed, clean
var response = await bolt.SendAsync<HelloMsg, HelloMsg>("_", "hello", new HelloMsg { Text = "World" });
```

### Blazor Server / WASM

```csharp
// In Program.cs
builder.Services.AddBoltClient(bolt => bolt
    .WithServer("ws://api.myapp.com/bolt")
    .WithClientId($"blazor_{Guid.NewGuid():N}")
    .WithClientName("BlazorApp")
    .HandleRpc("notification", async (payload, id) =>
    {
        // Handle server-push notifications
        var notification = MemoryPackSerializer.Deserialize<Notification>(payload.Span);
        NotificationStore.Add(notification);
        return (HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty);
    })
    .HandleStream("live-feed", async (stream) =>
    {
        await foreach (var update in stream.ReadAllAsync<LiveUpdate>())
            StateContainer.ApplyUpdate(update);
    })
);

// In any component or service — inject and use
@inject BoltClient Bolt

@code {
    private async Task SendMessage(string text)
    {
        await Bolt.SendAsync("chat-service", "send", new ChatMessage { Text = text });
    }

    private async Task<UserProfile> GetProfile(Guid userId)
    {
        return await Bolt.SendAsync<GetProfileRequest, UserProfile>("user-service", "get-profile",
            new GetProfileRequest { UserId = userId });
    }
}
```
