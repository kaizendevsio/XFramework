# Bolt Unified Transport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make QUIC the default Bolt transport, WebTransport for browsers, WebSocket fallback — all behind `IBoltConnection`.

**Architecture:** Extract transport concerns from `BoltClient`/`BoltServer` into `IBoltConnection` interface with three implementations (WebSocket, QUIC, WebTransport). A negotiator tries transports in priority order. One wire protocol (`BoltCodec`) everywhere. Delete old standalone QUIC classes.

**Tech Stack:** .NET 10, System.Net.Quic, ASP.NET Core WebTransport, BoltCodec, ArrayPool, RentedBufferWriter

**Spec:** `docs/superpowers/specs/2026-03-31-bolt-unified-transport-design.md`

---

## File Map

### New Files

Shared types (`IBoltConnection`, `BoltTransport`, `WebSocketBoltConnection`) live in `Bolt.Protocol` so both `Bolt.Client` and `Bolt.Server` can reference them without circular dependency. Client-only types (negotiator, QUIC/WebTransport connections) live in `Bolt.Client`.

| File | Responsibility |
|------|---------------|
| `Bolt.Protocol/Transport/IBoltConnection.cs` | Core transport interface (shared) |
| `Bolt.Protocol/Transport/BoltTransport.cs` | Transport enum (shared) |
| `Bolt.Protocol/Transport/WebSocketBoltConnection.cs` | WebSocket implementation (shared — server creates these too) |
| `Bolt.Client/Transport/QuicBoltConnection.cs` | QUIC implementation with length-prefixed framing |
| `Bolt.Client/Transport/WebTransportBoltConnection.cs` | WebTransport implementation |
| `Bolt.Client/Transport/BoltTransportNegotiator.cs` | Try QUIC → WebTransport → WebSocket |

### Modified Files
| File | Change |
|------|--------|
| `Bolt.Client/BoltClient.cs` | `BoltConnection` wraps `IBoltConnection`; `CreateConnectionAsync` uses negotiator; `ReceiveLoopAsync` uses `IBoltConnection.ReceiveAsync` |
| `Bolt.Client/BoltClientOptions.cs` | Add transport preferences (`PreferredTransports`, `TransportAttemptTimeoutMs`) |
| `Bolt.Client/BoltClientExtensions.cs` | Update builder to expose transport config |
| `Bolt.Client/Bolt.Client.csproj` | No new package refs needed (System.Net.Quic is in-box) |
| `Bolt.Server/BoltServer.cs` | `HandleConnectionAsync(IBoltConnection, ct)`; `BoltHubConnection` wraps `IBoltConnection` |
| `Bolt.Server/BoltServerExtensions.cs` | `MapBolt` accepts WebSocket + WebTransport; add `StartQuicListenerAsync` |
| `Bolt.Server/Bolt.Server.csproj` | No new package refs needed |

### Deleted Files
| File | Reason |
|------|--------|
| `XFramework.Integration/ThinProtocol/QuicBoltClient.cs` | Replaced by `BoltClient` + `QuicBoltConnection` |
| `XFramework.Integration/ThinProtocol/QuicDirectClient.cs` | Replaced by `BoltClient` + `QuicBoltConnection` |
| `XFramework.Bolt/Bolt.Hub/ThinProtocol/QuicBoltServer.cs` | Replaced by `BoltServer` + QUIC listener |
| `XFramework.Bolt/Bolt.Domain.Shared/Protocol/BoltHubCodec.cs` | Replaced by unified `BoltCodec` |

---

## Task 1: IBoltConnection Interface + BoltTransport Enum

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Protocol/Transport/IBoltConnection.cs`
- Create: `src/Libraries/Bolt/Bolt.Protocol/Transport/BoltTransport.cs`

- [ ] **Step 1: Create the BoltTransport enum**

```csharp
// src/Libraries/Bolt/Bolt.Protocol/Transport/BoltTransport.cs
namespace Bolt.Protocol.Transport;

/// <summary>Available transport protocols for Bolt connections.</summary>
public enum BoltTransport
{
    /// <summary>Raw QUIC with ALPN "bolt". Default for .NET server-to-server.</summary>
    Quic,

    /// <summary>HTTP/3 WebTransport. Default for browsers (Chrome/Edge).</summary>
    WebTransport,

    /// <summary>WebSocket over HTTP/1.1. Universal fallback.</summary>
    WebSocket
}
```

- [ ] **Step 2: Create the IBoltConnection interface**

```csharp
// src/Libraries/Bolt/Bolt.Protocol/Transport/IBoltConnection.cs
namespace Bolt.Protocol.Transport;

/// <summary>
/// Transport abstraction for Bolt protocol communication.
/// Implementations: WebSocket, QUIC, WebTransport.
///
/// The interface mirrors WebSocket message semantics (send complete message,
/// receive chunks with EndOfMessage flag). QUIC and WebTransport implementations
/// use 4-byte length-prefixed framing internally to provide message boundaries
/// over their byte-oriented streams.
/// </summary>
public interface IBoltConnection : IAsyncDisposable
{
    /// <summary>Send a complete binary message reliably (ordered, guaranteed delivery).</summary>
    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>
    /// Receive the next message chunk into the buffer.
    /// Returns (bytesRead, endOfMessage). When endOfMessage is false,
    /// caller must keep reading to assemble the full message.
    /// Returns (0, true) when the connection is closed.
    /// </summary>
    ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default);

    /// <summary>
    /// Send a fire-and-forget datagram (unreliable, unordered).
    /// Used for drop-eligible media frames. No-op on transports that don't support datagrams.
    /// </summary>
    ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>Whether this transport supports unreliable datagrams (QUIC/WebTransport only).</summary>
    bool SupportsDatagrams { get; }

    /// <summary>Connection is open and usable.</summary>
    bool IsConnected { get; }

    /// <summary>Which transport this connection uses.</summary>
    BoltTransport TransportType { get; }

    /// <summary>Graceful close.</summary>
    ValueTask CloseAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build src/Libraries/Bolt/Bolt.Protocol/Bolt.Protocol.csproj -v q`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Protocol/Transport/
git commit -m "feat(bolt): add IBoltConnection interface and BoltTransport enum"
```

---

## Task 2: WebSocketBoltConnection

Extract the existing WebSocket transport into an `IBoltConnection` implementation.

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Protocol/Transport/WebSocketBoltConnection.cs`
- Test: `src/Tests/Bolt.Tests/TransportTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// src/Tests/Bolt.Tests/TransportTests.cs
using System.Net.WebSockets;
using Bolt.Protocol.Transport;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class WebSocketBoltConnectionTests
{
    [Test]
    public void TransportType_IsWebSocket()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.TransportType.Should().Be(BoltTransport.WebSocket);
    }

    [Test]
    public void SupportsDatagrams_IsFalse()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.SupportsDatagrams.Should().BeFalse();
    }

    [Test]
    public async Task SendDatagramAsync_IsNoOp()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        // Should not throw — just a no-op
        await conn.SendDatagramAsync(new byte[] { 1, 2, 3 });
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "ClassName~WebSocketBoltConnectionTests" --no-restore -v q`
Expected: FAIL — `WebSocketBoltConnection` does not exist

- [ ] **Step 3: Implement WebSocketBoltConnection**

```csharp
// src/Libraries/Bolt/Bolt.Protocol/Transport/WebSocketBoltConnection.cs
using System.Net.WebSockets;

namespace Bolt.Protocol.Transport;

/// <summary>
/// IBoltConnection implementation over WebSocket.
/// Thin wrapper — delegates directly to ClientWebSocket.
/// </summary>
public sealed class WebSocketBoltConnection : IBoltConnection
{
    private readonly WebSocket _ws;

    public WebSocketBoltConnection(WebSocket webSocket) => _ws = webSocket;

    public BoltTransport TransportType => BoltTransport.WebSocket;

    public bool SupportsDatagrams => false;

    public bool IsConnected => _ws.State == WebSocketState.Open;

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => _ws.SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, ct);

    public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var result = await _ws.ReceiveAsync(buffer, ct);
        if (result.MessageType == WebSocketMessageType.Close)
            return (0, true);
        return (result.Count, result.EndOfMessage);
    }

    public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => ValueTask.CompletedTask; // No-op: WebSocket has no datagram support

    public async ValueTask CloseAsync(CancellationToken ct = default)
    {
        if (_ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
    }

    public ValueTask DisposeAsync()
    {
        _ws.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "ClassName~WebSocketBoltConnectionTests" -v q`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Protocol/Transport/WebSocketBoltConnection.cs src/Tests/Bolt.Tests/TransportTests.cs
git commit -m "feat(bolt): add WebSocketBoltConnection — IBoltConnection over WebSocket"
```

---

## Task 3: QuicBoltConnection

QUIC transport with 4-byte length-prefixed framing over a persistent bidirectional stream.

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Client/Transport/QuicBoltConnection.cs`
- Test: `src/Tests/Bolt.Tests/TransportTests.cs` (append)

- [ ] **Step 1: Write the test for length-prefixed framing round-trip**

We can't easily unit test real QUIC (requires listener + TLS), so test the framing logic in isolation. Add to `TransportTests.cs`:

```csharp
[TestFixture]
public class QuicFramingTests
{
    [Test]
    public void WriteLengthPrefix_ReadsBackCorrectly()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var framed = new byte[4 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(framed, (uint)payload.Length);
        payload.CopyTo(framed.AsSpan(4));

        var readLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(framed);
        readLen.Should().Be(5);
        framed.AsSpan(4, (int)readLen).ToArray().Should().Equal(payload);
    }

    [Test]
    public void WriteLengthPrefix_LargePayload_CorrectLength()
    {
        var payload = new byte[1_048_576]; // 1MB
        Random.Shared.NextBytes(payload);
        var framed = new byte[4 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(framed, (uint)payload.Length);
        payload.CopyTo(framed.AsSpan(4));

        var readLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(framed);
        readLen.Should().Be(1_048_576);
    }
}
```

- [ ] **Step 2: Run framing tests to verify they pass**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "ClassName~QuicFramingTests" -v q`
Expected: 2 passed

- [ ] **Step 3: Implement QuicBoltConnection**

```csharp
// src/Libraries/Bolt/Bolt.Client/Transport/QuicBoltConnection.cs
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Quic;
using Bolt.Protocol.Buffers;

namespace Bolt.Client.Transport;

/// <summary>
/// IBoltConnection implementation over QUIC.
/// Uses a single persistent bidirectional stream with 4-byte length-prefixed framing
/// to provide message boundaries over QUIC's byte-oriented streams.
///
/// Wire format per message: [4:messageLength (uint32 LE)] [messageLength bytes of Bolt frame]
///
/// Datagrams use QuicConnection unreliable datagram API (RFC 9221).
/// </summary>
public sealed class QuicBoltConnection : IBoltConnection
{
    private readonly QuicConnection _connection;
    private QuicStream? _primaryStream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Receive state: tracks partially read messages across ReceiveAsync calls
    private int _remainingMessageBytes;
    private readonly byte[] _lengthBuf = new byte[4];

    public QuicBoltConnection(QuicConnection connection)
    {
        _connection = connection;
    }

    public BoltTransport TransportType => BoltTransport.Quic;

    public bool SupportsDatagrams
    {
        get
        {
            try { return _connection.RemoteEndPoint != null; } // Proxy for "connected"
            catch { return false; }
        }
    }

    public bool IsConnected => !_connection.RemoteCertificate?.Equals(null) ?? _primaryStream != null;

    /// <summary>Open the primary bidirectional stream. Called once after QUIC connection is established.</summary>
    public async Task OpenPrimaryStreamAsync(CancellationToken ct = default)
    {
        _primaryStream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
    }

    /// <summary>Accept the primary stream from the remote side (server-side usage).</summary>
    public async Task AcceptPrimaryStreamAsync(CancellationToken ct = default)
    {
        _primaryStream = await _connection.AcceptInboundStreamAsync(ct);
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_primaryStream is null) throw new InvalidOperationException("Primary stream not opened");

        await _sendLock.WaitAsync(ct);
        try
        {
            // Write length prefix + payload in one call via RentedBufferWriter
            var totalSize = 4 + data.Length;
            var buf = ArrayPool<byte>.Shared.Rent(totalSize);
            try
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)data.Length);
                data.Span.CopyTo(buf.AsSpan(4));
                await _primaryStream.WriteAsync(buf.AsMemory(0, totalSize), ct);
            }
            finally { ArrayPool<byte>.Shared.Return(buf); }
        }
        finally { _sendLock.Release(); }
    }

    public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_primaryStream is null) throw new InvalidOperationException("Primary stream not opened");

        // If we're in the middle of reading a message, continue reading payload bytes
        if (_remainingMessageBytes > 0)
        {
            var toRead = Math.Min(_remainingMessageBytes, buffer.Length);
            var bytesRead = await ReadExactlyOrEofAsync(_primaryStream, buffer[..toRead], ct);
            if (bytesRead == 0) return (0, true); // stream closed
            _remainingMessageBytes -= bytesRead;
            return (bytesRead, _remainingMessageBytes == 0);
        }

        // Read 4-byte length prefix for the next message
        var prefixRead = await ReadExactlyOrEofAsync(_primaryStream, _lengthBuf.AsMemory(), ct);
        if (prefixRead == 0) return (0, true); // stream closed

        var messageLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lengthBuf);
        if (messageLength == 0) return (0, false); // empty message, skip

        // Read as much of the payload as fits in the caller's buffer
        var chunkSize = Math.Min(messageLength, buffer.Length);
        var read = await ReadExactlyOrEofAsync(_primaryStream, buffer[..chunkSize], ct);
        if (read == 0) return (0, true);

        _remainingMessageBytes = messageLength - read;
        return (read, _remainingMessageBytes == 0);
    }

    public async ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        try
        {
            await _connection.SendDatagramAsync(data, ct);
        }
        catch (QuicException)
        {
            // Datagram send failures are silently ignored — unreliable by design
        }
    }

    public async ValueTask CloseAsync(CancellationToken ct = default)
    {
        if (_primaryStream is not null)
        {
            _primaryStream.CompleteWrites();
            await _primaryStream.DisposeAsync();
            _primaryStream = null;
        }
        await _connection.CloseAsync(0, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_primaryStream is not null)
            await _primaryStream.DisposeAsync();
        await _connection.DisposeAsync();
        _sendLock.Dispose();
    }

    /// <summary>Read exactly the requested bytes, or return 0 if stream is closed.</summary>
    private static async Task<int> ReadExactlyOrEofAsync(QuicStream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], ct);
            if (read == 0) return totalRead == 0 ? 0 : totalRead; // EOF
            totalRead += read;
        }
        return totalRead;
    }
}
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj -v q`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Client/Transport/QuicBoltConnection.cs src/Tests/Bolt.Tests/TransportTests.cs
git commit -m "feat(bolt): add QuicBoltConnection — IBoltConnection over QUIC with length-prefixed framing"
```

---

## Task 4: WebTransportBoltConnection

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Client/Transport/WebTransportBoltConnection.cs`

- [ ] **Step 1: Implement WebTransportBoltConnection**

Uses the same 4-byte length-prefixed framing as QUIC. On the server side it wraps an ASP.NET Core `WebTransportSession`. On the browser side (future Blazor WASM), it would wrap JS interop — for now we implement the server-side version.

```csharp
// src/Libraries/Bolt/Bolt.Client/Transport/WebTransportBoltConnection.cs
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Bolt.Client.Transport;

/// <summary>
/// IBoltConnection implementation over WebTransport (HTTP/3).
/// Uses same 4-byte length-prefixed framing as QuicBoltConnection.
///
/// WebTransport provides both reliable streams and unreliable datagrams,
/// accessible from browsers (Chrome/Edge) via the WebTransport API.
///
/// Server-side: wraps a bidirectional WebTransport stream.
/// Browser-side: would wrap JS WebTransport API via interop (future).
/// </summary>
public sealed class WebTransportBoltConnection : IBoltConnection
{
    private readonly Stream _stream;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _datagramSend;
    private readonly Func<ValueTask>? _sessionClose;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private volatile bool _closed;

    // Receive state
    private int _remainingMessageBytes;
    private readonly byte[] _lengthBuf = new byte[4];

    /// <summary>
    /// Create from a bidirectional WebTransport stream.
    /// </summary>
    /// <param name="stream">The bidirectional stream for reliable communication.</param>
    /// <param name="datagramSend">Optional delegate to send unreliable datagrams.</param>
    /// <param name="sessionClose">Optional delegate to close the WebTransport session.</param>
    public WebTransportBoltConnection(
        Stream stream,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? datagramSend = null,
        Func<ValueTask>? sessionClose = null)
    {
        _stream = stream;
        _datagramSend = datagramSend;
        _sessionClose = sessionClose;
    }

    public BoltTransport TransportType => BoltTransport.WebTransport;

    public bool SupportsDatagrams => _datagramSend is not null;

    public bool IsConnected => !_closed && _stream.CanRead && _stream.CanWrite;

    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            var totalSize = 4 + data.Length;
            var buf = ArrayPool<byte>.Shared.Rent(totalSize);
            try
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)data.Length);
                data.Span.CopyTo(buf.AsSpan(4));
                await _stream.WriteAsync(buf.AsMemory(0, totalSize), ct);
                await _stream.FlushAsync(ct);
            }
            finally { ArrayPool<byte>.Shared.Return(buf); }
        }
        finally { _sendLock.Release(); }
    }

    public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        // Same length-prefixed framing as QuicBoltConnection
        if (_remainingMessageBytes > 0)
        {
            var toRead = Math.Min(_remainingMessageBytes, buffer.Length);
            var bytesRead = await ReadExactlyOrEofAsync(_stream, buffer[..toRead], ct);
            if (bytesRead == 0) return (0, true);
            _remainingMessageBytes -= bytesRead;
            return (bytesRead, _remainingMessageBytes == 0);
        }

        var prefixRead = await ReadExactlyOrEofAsync(_stream, _lengthBuf.AsMemory(), ct);
        if (prefixRead == 0) return (0, true);

        var messageLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lengthBuf);
        if (messageLength == 0) return (0, false);

        var chunkSize = Math.Min(messageLength, buffer.Length);
        var read = await ReadExactlyOrEofAsync(_stream, buffer[..chunkSize], ct);
        if (read == 0) return (0, true);

        _remainingMessageBytes = messageLength - read;
        return (read, _remainingMessageBytes == 0);
    }

    public async ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_datagramSend is not null)
        {
            try { await _datagramSend(data, ct); }
            catch { /* Unreliable — failures silently ignored */ }
        }
    }

    public async ValueTask CloseAsync(CancellationToken ct = default)
    {
        _closed = true;
        _stream.Close();
        if (_sessionClose is not null)
            await _sessionClose();
    }

    public async ValueTask DisposeAsync()
    {
        _closed = true;
        await _stream.DisposeAsync();
        _sendLock.Dispose();
    }

    private static async Task<int> ReadExactlyOrEofAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], ct);
            if (read == 0) return totalRead == 0 ? 0 : totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj -v q`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Client/Transport/WebTransportBoltConnection.cs
git commit -m "feat(bolt): add WebTransportBoltConnection — IBoltConnection over HTTP/3 WebTransport"
```

---

## Task 5: BoltTransportNegotiator

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Client/Transport/BoltTransportNegotiator.cs`
- Modify: `src/Libraries/Bolt/Bolt.Client/BoltClientOptions.cs`

- [ ] **Step 1: Extend BoltClientOptions with transport preferences**

Replace the full file:

```csharp
// src/Libraries/Bolt/Bolt.Client/BoltClientOptions.cs
using Bolt.Protocol.Transport;

namespace Bolt.Client;

/// <summary>
/// Configuration for BoltClient connections (RPC, Push, Streaming).
/// For media-specific options, see Bolt.Media.MediaStreamOptions.
/// </summary>
public class BoltClientOptions
{
    /// <summary>RPC call timeout in seconds. Default: 30.</summary>
    public int RpcTimeoutSeconds { get; set; } = 30;

    /// <summary>Minimum connections to maintain. Default: 1.</summary>
    public int MinConnections { get; set; } = 1;

    /// <summary>Maximum connections to scale to. Default: ProcessorCount.</summary>
    public int MaxConnections { get; set; } = Environment.ProcessorCount;

    /// <summary>Pending send count threshold to trigger connection scale-up. Default: 48.</summary>
    public int ScaleUpThreshold { get; set; } = 48;

    /// <summary>
    /// Payload size threshold (bytes) above which InvokeAsync transparently switches
    /// to BoltStream chunking instead of a single Request/Response frame.
    /// Default: 1MB. Set to int.MaxValue to disable auto-streaming.
    /// </summary>
    public int LargePayloadThreshold { get; set; } = 1024 * 1024;

    /// <summary>Chunk size for large payload streaming. Default: 65536 (64KB).</summary>
    public int StreamChunkSize { get; set; } = 65536;

    /// <summary>
    /// Preferred transport order. The negotiator tries each in sequence, using the first
    /// that succeeds. Transports unavailable on the current platform are auto-skipped.
    /// Default: QUIC, WebTransport, WebSocket.
    /// </summary>
    public BoltTransport[] PreferredTransports { get; set; } =
        [BoltTransport.Quic, BoltTransport.WebTransport, BoltTransport.WebSocket];

    /// <summary>Timeout per transport attempt before trying the next one. Default: 3000ms.</summary>
    public int TransportAttemptTimeoutMs { get; set; } = 3000;
}
```

- [ ] **Step 2: Implement BoltTransportNegotiator**

```csharp
// src/Libraries/Bolt/Bolt.Client/Transport/BoltTransportNegotiator.cs
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Bolt.Client.Transport;

/// <summary>
/// Tries transports in priority order and returns the first working IBoltConnection.
/// QUIC -> WebTransport -> WebSocket. Each attempt has a short timeout.
/// </summary>
public sealed class BoltTransportNegotiator
{
    private readonly ILogger _logger;

    /// <summary>The transport that was used for the last successful connection.</summary>
    public BoltTransport? LastTransportUsed { get; private set; }

    public BoltTransportNegotiator(ILogger logger) => _logger = logger;

    public async Task<IBoltConnection> ConnectAsync(Uri serverUri, BoltClientOptions options, CancellationToken ct)
    {
        foreach (var transport in options.PreferredTransports)
        {
            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(options.TransportAttemptTimeoutMs);

                IBoltConnection? conn = transport switch
                {
                    BoltTransport.Quic => await TryQuicAsync(serverUri, attemptCts.Token),
                    BoltTransport.WebTransport => await TryWebTransportAsync(serverUri, attemptCts.Token),
                    BoltTransport.WebSocket => await TryWebSocketAsync(serverUri, attemptCts.Token),
                    _ => null
                };

                if (conn is not null)
                {
                    LastTransportUsed = transport;
                    _logger.LogInformation("Bolt connected via {Transport} to {Uri}", transport, serverUri);
                    return conn;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("Transport {Transport} timed out, trying next", transport);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Transport {Transport} failed, trying next", transport);
            }
        }

        throw new InvalidOperationException(
            $"All transports failed for {serverUri}. Tried: {string.Join(", ", options.PreferredTransports)}");
    }

    private static async Task<IBoltConnection?> TryQuicAsync(Uri serverUri, CancellationToken ct)
    {
        if (!QuicConnection.IsSupported)
            return null;

        var port = serverUri.Port > 0 ? serverUri.Port : 443;
        var connection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
        {
            RemoteEndPoint = new IPEndPoint(
                (await Dns.GetHostAddressesAsync(serverUri.Host, ct))[0], port),
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            MaxInboundBidirectionalStreams = 256,
            MaxInboundUnidirectionalStreams = 256,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = [new SslApplicationProtocol("bolt")],
                TargetHost = serverUri.Host,
                RemoteCertificateValidationCallback = (_, _, _, _) => true // Dev: accept self-signed
            }
        }, ct);

        var quicConn = new QuicBoltConnection(connection);
        await quicConn.OpenPrimaryStreamAsync(ct);
        return quicConn;
    }

    private static async Task<IBoltConnection?> TryWebTransportAsync(Uri serverUri, CancellationToken ct)
    {
        // WebTransport client is not available in .NET natively — only via browser APIs.
        // This transport is skipped for .NET server-to-server. It activates in Blazor WASM
        // where the JS WebTransport API is available via interop.
        //
        // Future: implement JS interop bridge for Blazor WASM here.
        await Task.CompletedTask;
        return null;
    }

    private static async Task<IBoltConnection?> TryWebSocketAsync(Uri serverUri, CancellationToken ct)
    {
        // Build the WebSocket URI from whatever scheme the user provided
        var wsScheme = serverUri.Scheme switch
        {
            "https" or "wss" or "quic" => "wss",
            _ => "ws"
        };
        var wsUri = new UriBuilder(serverUri) { Scheme = wsScheme }.Uri;

        var ws = new ClientWebSocket();
        await ws.ConnectAsync(wsUri, ct);
        return new WebSocketBoltConnection(ws);
    }
}
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj -v q`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Client/BoltClientOptions.cs src/Libraries/Bolt/Bolt.Client/Transport/BoltTransportNegotiator.cs
git commit -m "feat(bolt): add BoltTransportNegotiator — QUIC/WebTransport/WebSocket fallback chain"
```

---

## Task 6: Update BoltClient to Use IBoltConnection

The core refactor. Replace all `ClientWebSocket` references with `IBoltConnection`.

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs`

- [ ] **Step 1: Update BoltConnection class (pool wrapper)**

At the bottom of `BoltClient.cs`, replace the `BoltConnection` class (currently lines ~822-870):

```csharp
/// <summary>
/// A single connection in the Bolt client pool.
/// Wraps an IBoltConnection (WebSocket, QUIC, or WebTransport).
/// </summary>
public sealed class BoltConnection
{
    public IBoltConnection Transport { get; }
    public BoltTransport TransportType { get; }
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _pendingSends;

    public CancellationTokenSource? ReceiveCts { get; set; }
    public Task? ReceiveLoop { get; set; }
    public int PendingSends => _pendingSends;

    public BoltConnection(IBoltConnection transport)
    {
        Transport = transport;
        TransportType = transport.TransportType;
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        Interlocked.Increment(ref _pendingSends);
        if (_sendLock.Wait(0))
        {
            try
            {
                if (Transport.IsConnected)
                {
                    var task = Transport.SendAsync(data, ct);
                    if (task.IsCompleted) { Interlocked.Decrement(ref _pendingSends); return task; }
                    return AwaitAndDecrement(task);
                }
                Interlocked.Decrement(ref _pendingSends);
                return ValueTask.CompletedTask;
            }
            finally { _sendLock.Release(); }
        }
        return SendSlowAsync(data, ct);
    }

    private async ValueTask AwaitAndDecrement(ValueTask task)
    {
        try { await task; } finally { Interlocked.Decrement(ref _pendingSends); }
    }

    private async ValueTask SendSlowAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            if (Transport.IsConnected)
                await Transport.SendAsync(data, ct);
        }
        finally { _sendLock.Release(); Interlocked.Decrement(ref _pendingSends); }
    }
}
```

- [ ] **Step 2: Update BoltClient fields, constructor, and add negotiator**

At the top of `BoltClient`, add the `using Bolt.Protocol.Transport;` import. Update the constructor to create a negotiator:

```csharp
// Add to using statements:
using Bolt.Protocol.Transport;

// Add field:
private readonly BoltTransportNegotiator _negotiator;

// Update constructor:
public BoltClient(Uri serverUri, string clientId, string clientName, BoltClientOptions config, ILogger logger)
{
    _serverUri = serverUri;
    _clientId = clientId;
    _senderHash = BoltCodec.Fnv1aHash(clientId);
    _clientName = clientName;
    _config = config;
    _logger = logger;
    _rpcTimeout = TimeSpan.FromSeconds(config.RpcTimeoutSeconds > 0 ? config.RpcTimeoutSeconds : 30);
    _negotiator = new BoltTransportNegotiator(logger);
}
```

- [ ] **Step 3: Update CreateConnectionAsync to use the negotiator**

Replace `CreateConnectionAsync` (currently lines ~245-264):

```csharp
private async Task<BoltConnection> CreateConnectionAsync(CancellationToken ct)
{
    var transport = await _negotiator.ConnectAsync(_serverUri, _config, ct);
    var conn = new BoltConnection(transport);

    // Send registration frame (same for all transports)
    var writer = new ArrayBufferWriter<byte>(128);
    BoltCodec.WriteRegister(writer, _clientId, _clientName);
    await transport.SendAsync(writer.WrittenMemory, ct);

    // Read registration ack
    var ackBuffer = new byte[2];
    var (ackBytes, _) = await transport.ReceiveAsync(ackBuffer, ct);
    if (ackBytes < 2 || (FrameType)ackBuffer[0] != FrameType.RegisterAck || ackBuffer[1] != 1)
        throw new InvalidOperationException("Server rejected registration");

    var receiveCts = new CancellationTokenSource();
    conn.ReceiveCts = receiveCts;
    conn.ReceiveLoop = Task.Run(() => ReceiveLoopAsync(conn, receiveCts.Token));
    return conn;
}
```

- [ ] **Step 4: Update ReceiveLoopAsync**

Replace the WebSocket-specific receive calls in `ReceiveLoopAsync`. The key changes are:

1. Replace `conn.WebSocket.ReceiveAsync(buffer.AsMemory(), ct)` with `conn.Transport.ReceiveAsync(buffer.AsMemory(), ct)`
2. Replace WebSocket-specific result handling with `(bytesRead, endOfMessage)` tuple
3. Replace `conn.WebSocket.State == WebSocketState.Open` with `conn.Transport.IsConnected`
4. Replace `WebSocketException` catch with general `Exception`

The receive loop header becomes:

```csharp
private async Task ReceiveLoopAsync(BoltConnection conn, CancellationToken ct)
{
    var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
    byte[]? largeBuffer = null;
    try
    {
        while (!ct.IsCancellationRequested && conn.Transport.IsConnected)
        {
            var (bytesRead, endOfMessage) = await conn.Transport.ReceiveAsync(buffer.AsMemory(), ct);
            if (bytesRead == 0) break; // connection closed

            // Handle multi-frame messages (large payloads)
            byte[] frameBytes;
            int totalLength;
            if (!endOfMessage)
            {
                var assembled = bytesRead;
                var capacity = Math.Max(bytesRead * 4, 512 * 1024);
                if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
                largeBuffer = ArrayPool<byte>.Shared.Rent(capacity);
                buffer.AsSpan(0, bytesRead).CopyTo(largeBuffer);

                while (!endOfMessage)
                {
                    (bytesRead, endOfMessage) = await conn.Transport.ReceiveAsync(buffer.AsMemory(), ct);
                    if (bytesRead == 0) break;
                    if (assembled + bytesRead > largeBuffer.Length)
                    {
                        var newBuf = ArrayPool<byte>.Shared.Rent(largeBuffer.Length * 2);
                        largeBuffer.AsSpan(0, assembled).CopyTo(newBuf);
                        ArrayPool<byte>.Shared.Return(largeBuffer);
                        largeBuffer = newBuf;
                    }
                    buffer.AsSpan(0, bytesRead).CopyTo(largeBuffer.AsSpan(assembled));
                    assembled += bytesRead;
                }
                if (bytesRead == 0) break;
                frameBytes = largeBuffer;
                totalLength = assembled;
            }
            else
            {
                frameBytes = buffer;
                totalLength = bytesRead;
            }

            // ... rest of switch (frameType) dispatch unchanged ...
```

The catch block changes from `WebSocketException` to a generic catch:

```csharp
    catch (OperationCanceledException) { }
    catch (Exception ex) { _logger.LogWarning("Receive error ({Transport}): {Error}", conn.TransportType, ex.Message); }
```

- [ ] **Step 5: Update DisposeAsync**

Replace `WebSocket` references in DisposeAsync:

```csharp
public async ValueTask DisposeAsync()
{
    _disposed = true;
    foreach (var conn in _connections)
    {
        conn.ReceiveCts?.Cancel();
        if (conn.ReceiveLoop is not null)
            try { await conn.ReceiveLoop; } catch { }
        try { await conn.Transport.CloseAsync(); } catch { }
        await conn.Transport.DisposeAsync();
        conn.ReceiveCts?.Dispose();
    }
    _connections.Clear();
}
```

- [ ] **Step 6: Verify it compiles**

Run: `dotnet build src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj -v q`
Expected: Build succeeded, 0 errors

- [ ] **Step 7: Run existing RPC stress tests to verify nothing broke**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "Name~HighConcurrency|Name~LargePayload|Name~SmallPayload|Name~BurstTraffic|Name~LargeResponse|Name~MultipleClients" -v q`
Expected: All pass (tests use WebSocket which is now wrapped in `WebSocketBoltConnection` via the negotiator)

- [ ] **Step 8: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Client/BoltClient.cs
git commit -m "refactor(bolt): BoltClient uses IBoltConnection — transport-agnostic RPC/streaming"
```

---

## Task 7: Update BoltServer to Use IBoltConnection

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs`
- Modify: `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs`

- [ ] **Step 1: Update BoltHubConnection to wrap IBoltConnection**

Replace the `BoltHubConnection` class (currently lines ~1133-1213) to use `IBoltConnection` instead of `WebSocket`:

```csharp
public sealed class BoltHubConnection
{
    private readonly IBoltConnection _transport;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string StreamId { get; } = Guid.NewGuid().ToString("N");
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int ServiceHash { get; set; }
    public BoltTransport TransportType => _transport.TransportType;
    public bool IsAlive => _transport.IsConnected;

    public long PendingBytes => Interlocked.Read(ref _pendingBytes);
    private long _pendingBytes;

    public const long BackpressureDropThreshold = 1024 * 1024;
    public const long BackpressureFeedbackThreshold = 2 * 1024 * 1024;
    public bool IsUnderPressure => PendingBytes > BackpressureDropThreshold;

    public BoltHubConnection(IBoltConnection transport) => _transport = transport;

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        Interlocked.Add(ref _pendingBytes, data.Length);
        if (_sendLock.Wait(0))
        {
            try
            {
                if (_transport.IsConnected)
                {
                    var task = _transport.SendAsync(data, ct);
                    if (task.IsCompleted)
                    {
                        Interlocked.Add(ref _pendingBytes, -data.Length);
                        return task;
                    }
                    return AwaitAndTrack(task, data.Length);
                }
                Interlocked.Add(ref _pendingBytes, -data.Length);
                return ValueTask.CompletedTask;
            }
            finally { _sendLock.Release(); }
        }
        return SendSlowAsync(data, ct);
    }

    private async ValueTask AwaitAndTrack(ValueTask task, int size)
    {
        try { await task; }
        finally { Interlocked.Add(ref _pendingBytes, -size); }
    }

    private async ValueTask SendSlowAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            if (_transport.IsConnected)
                await _transport.SendAsync(data, ct);
        }
        finally
        {
            _sendLock.Release();
            Interlocked.Add(ref _pendingBytes, -data.Length);
        }
    }
}
```

Add `using Bolt.Protocol.Transport;` to the server's imports.

- [ ] **Step 2: Update HandleConnectionAsync signature and receive loop**

Change signature from `WebSocket` to `IBoltConnection`:

```csharp
public async Task HandleConnectionAsync(IBoltConnection transport, CancellationToken ct)
{
    var connection = new BoltHubConnection(transport);
    var receiveBuffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
    byte[]? largeBuffer = null;

    try
    {
        while (transport.IsConnected && !ct.IsCancellationRequested)
        {
            var (bytesRead, endOfMessage) = await transport.ReceiveAsync(receiveBuffer.AsMemory(), ct);
            if (bytesRead == 0) break;

            byte[] frameBytes;
            int totalLength;
            if (!endOfMessage)
            {
                var assembled = bytesRead;
                var capacity = Math.Max(bytesRead * 4, 512 * 1024);
                if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
                largeBuffer = ArrayPool<byte>.Shared.Rent(capacity);
                receiveBuffer.AsSpan(0, bytesRead).CopyTo(largeBuffer);

                while (!endOfMessage)
                {
                    (bytesRead, endOfMessage) = await transport.ReceiveAsync(receiveBuffer.AsMemory(), ct);
                    if (bytesRead == 0) break;
                    if (assembled + bytesRead > largeBuffer.Length)
                    {
                        var newBuf = ArrayPool<byte>.Shared.Rent(largeBuffer.Length * 2);
                        largeBuffer.AsSpan(0, assembled).CopyTo(newBuf);
                        ArrayPool<byte>.Shared.Return(largeBuffer);
                        largeBuffer = newBuf;
                    }
                    receiveBuffer.AsSpan(0, bytesRead).CopyTo(largeBuffer.AsSpan(assembled));
                    assembled += bytesRead;
                }
                if (bytesRead == 0) break;
                frameBytes = largeBuffer;
                totalLength = assembled;
            }
            else
            {
                frameBytes = receiveBuffer;
                totalLength = bytesRead;
            }

            await ProcessFrameAsync(connection, frameBytes, totalLength, ct);
        }
    }
    // ... same catch/finally pattern, replace WebSocketException with Exception ...
```

Update the catch blocks to remove `WebSocketException` references and use generic `Exception`.

Update the finally block:

```csharp
    finally
    {
        ArrayPool<byte>.Shared.Return(receiveBuffer);
        if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
        RemoveConnection(connection);

        try { await transport.CloseAsync(CancellationToken.None); } catch { }
    }
```

- [ ] **Step 3: Update MapBolt to accept all transports**

Replace `BoltServerExtensions.cs` `MapBolt` method:

```csharp
public static IEndpointRouteBuilder MapBolt(this IEndpointRouteBuilder endpoints, string path = "/bolt")
{
    // WebSocket endpoint — universal fallback
    endpoints.Map(path, async (HttpContext context) =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("WebSocket connections only");
            return;
        }

        var server = context.RequestServices.GetRequiredService<BoltServer>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var transport = new WebSocketBoltConnection(webSocket);
        await server.HandleConnectionAsync(transport, context.RequestAborted);
    });

    return endpoints;
}
```

Add `using Bolt.Protocol.Transport;` to the imports.

- [ ] **Step 4: Add StartQuicListenerAsync to BoltServer**

Add this method to `BoltServer`:

```csharp
/// <summary>
/// Start a raw QUIC listener for Bolt connections. Accepts QUIC clients
/// and routes them through the same HandleConnectionAsync as WebSocket clients.
/// </summary>
public async Task StartQuicListenerAsync(IPEndPoint endpoint, X509Certificate2 certificate, CancellationToken ct)
{
    if (!QuicListener.IsSupported)
    {
        _logger.LogWarning("QUIC is not supported on this platform — skipping QUIC listener");
        return;
    }

    var listener = await QuicListener.ListenAsync(new QuicListenerOptions
    {
        ListenEndPoint = endpoint,
        ApplicationProtocols = [new SslApplicationProtocol("bolt")],
        ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
        {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            MaxInboundBidirectionalStreams = 256,
            ServerAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                ApplicationProtocols = [new SslApplicationProtocol("bolt")]
            }
        })
    }, ct);

    _logger.LogInformation("Bolt QUIC listener started on {Endpoint}", endpoint);

    _ = Task.Run(async () =>
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var quicConn = await listener.AcceptConnectionAsync(ct);
                var transport = new QuicBoltConnection(quicConn);
                await transport.AcceptPrimaryStreamAsync(ct);
                _ = HandleConnectionAsync(transport, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "QUIC listener error"); }
        finally { await listener.DisposeAsync(); }
    }, ct);
}
```

Add these imports to BoltServer.cs:

```csharp
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Bolt.Protocol.Transport;
```

- [ ] **Step 5: Verify everything compiles**

Run: `dotnet build src/Tests/Bolt.Tests/Bolt.Tests.csproj -v q`
Expected: Build succeeded, 0 errors

- [ ] **Step 6: Run existing tests to verify nothing broke**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "Name~HighConcurrency|Name~LargePayload|Name~SmallPayload|Name~BurstTraffic|Name~LargeResponse|Name~MultipleClients|Name~RpcTimeout" -v q`
Expected: All pass

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor(bolt): BoltServer uses IBoltConnection — unified transport for hub routing

BoltServer.HandleConnectionAsync takes IBoltConnection.
Add StartQuicListenerAsync for raw QUIC listener.
MapBolt wraps WebSocket in WebSocketBoltConnection."
```

---

## Task 8: Update BoltClientExtensions

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Client/BoltClientExtensions.cs`

- [ ] **Step 1: Update builder to expose transport configuration**

Update the `BoltClientBuilder` to allow transport configuration:

```csharp
/// <summary>
/// Configure preferred transports. Default: QUIC, WebTransport, WebSocket.
/// Example: bolt.WithTransports(BoltTransport.WebSocket) to force WebSocket only.
/// </summary>
public BoltClientBuilder WithTransports(params BoltTransport[] transports)
{
    Options.PreferredTransports = transports;
    return this;
}

/// <summary>Set the timeout per transport attempt in milliseconds. Default: 3000.</summary>
public BoltClientBuilder WithTransportTimeout(int ms)
{
    Options.TransportAttemptTimeoutMs = ms;
    return this;
}
```

Update the doc comments on `WithMinConnections`/`WithMaxConnections` to remove "WebSocket" specificity:

```csharp
/// <summary>Set the minimum number of connections. Default: 1.</summary>
public BoltClientBuilder WithMinConnections(int min) { Options.MinConnections = min; return this; }

/// <summary>Set the maximum number of connections. Default: ProcessorCount.</summary>
public BoltClientBuilder WithMaxConnections(int max) { Options.MaxConnections = max; return this; }
```

- [ ] **Step 2: Verify it compiles and commit**

Run: `dotnet build src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj -v q`
Expected: Build succeeded

```bash
git add src/Libraries/Bolt/Bolt.Client/BoltClientExtensions.cs
git commit -m "feat(bolt): expose transport preferences in BoltClientBuilder"
```

---

## Task 9: Delete Old QUIC Implementations

**Files:**
- Delete: `src/Infrastructure/XFramework.Integration/ThinProtocol/QuicBoltClient.cs`
- Delete: `src/Infrastructure/XFramework.Integration/ThinProtocol/QuicDirectClient.cs`
- Delete: `src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/QuicBoltServer.cs`
- Delete: `src/Modules/XFramework.Bolt/Bolt.Domain.Shared/Protocol/BoltHubCodec.cs`

- [ ] **Step 1: Delete the files**

```bash
rm src/Infrastructure/XFramework.Integration/ThinProtocol/QuicBoltClient.cs
rm src/Infrastructure/XFramework.Integration/ThinProtocol/QuicDirectClient.cs
rm src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/QuicBoltServer.cs
rm src/Modules/XFramework.Bolt/Bolt.Domain.Shared/Protocol/BoltHubCodec.cs
```

- [ ] **Step 2: Fix any compilation errors from dangling references**

Search for references to `QuicBoltHubClient`, `QuicDirectClient`, `QuicDirectServer`, `QuicBoltServer`, `BoltHubCodec` across the codebase and remove or update them.

Run: `dotnet build src/Tests/Bolt.Tests/Bolt.Tests.csproj -v q`

Fix any errors — likely in `IdentityServer.Benchmarks` which referenced the old QUIC classes. Those benchmark methods should be updated to use `BoltClient` with `BoltTransport.Quic` in the `PreferredTransports` option, or temporarily commented out until the QUIC integration tests are written.

- [ ] **Step 3: Verify full solution builds**

Run: `dotnet build -v q` (from solution root)
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore(bolt): delete old standalone QUIC classes and BoltHubCodec

Replaced by unified BoltClient + IBoltConnection transport abstraction.
QuicBoltHubClient → BoltClient + QuicBoltConnection
QuicDirectClient/Server → BoltClient/BoltServer + QuicBoltConnection
BoltHubCodec → BoltCodec (unified wire protocol)"
```

---

## Task 10: Integration Tests — QUIC Transport

**Files:**
- Modify: `src/Tests/Bolt.Tests/TransportTests.cs`

- [ ] **Step 1: Write QUIC integration test for RPC through hub**

```csharp
[TestFixture]
public class QuicTransportIntegrationTests
{
    private WebApplication _hubApp = null!;
    private BoltServer _server = null!;
    private X509Certificate2 _cert = null!;
    private const int WsPort = 18700;
    private const int QuicPort = 18701;

    [OneTimeSetUp]
    public async Task Setup()
    {
        // Generate self-signed cert for QUIC
        _cert = GenerateSelfSignedCert();

        // Start hub with both WebSocket and QUIC
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{WsPort}");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _hubApp = builder.Build();
        _hubApp.UseWebSockets();
        _hubApp.MapBolt("/bolt");
        _hubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _hubApp.RunAsync());

        // Wait for HTTP health
        await WaitForHealth($"http://localhost:{WsPort}/health");

        // Start QUIC listener
        _server = _hubApp.Services.GetRequiredService<BoltServer>();
        await _server.StartQuicListenerAsync(
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, QuicPort),
            _cert, CancellationToken.None);
    }

    [Test]
    public async Task Quic_RpcCall_WorksThroughHub()
    {
        if (!System.Net.Quic.QuicConnection.IsSupported)
        {
            Assert.Ignore("QUIC not supported on this platform");
            return;
        }

        var loggerFactory = _hubApp.Services.GetRequiredService<ILoggerFactory>();
        var quicOpts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            PreferredTransports = [BoltTransport.Quic]
        };

        // Service connects via QUIC
        var service = new BoltClient(
            new Uri($"quic://localhost:{QuicPort}/bolt"),
            "quic_svc", "QuicSvc", quicOpts, loggerFactory.CreateLogger<BoltClient>());
        service.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((System.Net.HttpStatusCode.OK, payload)));
        await service.ConnectAsync();

        // Caller also connects via QUIC
        var caller = new BoltClient(
            new Uri($"quic://localhost:{QuicPort}/bolt"),
            "quic_caller", "QuicCaller", quicOpts, loggerFactory.CreateLogger<BoltClient>());
        await caller.ConnectAsync();

        // RPC through hub
        var testData = new byte[1024];
        Random.Shared.NextBytes(testData);
        var (status, response) = await caller.InvokeAsync("quic_svc", "echo", testData);

        status.Should().Be(System.Net.HttpStatusCode.OK);
        response.ToArray().Should().Equal(testData);

        await caller.DisposeAsync();
        await service.DisposeAsync();
    }

    [Test]
    public async Task MixedTransport_QuicCaller_WebSocketService()
    {
        if (!System.Net.Quic.QuicConnection.IsSupported)
        {
            Assert.Ignore("QUIC not supported on this platform");
            return;
        }

        var loggerFactory = _hubApp.Services.GetRequiredService<ILoggerFactory>();

        // Service connects via WebSocket
        var wsOpts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            PreferredTransports = [BoltTransport.WebSocket]
        };
        var service = new BoltClient(
            new Uri($"ws://localhost:{WsPort}/bolt"),
            "ws_svc", "WsSvc", wsOpts, loggerFactory.CreateLogger<BoltClient>());
        service.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((System.Net.HttpStatusCode.OK, payload)));
        await service.ConnectAsync();

        // Caller connects via QUIC
        var quicOpts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            PreferredTransports = [BoltTransport.Quic]
        };
        var caller = new BoltClient(
            new Uri($"quic://localhost:{QuicPort}/bolt"),
            "quic_caller2", "QuicCaller2", quicOpts, loggerFactory.CreateLogger<BoltClient>());
        await caller.ConnectAsync();

        // Cross-transport RPC
        var testData = new byte[512];
        Random.Shared.NextBytes(testData);
        var (status, response) = await caller.InvokeAsync("ws_svc", "echo", testData);

        status.Should().Be(System.Net.HttpStatusCode.OK);
        response.ToArray().Should().Equal(testData);

        await caller.DisposeAsync();
        await service.DisposeAsync();
    }

    [Test]
    public async Task TransportFallback_QuicUnavailable_FallsBackToWebSocket()
    {
        var loggerFactory = _hubApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            TransportAttemptTimeoutMs = 1000,
            // Try QUIC on a port where no QUIC listener exists, then fall back
            PreferredTransports = [BoltTransport.Quic, BoltTransport.WebSocket]
        };

        // Connect to WebSocket port — QUIC will fail (no QUIC listener on WsPort), WebSocket will succeed
        var client = new BoltClient(
            new Uri($"ws://localhost:{WsPort}/bolt"),
            "fallback_client", "FallbackClient", opts, loggerFactory.CreateLogger<BoltClient>());
        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();

        await client.DisposeAsync();
    }

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        try { await _hubApp.StopAsync(); } catch { }
        _cert?.Dispose();
    }

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=localhost", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}
```

- [ ] **Step 2: Run the integration tests**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "ClassName~QuicTransportIntegrationTests" -v n`
Expected: All pass (or skipped if QUIC not supported on the machine)

- [ ] **Step 3: Commit**

```bash
git add src/Tests/Bolt.Tests/TransportTests.cs
git commit -m "test(bolt): QUIC transport integration tests — hub routing, mixed transport, fallback"
```

---

## Task 11: Transport Benchmarks

**Files:**
- Modify: `src/Tests/Bolt.Tests/PayloadBenchmarks.cs`

- [ ] **Step 1: Add QUIC benchmark variant**

Add a QUIC Bolt client alongside the existing WebSocket client in the benchmark setup. Add a `Bolt_Quic_Echo` benchmark method that uses the QUIC transport. Keep `Bolt_Echo` (WebSocket) as baseline.

The benchmark GlobalSetup creates:
- Bolt hub with both WebSocket + QUIC listeners
- `_boltService` and `_boltCaller` connected via WebSocket (existing)
- `_boltQuicService` and `_boltQuicCaller` connected via QUIC (new)
- gRPC server and client (existing)

New benchmark method:

```csharp
[Benchmark]
public async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> Bolt_Quic_Echo()
{
    return await _boltQuicCaller.InvokeAsync("quic_payload_svc", "echo", _boltPayload);
}
```

- [ ] **Step 2: Run benchmarks**

Run: `dotnet run --project src/Tests/Bolt.Tests/Bolt.Tests.csproj -c Release -- --filter "*PayloadBenchmarks*"`
Expected: Results for Bolt_Echo (WebSocket), Bolt_Quic_Echo, and GRPC_Echo across all payload sizes.

- [ ] **Step 3: Commit**

```bash
git add src/Tests/Bolt.Tests/PayloadBenchmarks.cs
git commit -m "bench(bolt): add QUIC transport benchmark — WebSocket vs QUIC vs gRPC payload comparison"
```

---

## Task 12: Final Verification & Push

- [ ] **Step 1: Run all Bolt tests**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj -v n`
Expected: All tests pass

- [ ] **Step 2: Build full solution**

Run: `dotnet build -v q`
Expected: Build succeeded, 0 errors (ignoring warnings)

- [ ] **Step 3: Push to remote**

```bash
git push
```
