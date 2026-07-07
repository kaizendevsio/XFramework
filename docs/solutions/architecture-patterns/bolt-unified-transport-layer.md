---
title: "Bolt Unified Transport Layer"
date: 2026-03-31
category: architecture-patterns
module: Bolt
problem_type: architecture_pattern
component: service_object
severity: critical
applies_when:
  - "Designing Bolt transport abstraction across QUIC, WebTransport, and WebSocket with one codec and fallback negotiation"
tags: [bolt, transport, quic, webtransport, websocket]
---

# Bolt Unified Transport Layer

**Date**: 2026-03-31
**Status**: Approved direction; current implementation is WebSocket-first for RPC
**Scope**: Transport abstraction for Bolt protocol. Today, production RPC uses WebSocket. QUIC/WebTransport remain planned or media/browser-specific transport work unless explicitly implemented in the runtime.

## Goal

Keep one Bolt wire protocol (`BoltCodec` with `senderHash`) behind `IBoltConnection`, while preserving the current WebSocket RPC path. QUIC and WebTransport can use the same codec and length-prefixed framing when their runtime endpoints are completed, but they are not the default RPC path today.

## Decision: One Codec Everywhere

Standardize on `BoltCodec` (33-byte request header with `senderHash`) across all transports. The existing `BoltHubCodec` (29-byte header, no `senderHash`) is deleted. The 4-byte overhead per frame is negligible, and having one wire format simplifies hub routing — the hub forwards raw bytes regardless of transport.

## Core Interface: `IBoltConnection`

Every transport implements this. `BoltClient` and `BoltServer` operate exclusively on this interface.

```csharp
public interface IBoltConnection : IAsyncDisposable
{
    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default);
    ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    bool SupportsDatagrams { get; }
    bool IsConnected { get; }
    ValueTask CloseAsync(CancellationToken ct = default);
}
```

Design choice: the interface mirrors WebSocket semantics (message send, chunked receive with `EndOfMessage` flag). This is the lowest common denominator. QUIC and WebTransport map their byte-stream model into this message model via length-prefixed framing. This means `BoltClient`'s receive loop, multi-frame assembly, frame dispatch, and streaming logic remain unchanged.

## Transport Implementations

### `WebSocketBoltConnection`

Wraps `ClientWebSocket`. Thin adapter — `SendAsync` maps to `WebSocket.SendAsync`, `ReceiveAsync` maps to `WebSocket.ReceiveAsync` returning `(Count, EndOfMessage)`. `SendDatagramAsync` is a no-op. `SupportsDatagrams` returns false.

### `QuicBoltConnection`

Wraps `QuicConnection` with a single persistent bidirectional `QuicStream` for message exchange.

QUIC streams are byte-oriented (no message boundaries), unlike WebSocket. `QuicBoltConnection` adds a 4-byte little-endian length prefix to each message:

```
[4:messageLength (uint32 LE)] [messageLength bytes of Bolt frame]
```

This framing is internal to the transport — `BoltClient` never sees the prefix. The send path writes prefix + payload in one write via `RentedBufferWriter`. The receive path reads the prefix, then fills the caller's buffer, returning `(bytesRead, endOfMessage)` matching the WebSocket pattern. If the message is larger than the buffer, `endOfMessage` is false and the caller continues reading — same as WebSocket fragmentation.

`SendDatagramAsync` uses `QuicConnection.SendDatagramAsync()` (RFC 9221) for unreliable media frames. `SupportsDatagrams` checks `DatagramMaxSize > 0` (negotiated during QUIC handshake).

Overhead: 4 bytes per frame. At 50fps media streaming, 200 bytes/second. Negligible.

### `WebTransportBoltConnection`

For browser clients (Blazor WASM via JS interop, or TypeScript client). WebTransport is HTTP/3-based and provides both reliable streams and unreliable datagrams — same capabilities as raw QUIC but accessible from browsers.

On the .NET server: wraps ASP.NET Core's `IHttpWebTransportFeature` / `WebTransportSession` (available in .NET 8+). On the browser: wraps the JS WebTransport API via interop. Uses the same 4-byte length-prefixed framing as `QuicBoltConnection`.

`SupportsDatagrams` returns true (WebTransport always supports datagrams).

### Why not one-stream-per-RPC for QUIC?

QUIC's ideal model is opening a fresh stream per RPC. But that requires a completely different receive model — accepting streams and dispatching instead of one loop reading frames. The persistent-stream approach with length prefixing gives us zero changes to BoltClient's receive loop, frame dispatch, and streaming logic. Stream-per-RPC can be added as a future optimization inside `QuicBoltConnection` without changing the interface.

## Transport Negotiation

### Client-side: `BoltTransportNegotiator`

Tries transports in priority order with a 3-second timeout per attempt. Returns the first working `IBoltConnection`.

```
Default priority today: WebSocket
Optional configured order: WebTransport -> WebSocket
```

Configuration via `BoltClientOptions`:

```csharp
public class BoltClientOptions
{
    public BoltTransport[] PreferredTransports { get; set; } =
        [BoltTransport.WebSocket];
    public int TransportAttemptTimeoutMs { get; set; } = 3000;
}

public enum BoltTransport { Quic, WebTransport, WebSocket }
```

The .NET `BoltTransportNegotiator` currently attempts WebSocket and has a placeholder for WebTransport. QUIC is not used for RPC transport; it is reserved for media/datagram work.

### Runtime detection

| Transport | Available when |
|-----------|---------------|
| QUIC | Planned for media/datagram or future RPC work; not attempted by the .NET RPC negotiator today |
| WebTransport | Planned/browser-specific; the .NET RPC negotiator placeholder returns no connection today |
| WebSocket | Always |

### Transport selection by environment

| Environment | Default | Fallback |
|------------|---------|----------|
| .NET server-to-server | WebSocket | -- |
| Blazor WASM (Chrome/Edge) | WebSocket today | WebTransport planned |
| Blazor WASM (Safari/Firefox) | WebSocket | -- |
| MAUI Android/Windows | WebSocket today | QUIC planned |
| MAUI iOS/macOS | WebSocket | -- |

### Server-side: current RPC endpoint

The server doesn't negotiate transport inside one endpoint. Current `MapBolt` maps the WebSocket RPC endpoint:

1. **WebSocket**: `app.MapBolt("/bolt")` or `app.MapBolt("/bolt/ws")` accepts WebSocket upgrade and wraps it in `WebSocketBoltConnection`.
2. **WebTransport**: not currently mapped by `MapBolt`.
3. **QUIC**: not currently started by `BoltServer` for RPC.

Any future transport should still funnel into `HandleConnectionAsync(IBoltConnection, ClaimsPrincipal?, CancellationToken)` so hub routing remains transport-agnostic.

### Reconnection

On disconnect, `BoltClient.ReconnectAsync` re-runs the negotiator. If QUIC was working but now fails (network changed), it falls back to WebSocket transparently.

## BoltClient Integration

### `BoltConnection` (pool wrapper)

Changes from wrapping `ClientWebSocket` to wrapping `IBoltConnection`:

```csharp
public sealed class BoltConnection
{
    public IBoltConnection Transport { get; }
    public BoltTransport TransportType { get; }
}
```

### `CreateConnectionAsync`

Changes from `new ClientWebSocket()` + `ConnectAsync` to `_negotiator.ConnectAsync(_options, ct)`. Returns an `IBoltConnection`. Registration frame and receive loop start unchanged.

### `ReceiveLoopAsync`

Changes from `conn.WebSocket.ReceiveAsync` to `conn.Transport.ReceiveAsync`. Returns `(bytesRead, endOfMessage)` instead of `WebSocketReceiveResult`. The rest — multi-frame assembly, frame dispatch, handler invocation — unchanged.

### Media datagram integration

`BoltMediaStream` already has `SetDatagramTransport(Func<ReadOnlyMemory<byte>, ValueTask>)`. Now wired automatically:

```csharp
if (connection.Transport.SupportsDatagrams)
    mediaStream.SetDatagramTransport(data => connection.Transport.SendDatagramAsync(data));
```

No changes to `BoltMediaStream` itself.

## BoltServer Integration

### `HandleConnectionAsync`

Signature changes from `(WebSocket, CancellationToken)` to `(IBoltConnection, CancellationToken)`. Internal receive loop, frame dispatch, routing — unchanged.

### `BoltHubConnection`

Changes from wrapping `WebSocket` to wrapping `IBoltConnection`. `SendAsync` delegates to `Transport.SendAsync`.

### Raw QUIC listener (planned)

```csharp
public async Task StartQuicListenerAsync(IPEndPoint endpoint, X509Certificate2 cert, CancellationToken ct)
```

A future RPC QUIC listener would create a `QuicListener` with ALPN "bolt", accept connections in a background loop, wrap each in `QuicBoltConnection`, and call `HandleConnectionAsync`. This is not part of the current `MapBolt` runtime.

## Data Flow by Frame Type

| Frame type | Transport mode | Reason |
|-----------|---------------|--------|
| RPC (Request/Response) | Reliable stream | Must arrive, in order |
| Push | Reliable stream | Must arrive |
| Streaming (StreamOpen/Data/Close) | Reliable stream | Must arrive, in order |
| Media (audio/video delta) | Unreliable datagram (if available) | OK to drop, lowest latency |
| Media (keyframes) | Reliable stream | Too important to drop |
| Call signaling | Reliable stream | Must arrive |

## File Changes

### Transport files

```
Bolt.Client/Transport/IBoltConnection.cs
Bolt.Client/Transport/BoltTransport.cs
Bolt.Client/Transport/BoltTransportNegotiator.cs
Bolt.Client/Transport/WebSocketBoltConnection.cs
Bolt.Client/Transport/WebTransportBoltConnection.cs
```

### Current modified runtime surface

```
Bolt.Client/BoltClient.cs              — IBoltConnection instead of ClientWebSocket
Bolt.Client/BoltClientOptions.cs       — PreferredTransports and transport attempt timeout
Bolt.Client/BoltConnection.cs          — wraps IBoltConnection
Bolt.Server/BoltServer.cs              — HandleConnectionAsync takes IBoltConnection
Bolt.Server/BoltServerExtensions.cs    — MapBolt accepts WebSocket connections
IdentityServer.Benchmarks/            — updated to use unified BoltClient
```

### Deleted files (4)

```
XFramework.Integration/ThinProtocol/QuicBoltClient.cs
XFramework.Integration/ThinProtocol/QuicDirectClient.cs
XFramework.Bolt/Bolt.Hub/ThinProtocol/QuicBoltServer.cs
XFramework.Bolt/Bolt.Domain.Shared/Protocol/BoltHubCodec.cs
```

### Kept as-is

```
Bolt.Media/QuicDatagramHelper.cs   — fragmentation logic still useful for QUIC/WebTransport datagrams
```

## Testing

### Benchmarks

Extend `PayloadBenchmarks` with transport variants:

```
Bolt_WebSocket_Echo (baseline)
Bolt_Quic_Echo
Bolt_WebTransport_Echo  (platform-dependent)
GRPC_Echo
```

Same payload sizes (100B to 20MB).

### Integration tests

| Test | Verifies |
|------|----------|
| `Quic_RpcCall_WorksThroughHub` | QUIC client -> hub -> QUIC service round-trip |
| `MixedTransport_QuicCaller_WebSocketService` | Cross-transport routing through hub |
| `MixedTransport_WebSocketCaller_QuicService` | Reverse cross-transport |
| `TransportFallback_QuicUnavailable_FallsBackToWebSocket` | Auto-fallback when QUIC unavailable |
| `Quic_LargePayload_AutoStreaming` | Auto-streaming over QUIC |
| `Quic_Datagram_MediaFrames` | Unreliable datagrams for media |
| `Quic_Reconnect_AfterDisconnect` | Negotiator re-runs on reconnect |

WebTransport integration tests deferred until Blazor WASM client is built (requires HTTP/3 + TLS setup).
