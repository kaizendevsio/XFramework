using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using MemoryPack;
using Microsoft.Extensions.Logging;


namespace Bolt.Client;

/// <summary>
/// Thin binary WebSocket client that replaces SignalR for .NET-to-.NET RPC.
/// Single serialization pass (MemoryPack only), no MessagePack/SignalR overhead.
///
/// Features:
/// - Exponential backoff + jitter reconnection
/// - Pooled RPC completion (PooledRpcCall)
/// - Offline message queue
/// - Handler routing by FNV-1a command hash
/// </summary>
public sealed class BoltClient : IAsyncDisposable
{
    private readonly Uri _serverUri;
    private readonly string _clientId;
    private readonly string _clientName;
    private readonly BoltClientOptions _config;
    private readonly ILogger _logger;

    // Connection pool — multiple WebSocket connections for throughput
    private readonly List<BoltConnection> _connections = [];
    private int _roundRobin;
    private volatile bool _isRegistered;
    private volatile bool _disposed;

    // Pending RPC calls — shared across all connections, response frames resolve these
    private readonly ConcurrentDictionary<Guid, PooledRpcCall> _pendingCalls = new();

    // Handler registry — maps command hash to handler delegate
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _handlers = new();

    // Hash cache — computed once per unique string, reused every call
    private readonly ConcurrentDictionary<string, int> _hashCache = new();

    // Offline queue
    private readonly ConcurrentQueue<byte[]> _offlineQueue = new();

    // Streaming — active streams by streamId
    private readonly ConcurrentDictionary<Guid, BoltStream> _activeStreams = new();

    // Stream handler registry — maps command hash to stream handler
    private readonly ConcurrentDictionary<int, Func<BoltStream, Task>> _streamHandlers = new();

    // Cached timeout for RPC calls
    private TimeSpan _rpcTimeout;

    // Call management
    private readonly ConcurrentDictionary<Guid, ClientCallInfo> _activeCalls = new();
    private readonly ConcurrentDictionary<Guid, BoltMediaStream> _mediaStreams = new();

    // Call events
    public event Func<IncomingCallInfo, Task>? OnIncomingCall;
    public event Func<Guid, Task>? OnCallAnswered;
    public event Func<Guid, string?, Task>? OnCallRejected;
    public event Func<Guid, Task>? OnCallEnded;
    public event Action<Guid>? OnKeyframeRequested;

    public bool IsConnected => _connections.Count > 0 && _isRegistered;

    public BoltClient(Uri serverUri, string clientId, string clientName, BoltClientOptions config, ILogger logger)
    {
        _serverUri = serverUri;
        _clientId = clientId;
        _clientName = clientName;
        _config = config;
        _logger = logger;
        _rpcTimeout = TimeSpan.FromSeconds(config.RpcTimeoutSeconds > 0 ? config.RpcTimeoutSeconds : 30);
    }

    /// <summary>
    /// Connect to the thin Bolt server and register.
    /// Creates MinConnections connections (default 1, scales up dynamically).
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var minConns = Math.Max(1, _config.MinConnections);
        for (int i = 0; i < minConns; i++)
        {
            var conn = await CreateConnectionAsync(ct);
            _connections.Add(conn);
        }

        _isRegistered = true;
        _logger.LogInformation("Bolt client connected: {ClientId} ({ClientName}), {Count} connection(s)",
            _clientId, _clientName, _connections.Count);

        await DrainOfflineQueueAsync(ct);
    }

    private async Task<BoltConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(_serverUri, ct);

        // Register this connection
        var writer = new ArrayBufferWriter<byte>(128);
        BoltCodec.WriteRegister(writer, _clientId, _clientName);
        await ws.SendAsync(writer.WrittenMemory, WebSocketMessageType.Binary, true, ct);

        var ackBuffer = new byte[2];
        var result = await ws.ReceiveAsync(ackBuffer, ct);
        if (result.Count < 2 || (FrameType)ackBuffer[0] != FrameType.RegisterAck || ackBuffer[1] != 1)
            throw new InvalidOperationException("Server rejected registration");

        var conn = new BoltConnection(ws);

        // Start receive loop for this connection
        var receiveCts = new CancellationTokenSource();
        conn.ReceiveCts = receiveCts;
        conn.ReceiveLoop = Task.Run(() => ReceiveLoopAsync(conn, receiveCts.Token));

        return conn;
    }

    /// <summary>
    /// Scale up: add a new connection when under load.
    /// </summary>
    private async Task ScaleUpAsync()
    {
        if (_connections.Count >= _config.MaxConnections) return;

        try
        {
            var conn = await CreateConnectionAsync(CancellationToken.None);
            _connections.Add(conn);
            _logger.LogInformation("Bolt connection pool scaled to {Count}", _connections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scale up Bolt connection pool");
        }
    }

    /// <summary>
    /// Connect with automatic retry and exponential backoff + jitter.
    /// </summary>
    public async Task ConnectWithRetryAsync(CancellationToken ct = default)
    {
        const int maxRetries = 100;
        var baseDelay = TimeSpan.FromMilliseconds(500);
        var maxDelay = TimeSpan.FromSeconds(30);
        var random = new Random();

        for (int attempt = 0; attempt < maxRetries && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                await ConnectAsync(ct);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Connection attempt {Attempt} failed: {Error}", attempt + 1, ex.Message);

                // Exponential backoff with jitter
                var delay = TimeSpan.FromMilliseconds(
                    Math.Min(baseDelay.TotalMilliseconds * Math.Pow(2, attempt), maxDelay.TotalMilliseconds));
                var jitter = TimeSpan.FromMilliseconds(random.Next(0, (int)(delay.TotalMilliseconds * 0.3)));
                await Task.Delay(delay + jitter, ct);

                // Reset connections for retry
                foreach (var c in _connections)
                    c.WebSocket.Dispose();
                _connections.Clear();
            }
        }

        throw new InvalidOperationException($"Failed to connect after {maxRetries} attempts");
    }

    /// <summary>
    /// Invoke a method on a remote service and wait for the response.
    /// This is the hot path — optimized for minimal allocations.
    /// </summary>
    public async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Data)> InvokeAsync(
        string recipientId, string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var requestId = Guid.NewGuid();

        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = _hashCache.GetOrAdd(commandName, BoltCodec.Fnv1aHash);

        var rpcCall = PooledRpcCall.Rent();
        _pendingCalls[requestId] = rpcCall;

        try
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteRequest(writer, requestId, recipientHash, commandHash, payload.Span);

            if (!IsConnected)
            {
                _offlineQueue.Enqueue(writer.WrittenSpan.ToArray());
                rpcCall.SetException(new InvalidOperationException("Not connected"));
            }
            else
            {
                // Round-robin across connections for load distribution
                var conn = GetConnection();
                await conn.SendAsync(writer.WrittenMemory, ct);

                // Auto-scale: if this connection is backed up, add another
                if (conn.PendingSends > _config.ScaleUpThreshold && _connections.Count < _config.MaxConnections)
                    _ = ScaleUpAsync();
            }

            using var timeoutCts = new CancellationTokenSource(_rpcTimeout);
            rpcCall.RegisterTimeout(timeoutCts.Token);

            var response = await rpcCall.GetTask();
            return (response.StatusCode, response.Data);
        }
        finally
        {
            _pendingCalls.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Invoke with typed request and response. Auto-serializes with MemoryPack.
    ///
    /// Usage: var response = await client.SendAsync&lt;MyRequest, MyResponse&gt;("service", "command", request);
    /// </summary>
    public async Task<TResponse?> SendAsync<TRequest, TResponse>(string recipientId, string commandName, TRequest request, CancellationToken ct = default)
    {
        var payload = MemoryPackSerializer.Serialize(request);
        var result = await InvokeAsync(recipientId, commandName, payload, ct);
        return result.Data.Length > 0 ? MemoryPackSerializer.Deserialize<TResponse>(result.Data.Span) : default;
    }

    /// <summary>
    /// Invoke with typed request, no response data expected (command pattern).
    ///
    /// Usage: var status = await client.SendAsync("service", "command", request);
    /// </summary>
    public async Task<HttpStatusCode> SendAsync<TRequest>(string recipientId, string commandName, TRequest request, CancellationToken ct = default)
    {
        var payload = MemoryPackSerializer.Serialize(request);
        var result = await InvokeAsync(recipientId, commandName, payload, ct);
        return result.StatusCode;
    }

    private BoltConnection GetConnection()
    {
        var count = _connections.Count;
        if (count == 1) return _connections[0];
        var idx = (uint)Interlocked.Increment(ref _roundRobin) % count;
        return _connections[(int)idx];
    }

    /// <summary>
    /// Register a handler for incoming request frames (this client is the recipient).
    /// </summary>
    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = BoltCodec.Fnv1aHash(commandName);
        _handlers[hash] = handler;
        _logger.LogDebug("Registered Bolt handler for {CommandName} [hash={Hash}]", commandName, hash);
    }

    /// <summary>
    /// Register a handler for incoming streams.
    /// Called when a remote client opens a stream to this client.
    /// </summary>
    public void RegisterStreamHandler(string commandName, Func<BoltStream, Task> handler)
    {
        var hash = BoltCodec.Fnv1aHash(commandName);
        _streamHandlers[hash] = handler;
        _logger.LogDebug("Registered Bolt stream handler for {CommandName} [hash={Hash}]", commandName, hash);
    }

    /// <summary>
    /// Open a bidirectional stream to a remote service.
    /// Returns a BoltStream for sending/receiving raw bytes or typed objects.
    /// </summary>
    public async Task<BoltStream> OpenStreamAsync(string recipientId, string commandName, CancellationToken ct = default)
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected");

        var streamId = Guid.NewGuid();
        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = _hashCache.GetOrAdd(commandName, BoltCodec.Fnv1aHash);

        var conn = GetConnection();
        var stream = new BoltStream(streamId, conn);
        _activeStreams[streamId] = stream;

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteStreamOpen(writer, streamId, recipientHash, commandHash);
        await conn.SendAsync(writer.WrittenMemory, ct);

        return stream;
    }

    /// <summary>
    /// Stream an IAsyncEnumerable to a remote service.
    /// Each item is serialized with MemoryPack and sent as a StreamData frame.
    /// Stream is automatically closed when the enumerable completes.
    /// </summary>
    public async Task StreamAsync<T>(string recipientId, string commandName, IAsyncEnumerable<T> items, CancellationToken ct = default)
    {
        await using var stream = await OpenStreamAsync(recipientId, commandName, ct);
        await stream.SendAllAsync(items, ct);
    }

    /// <summary>
    /// Register a typed stream handler. When a remote client opens a stream with the
    /// given command name, the handler receives an IAsyncEnumerable of deserialized items.
    /// </summary>
    public void RegisterStreamHandler<T>(string commandName, Func<IAsyncEnumerable<T>, BoltStream, Task> handler)
    {
        RegisterStreamHandler(commandName, async (stream) =>
        {
            var items = stream.ReadAllAsync<T>();
            await handler(items, stream);
        });
    }

    private async Task ReceiveLoopAsync(BoltConnection conn, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (!ct.IsCancellationRequested && conn.WebSocket.State == WebSocketState.Open)
            {
                var result = await conn.WebSocket.ReceiveAsync(buffer.AsMemory(), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType != WebSocketMessageType.Binary || result.Count == 0)
                    continue;

                var data = buffer.AsSpan(0, result.Count);
                var frameType = BoltCodec.PeekFrameType(data);

                switch (frameType)
                {
                    case FrameType.Response:
                        HandleIncomingResponse(data);
                        break;

                    case FrameType.Request:
                        var reqData = buffer.AsSpan(0, result.Count).ToArray();
                        _ = HandleIncomingRequestAsync(conn, reqData, reqData.Length, ct);
                        break;

                    case FrameType.StreamOpen:
                        HandleStreamOpen(conn, data, ct);
                        break;

                    case FrameType.StreamData:
                        HandleStreamData(data);
                        break;

                    case FrameType.StreamClose:
                        HandleStreamClose(data);
                        break;

                    case FrameType.MediaFrame:
                        HandleMediaFrame(data);
                        break;

                    case FrameType.MediaConfig:
                        HandleMediaConfig(conn, data);
                        break;

                    case FrameType.MediaFeedback:
                        HandleMediaFeedback(data);
                        break;

                    case FrameType.MediaKeyRequest:
                        HandleMediaKeyRequest(data);
                        break;

                    case FrameType.CallSignal:
                        var csData = data.ToArray();
                        _ = HandleCallSignalAsync(csData, result.Count);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            _logger.LogWarning("WebSocket receive error: {Error}", ex.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            if (!_disposed)
            {
                // Remove dead connection, cancel pending RPCs only if ALL connections dead
                _connections.Remove(conn);
                if (_connections.Count == 0)
                {
                    _isRegistered = false;
                    foreach (var (id, _) in _pendingCalls)
                        if (_pendingCalls.TryRemove(id, out var call))
                            call.SetException(new InvalidOperationException("Connection lost"));
                    _ = Task.Run(() => ReconnectAsync());
                }
            }
        }
    }

    private void HandleIncomingResponse(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadResponse(data, out var frame, out _))
            return;

        if (_pendingCalls.TryRemove(frame.RequestId, out var rpcCall))
        {
            var payload = frame.PayloadLength > 0
                ? frame.GetPayload(data).ToArray()
                : Array.Empty<byte>();
            rpcCall.SetResult(new BoltRpcResponse { StatusCode = frame.StatusCode, Data = payload });
        }
    }

    private void HandleStreamOpen(BoltConnection conn, ReadOnlySpan<byte> data, CancellationToken ct)
    {
        if (!BoltCodec.TryReadStreamOpen(data, out var streamId, out _, out var commandHash))
            return;

        var stream = new BoltStream(streamId, conn);
        _activeStreams[streamId] = stream;

        if (_streamHandlers.TryGetValue(commandHash, out var handler))
        {
            // Dispatch stream handler off the receive loop
            _ = Task.Run(async () =>
            {
                try { await handler(stream); }
                catch (Exception ex) { _logger.LogError(ex, "Stream handler error for streamId={StreamId}", streamId); }
                finally { _activeStreams.TryRemove(streamId, out _); }
            }, ct);
        }
    }

    private void HandleStreamData(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamData(data, out var streamId, out var payloadOffset, out var payloadLength, out _))
            return;

        if (_activeStreams.TryGetValue(streamId, out var stream))
        {
            // Copy payload since buffer will be reused
            var chunk = data.Slice(payloadOffset, payloadLength).ToArray();
            stream.EnqueueInbound(chunk);
        }
    }

    private void HandleStreamClose(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamClose(data, out var streamId, out var statusCode))
            return;

        if (_activeStreams.TryRemove(streamId, out var stream))
            stream.MarkClosed(statusCode);
    }

    private async Task HandleIncomingRequestAsync(BoltConnection conn, byte[] data, int length, CancellationToken ct)
    {
        var span = data.AsSpan(0, length);
        if (!BoltCodec.TryReadRequest(span, out var frame, out _))
            return;

        if (_handlers.TryGetValue(frame.CommandHash, out var handler))
        {
            try
            {
                var payload = frame.GetPayload(data.AsMemory(0, length));
                var (statusCode, responsePayload) = await handler(payload, frame.RequestId);

                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteResponse(writer, frame.RequestId, statusCode, responsePayload.Span);
                await conn.SendAsync(writer.WrittenMemory, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler error for command hash {CommandHash}", frame.CommandHash);
                var errWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.InternalServerError, ReadOnlySpan<byte>.Empty);
                await conn.SendAsync(errWriter.WrittenMemory, ct);
            }
        }
        else
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(writer, frame.RequestId, HttpStatusCode.NotImplemented, ReadOnlySpan<byte>.Empty);
            await conn.SendAsync(writer.WrittenMemory, ct);
        }
    }

    // ── Call API ─────────────────────────────────────────────

    public async Task<Guid> StartCallAsync(string recipientId, bool video = false)
    {
        var callId = Guid.NewGuid();
        _activeCalls[callId] = new ClientCallInfo { CallId = callId, IsOutgoing = true, RemoteClientId = recipientId };

        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var payload = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload, recipientHash);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Initiate, payload);
        await GetConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();

        return callId;
    }

    public async Task AnswerCallAsync(Guid callId)
    {
        if (_activeCalls.TryGetValue(callId, out var call))
            call.Status = ClientCallStatus.Active;

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Answer, ReadOnlySpan<byte>.Empty);
        await GetConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();
    }

    public async Task RejectCallAsync(Guid callId, string? reason = null)
    {
        _activeCalls.TryRemove(callId, out _);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Reject, ReadOnlySpan<byte>.Empty);
        await GetConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();
    }

    public async Task EndCallAsync(Guid callId)
    {
        _activeCalls.TryRemove(callId, out _);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.End, ReadOnlySpan<byte>.Empty);
        await GetConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();

        // Clean up media streams for this call
        foreach (var (streamId, stream) in _mediaStreams)
        {
            if (stream.CallId == callId)
            {
                _mediaStreams.TryRemove(streamId, out _);
                await stream.DisposeAsync();
            }
        }
    }

    public BoltMediaStream? GetMediaStream(Guid streamId)
        => _mediaStreams.TryGetValue(streamId, out var stream) ? stream : null;

    // ── Media frame handlers ────────────────────────────────

    private void HandleMediaFrame(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadMediaFrame(data, out var header)) return;
        if (_mediaStreams.TryGetValue(header.StreamId, out var stream))
        {
            var payload = header.GetPayload(data).ToArray();
            stream.EnqueueFrame(header.SequenceNumber, header.Timestamp, payload, header.Flags);
        }
    }

    private void HandleMediaConfig(BoltConnection conn, ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadMediaConfig(data, out var config)) return;

        var isAudio = config.MediaType == MediaType.Audio;
        var stream = new BoltMediaStream(conn, config.StreamId, config.CallId, isAudio);
        _mediaStreams[config.StreamId] = stream;

        if (_activeCalls.TryGetValue(config.CallId, out var call))
        {
            if (isAudio) call.AudioStreamId = config.StreamId;
            else call.VideoStreamId = config.StreamId;
        }
    }

    private void HandleMediaFeedback(ReadOnlySpan<byte> data)
    {
        // Feedback from receiver — used for adaptive bitrate (handled by caller)
        // Future: route to BandwidthEstimator
    }

    private void HandleMediaKeyRequest(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadMediaKeyRequest(data, out var streamId)) return;
        OnKeyframeRequested?.Invoke(streamId);
    }

    private async Task HandleCallSignalAsync(byte[] data, int length)
    {
        if (!BoltCodec.TryReadCallSignal(data.AsSpan(0, length), out var header)) return;

        switch (header.SignalType)
        {
            case SignalType.Initiate:
                var incomingCall = new ClientCallInfo
                {
                    CallId = header.CallId, IsOutgoing = false, Status = ClientCallStatus.Ringing
                };
                _activeCalls[header.CallId] = incomingCall;
                if (OnIncomingCall != null)
                    await OnIncomingCall(new IncomingCallInfo(header.CallId, "", false));
                break;

            case SignalType.Ring:
                if (_activeCalls.TryGetValue(header.CallId, out var ringing))
                    ringing.Status = ClientCallStatus.Ringing;
                break;

            case SignalType.Answer:
                if (_activeCalls.TryGetValue(header.CallId, out var answered))
                    answered.Status = ClientCallStatus.Active;
                if (OnCallAnswered != null)
                    await OnCallAnswered(header.CallId);
                break;

            case SignalType.Reject:
                _activeCalls.TryRemove(header.CallId, out _);
                if (OnCallRejected != null)
                    await OnCallRejected(header.CallId, null);
                break;

            case SignalType.End:
                _activeCalls.TryRemove(header.CallId, out _);
                // Clean up media streams
                foreach (var (streamId, stream) in _mediaStreams)
                {
                    if (stream.CallId == header.CallId)
                    {
                        _mediaStreams.TryRemove(streamId, out _);
                        await stream.DisposeAsync();
                    }
                }
                if (OnCallEnded != null)
                    await OnCallEnded(header.CallId);
                break;
        }
    }

    private async Task DrainOfflineQueueAsync(CancellationToken ct)
    {
        var drained = 0;
        while (_offlineQueue.TryDequeue(out var frame))
        {
            await GetConnection().SendAsync(frame, ct);
            drained++;
        }
        if (drained > 0)
            _logger.LogInformation("Drained {Count} offline messages", drained);
    }

    private async Task ReconnectAsync()
    {
        _logger.LogInformation("Attempting reconnection...");
        try
        {
            await ConnectWithRetryAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconnection failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        foreach (var conn in _connections)
        {
            conn.ReceiveCts?.Cancel();
            if (conn.ReceiveLoop is not null)
                try { await conn.ReceiveLoop; } catch { }

            try
            {
                if (conn.WebSocket.State == WebSocketState.Open)
                    await conn.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
            conn.WebSocket.Dispose();
            conn.ReceiveCts?.Dispose();
        }

        _connections.Clear();
    }
}

/// <summary>
/// A single WebSocket connection in the Bolt client pool.
/// Each has its own send lock (WebSocket only supports one concurrent send).
/// </summary>
public sealed class BoltConnection
{
    public ClientWebSocket WebSocket { get; }
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _pendingSends;

    public CancellationTokenSource? ReceiveCts { get; set; }
    public Task? ReceiveLoop { get; set; }
    public int PendingSends => _pendingSends;

    public BoltConnection(ClientWebSocket webSocket) => WebSocket = webSocket;

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        Interlocked.Increment(ref _pendingSends);

        if (_sendLock.Wait(0))
        {
            try
            {
                if (WebSocket.State == WebSocketState.Open)
                {
                    var task = WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
                    if (task.IsCompleted)
                    {
                        Interlocked.Decrement(ref _pendingSends);
                        return task;
                    }
                    return AwaitAndDecrement(task);
                }
                Interlocked.Decrement(ref _pendingSends);
                return ValueTask.CompletedTask;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        return SendSlowAsync(data, ct);
    }

    private async ValueTask AwaitAndDecrement(ValueTask task)
    {
        try { await task; }
        finally { Interlocked.Decrement(ref _pendingSends); }
    }

    private async ValueTask SendSlowAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            if (WebSocket.State == WebSocketState.Open)
                await WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
        }
        finally
        {
            _sendLock.Release();
            Interlocked.Decrement(ref _pendingSends);
        }
    }
}

/// <summary>
/// Response data from a thin protocol RPC call.
/// </summary>
public struct BoltRpcResponse
{
    public HttpStatusCode StatusCode;
    public ReadOnlyMemory<byte> Data;
}

/// <summary>Information about an incoming call.</summary>
public record IncomingCallInfo(Guid CallId, string CallerClientId, bool VideoRequested);

internal enum ClientCallStatus { Initiating, Ringing, Active, Held, Ended }

internal sealed class ClientCallInfo
{
    public Guid CallId { get; init; }
    public bool IsOutgoing { get; init; }
    public string RemoteClientId { get; set; } = "";
    public ClientCallStatus Status { get; set; } = ClientCallStatus.Initiating;
    public Guid? AudioStreamId { get; set; }
    public Guid? VideoStreamId { get; set; }
}
