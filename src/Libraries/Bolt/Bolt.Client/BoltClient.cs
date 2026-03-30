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
/// Lean binary WebSocket client for .NET-to-.NET RPC + streaming.
/// Single serialization pass (MemoryPack only), no SignalR overhead.
///
/// Features: RPC, Push, bidirectional streaming, connection pooling, offline queue.
/// For voice/video calls, add the Bolt.Media package.
/// </summary>
public sealed class BoltClient : IAsyncDisposable
{
    private readonly Uri _serverUri;
    private readonly string _clientId;
    private readonly string _clientName;
    private readonly int _senderHash;
    private readonly BoltClientOptions _config;
    private readonly ILogger _logger;

    private readonly List<BoltConnection> _connections = [];
    private int _roundRobin;
    private volatile bool _isRegistered;
    private volatile bool _disposed;

    private readonly ConcurrentDictionary<Guid, PooledRpcCall> _pendingCalls = new();
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _handlers = new();
    private readonly ConcurrentDictionary<string, int> _hashCache = new();
    private readonly ConcurrentQueue<byte[]> _offlineQueue = new();
    private readonly ConcurrentDictionary<Guid, BoltStream> _activeStreams = new();
    private readonly ConcurrentDictionary<int, Func<BoltStream, Task>> _streamHandlers = new();
    private TimeSpan _rpcTimeout;

    // Large RPC: internal command hash for auto-streamed payloads
    private static readonly int LargeRpcCommandHash = BoltCodec.Fnv1aHash("__bolt_large_rpc__");

    // Frame handler extensibility — allows Bolt.Media to hook into the receive loop
    private readonly ConcurrentDictionary<byte, Action<BoltConnection, byte[], int>> _frameHandlers = new();

    /// <summary>
    /// Register a handler for a specific frame type. Used by Bolt.Media to handle media frames (0x20-0x26).
    /// </summary>
    public void RegisterFrameHandler(FrameType frameType, Action<BoltConnection, byte[], int> handler)
        => _frameHandlers[(byte)frameType] = handler;

    /// <summary>Get the current primary connection for sending frames.</summary>
    public BoltConnection GetPrimaryConnection() => GetConnection();

    public bool IsConnected => _connections.Count > 0 && _isRegistered;

    public BoltClient(Uri serverUri, string clientId, string clientName, BoltClientOptions config, ILogger logger)
    {
        _serverUri = serverUri;
        _clientId = clientId;
        _senderHash = BoltCodec.Fnv1aHash(clientId);
        _clientName = clientName;
        _config = config;
        _logger = logger;
        _rpcTimeout = TimeSpan.FromSeconds(config.RpcTimeoutSeconds > 0 ? config.RpcTimeoutSeconds : 30);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Auto-register the internal large RPC stream handler
        RegisterLargeRpcStreamHandler();

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

    /// <summary>
    /// Registers the internal handler that reassembles large RPC payloads sent via BoltStream.
    /// When a stream opens with the __bolt_large_rpc__ command, this handler:
    /// 1. Reads the metadata header (requestId, commandHash, totalSize)
    /// 2. Accumulates payload chunks
    /// 3. Calls the registered RPC handler with the full reassembled payload
    /// 4. Sends the Response back as a normal frame
    /// </summary>
    private void RegisterLargeRpcStreamHandler()
    {
        // Receiver side: reassemble large RPC payload from stream chunks
        RegisterStreamHandler("__bolt_large_rpc__", async (stream) =>
        {
            // Read metadata header: [16:requestId][4:commandHash][4:totalSize][4:senderHash]
            var (hasHeader, headerData) = await stream.ReadAsync();
            if (!hasHeader || headerData.Length < 28) return;

            var headerSpan = headerData.Span;
            var requestId = new Guid(headerSpan[..16]);
            var commandHash = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(headerSpan[16..]);
            var totalSize = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(headerSpan[20..]);
            var senderHash = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(headerSpan[24..]);

            if (totalSize < 0 || totalSize > 100 * 1024 * 1024) return; // 100MB safety limit

            // Reassemble payload chunks
            var buffer = new byte[totalSize];
            var bytesRead = 0;
            await foreach (var chunk in stream.ReadAllAsync())
            {
                var len = Math.Min(chunk.Length, totalSize - bytesRead);
                chunk[..len].CopyTo(buffer.AsMemory(bytesRead));
                bytesRead += len;
                if (bytesRead >= totalSize) break;
            }

            // Build response
            HttpStatusCode statusCode;
            ReadOnlyMemory<byte> responsePayload;

            if (_handlers.TryGetValue(commandHash, out var handler))
            {
                try
                {
                    (statusCode, responsePayload) = await handler(buffer, requestId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Large RPC handler error for commandHash {Hash}", commandHash);
                    statusCode = HttpStatusCode.InternalServerError;
                    responsePayload = ReadOnlyMemory<byte>.Empty;
                }
            }
            else
            {
                statusCode = HttpStatusCode.NotImplemented;
                responsePayload = ReadOnlyMemory<byte>.Empty;
            }

            // Response: small → single Push, large → stream back
            if (responsePayload.Length <= _config.LargePayloadThreshold)
            {
                // Small response: single Push frame
                var respData = new byte[18 + responsePayload.Length];
                requestId.TryWriteBytes(respData);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(respData.AsSpan(16), (short)statusCode);
                responsePayload.CopyTo(respData.AsMemory(18));

                var conn = GetConnection();
                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WritePush(writer, Guid.NewGuid(), senderHash, _senderHash, LargeRpcResponseHash, respData);
                await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
            }
            else
            {
                // Large response: stream it back via __bolt_large_rpc_response_stream__
                // Sender hash is used as recipientId for the reverse stream
                var respStream = new BoltStream(Guid.NewGuid(), GetConnection());
                _activeStreams[respStream.StreamId] = respStream;

                // StreamOpen to sender
                var openWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteStreamOpen(openWriter, respStream.StreamId, senderHash, LargeRpcResponseStreamHash);
                await GetConnection().SendAsync(openWriter.WrittenMemory, CancellationToken.None);

                // Header: [16:requestId][2:statusCode][4:totalSize]
                var respHeader = new byte[22];
                requestId.TryWriteBytes(respHeader);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(respHeader.AsSpan(16), (short)statusCode);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(respHeader.AsSpan(18), responsePayload.Length);
                await respStream.SendAsync((ReadOnlyMemory<byte>)respHeader);

                // Chunked payload
                var chunkSize = _config.StreamChunkSize;
                for (int offset = 0; offset < responsePayload.Length; offset += chunkSize)
                {
                    var len = Math.Min(chunkSize, responsePayload.Length - offset);
                    await respStream.SendAsync(responsePayload.Slice(offset, len));
                }
                await respStream.CloseAsync();
            }
        });

        // Sender side: resolve pending large RPC calls when SMALL response arrives via Push
        RegisterHandler("__bolt_large_rpc_response__", (payload, _) =>
        {
            if (payload.Length < 18) return Task.FromResult((HttpStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty));

            var span = payload.Span;
            var requestId = new Guid(span[..16]);
            var statusCode = (HttpStatusCode)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(span[16..]);
            var respPayload = payload.Length > 18 ? payload[18..].ToArray() : Array.Empty<byte>();

            if (_pendingCalls.TryRemove(requestId, out var rpcCall))
                rpcCall.SetResult(new BoltRpcResponse { StatusCode = statusCode, Data = respPayload });

            return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
        });

        // Sender side: resolve pending large RPC calls when LARGE response arrives via stream
        RegisterStreamHandler("__bolt_large_rpc_response_stream__", async (respStream) =>
        {
            var (hasHeader, headerData) = await respStream.ReadAsync();
            if (!hasHeader || headerData.Length < 22) return;

            var hdr = headerData.Span;
            var requestId = new Guid(hdr[..16]);
            var statusCode = (HttpStatusCode)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(hdr[16..]);
            var totalSize = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(hdr[18..]);

            if (totalSize < 0 || totalSize > 100 * 1024 * 1024) return;

            var buffer = new byte[totalSize];
            var bytesRead = 0;
            await foreach (var chunk in respStream.ReadAllAsync())
            {
                var len = Math.Min(chunk.Length, totalSize - bytesRead);
                chunk[..len].CopyTo(buffer.AsMemory(bytesRead));
                bytesRead += len;
                if (bytesRead >= totalSize) break;
            }

            if (_pendingCalls.TryRemove(requestId, out var rpcCall))
                rpcCall.SetResult(new BoltRpcResponse { StatusCode = statusCode, Data = buffer });
        });
    }

    private static readonly int LargeRpcResponseHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response__");
    private static readonly int LargeRpcResponseStreamHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response_stream__");

    private async Task<BoltConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(_serverUri, ct);

        var writer = new ArrayBufferWriter<byte>(128);
        BoltCodec.WriteRegister(writer, _clientId, _clientName);
        await ws.SendAsync(writer.WrittenMemory, WebSocketMessageType.Binary, true, ct);

        var ackBuffer = new byte[2];
        var result = await ws.ReceiveAsync(ackBuffer, ct);
        if (result.Count < 2 || (FrameType)ackBuffer[0] != FrameType.RegisterAck || ackBuffer[1] != 1)
            throw new InvalidOperationException("Server rejected registration");

        var conn = new BoltConnection(ws);
        var receiveCts = new CancellationTokenSource();
        conn.ReceiveCts = receiveCts;
        conn.ReceiveLoop = Task.Run(() => ReceiveLoopAsync(conn, receiveCts.Token));
        return conn;
    }

    private async Task ScaleUpAsync()
    {
        if (_connections.Count >= _config.MaxConnections) return;
        try
        {
            var conn = await CreateConnectionAsync(CancellationToken.None);
            _connections.Add(conn);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to scale up connection pool"); }
    }

    public async Task ConnectWithRetryAsync(CancellationToken ct = default)
    {
        const int maxRetries = 100;
        var baseDelay = TimeSpan.FromMilliseconds(500);
        var maxDelay = TimeSpan.FromSeconds(30);
        var random = new Random();

        for (int attempt = 0; attempt < maxRetries && !ct.IsCancellationRequested; attempt++)
        {
            try { await ConnectAsync(ct); return; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Connection attempt {Attempt} failed: {Error}", attempt + 1, ex.Message);
                var delay = TimeSpan.FromMilliseconds(Math.Min(baseDelay.TotalMilliseconds * Math.Pow(2, attempt), maxDelay.TotalMilliseconds));
                var jitter = TimeSpan.FromMilliseconds(random.Next(0, (int)(delay.TotalMilliseconds * 0.3)));
                await Task.Delay(delay + jitter, ct);
                foreach (var c in _connections) c.WebSocket.Dispose();
                _connections.Clear();
            }
        }
        throw new InvalidOperationException($"Failed to connect after {maxRetries} attempts");
    }

    // ── RPC ──────────────────────────────────────────────────

    public async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Data)> InvokeAsync(
        string recipientId, string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        // Auto-stream large payloads transparently
        if (payload.Length > _config.LargePayloadThreshold)
            return await InvokeLargeAsync(recipientId, commandName, payload, ct);

        var requestId = Guid.NewGuid();
        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = _hashCache.GetOrAdd(commandName, BoltCodec.Fnv1aHash);
        var rpcCall = PooledRpcCall.Rent();
        _pendingCalls[requestId] = rpcCall;

        try
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteRequest(writer, requestId, recipientHash, _senderHash, commandHash, payload.Span);

            if (!IsConnected)
            {
                _offlineQueue.Enqueue(writer.WrittenSpan.ToArray());
                rpcCall.SetException(new InvalidOperationException("Not connected"));
            }
            else
            {
                var conn = GetConnection();
                await conn.SendAsync(writer.WrittenMemory, ct);
                if (conn.PendingSends > _config.ScaleUpThreshold && _connections.Count < _config.MaxConnections)
                    _ = ScaleUpAsync();
            }

            using var timeoutCts = new CancellationTokenSource(_rpcTimeout);
            rpcCall.RegisterTimeout(timeoutCts.Token);
            var response = await rpcCall.GetTask();
            return (response.StatusCode, response.Data);
        }
        finally { _pendingCalls.TryRemove(requestId, out _); }
    }

    /// <summary>
    /// Transparently stream a large payload via BoltStream, then wait for the RPC response.
    /// The recipient reassembles the stream and processes it as a normal RPC call.
    /// </summary>
    private async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Data)> InvokeLargeAsync(
        string recipientId, string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected");

        var requestId = Guid.NewGuid();
        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = _hashCache.GetOrAdd(commandName, BoltCodec.Fnv1aHash);

        // Register pending RPC — response comes back as normal Response frame
        var rpcCall = PooledRpcCall.Rent();
        _pendingCalls[requestId] = rpcCall;

        try
        {
            // Open stream with special large-RPC command hash
            var stream = await OpenStreamAsync(recipientId, "__bolt_large_rpc__", ct);

            // First chunk: metadata header [16:requestId][4:commandHash][4:totalSize][4:senderHash]
            var header = new byte[28];
            requestId.TryWriteBytes(header);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), commandHash);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), payload.Length);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), _hashCache.GetOrAdd(_clientId, BoltCodec.Fnv1aHash));
            await stream.SendAsync((ReadOnlyMemory<byte>)header, ct);

            // Send payload in chunks
            var chunkSize = _config.StreamChunkSize;
            for (int offset = 0; offset < payload.Length; offset += chunkSize)
            {
                var len = Math.Min(chunkSize, payload.Length - offset);
                await stream.SendAsync(payload.Slice(offset, len), ct);
            }

            // Close stream — signals "all data sent"
            await stream.CloseAsync(ct: ct);

            // Response arrives as a Request with __bolt_large_rpc_response__ command
            // (handled by RegisterLargeRpcResponseHandler, which resolves our pending call)
            using var timeoutCts = new CancellationTokenSource(_rpcTimeout);
            rpcCall.RegisterTimeout(timeoutCts.Token);
            var response = await rpcCall.GetTask();
            return (response.StatusCode, response.Data);
        }
        finally { _pendingCalls.TryRemove(requestId, out _); }
    }

    public async Task<TResponse?> SendAsync<TRequest, TResponse>(string recipientId, string commandName, TRequest request, CancellationToken ct = default)
    {
        var payload = MemoryPackSerializer.Serialize(request);
        var result = await InvokeAsync(recipientId, commandName, payload, ct);
        return result.Data.Length > 0 ? MemoryPackSerializer.Deserialize<TResponse>(result.Data.Span) : default;
    }

    public async Task<HttpStatusCode> SendAsync<TRequest>(string recipientId, string commandName, TRequest request, CancellationToken ct = default)
    {
        var payload = MemoryPackSerializer.Serialize(request);
        var result = await InvokeAsync(recipientId, commandName, payload, ct);
        return result.StatusCode;
    }

    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = BoltCodec.Fnv1aHash(commandName);
        _handlers[hash] = handler;
    }

    /// <summary>
    /// Register a handler with CancellationToken that is cancelled when the connection drops.
    /// </summary>
    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = BoltCodec.Fnv1aHash(commandName);
        _handlers[hash] = (payload, requestId) =>
        {
            // Use a token linked to the client's connection state
            var cts = new CancellationTokenSource(_rpcTimeout);
            return handler(payload, requestId, cts.Token);
        };
    }

    /// <summary>
    /// Send a fire-and-forget push message (no response expected).
    /// Use for typing indicators, presence updates, read receipts.
    /// recipientId of "" with hash 0 broadcasts to all connected clients.
    /// </summary>
    public async ValueTask PushAsync(string recipientId, string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        if (!IsConnected) return;

        var recipientHash = string.IsNullOrEmpty(recipientId) ? 0 : _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = _hashCache.GetOrAdd(commandName, BoltCodec.Fnv1aHash);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WritePush(writer, Guid.NewGuid(), recipientHash, _senderHash, commandHash, payload.Span);
        await GetConnection().SendAsync(writer.WrittenMemory, ct);
    }

    /// <summary>Typed push with MemoryPack serialization.</summary>
    public async ValueTask PushAsync<T>(string recipientId, string commandName, T data, CancellationToken ct = default)
    {
        var payload = MemoryPackSerializer.Serialize(data);
        await PushAsync(recipientId, commandName, (ReadOnlyMemory<byte>)payload, ct);
    }

    // ── Streaming ────────────────────────────────────────────

    public void RegisterStreamHandler(string commandName, Func<BoltStream, Task> handler)
    {
        var hash = BoltCodec.Fnv1aHash(commandName);
        _streamHandlers[hash] = handler;
    }

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

    public async Task StreamAsync<T>(string recipientId, string commandName, IAsyncEnumerable<T> items, CancellationToken ct = default)
    {
        await using var stream = await OpenStreamAsync(recipientId, commandName, ct);
        await stream.SendAllAsync(items, ct);
    }

    public void RegisterStreamHandler<T>(string commandName, Func<IAsyncEnumerable<T>, BoltStream, Task> handler)
    {
        RegisterStreamHandler(commandName, async (stream) =>
        {
            var items = stream.ReadAllAsync<T>();
            await handler(items, stream);
        });
    }

    // ── Receive loop ─────────────────────────────────────────

    private BoltConnection GetConnection()
    {
        var count = _connections.Count;
        if (count == 1) return _connections[0];
        var idx = (uint)Interlocked.Increment(ref _roundRobin) % count;
        return _connections[(int)idx];
    }

    private async Task ReceiveLoopAsync(BoltConnection conn, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
        var messageBuffer = new List<byte>(); // For multi-frame WebSocket messages
        try
        {
            while (!ct.IsCancellationRequested && conn.WebSocket.State == WebSocketState.Open)
            {
                var result = await conn.WebSocket.ReceiveAsync(buffer.AsMemory(), ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType != WebSocketMessageType.Binary || result.Count == 0) continue;

                // Handle multi-frame WebSocket messages (large payloads)
                byte[] frameBytes;
                if (!result.EndOfMessage)
                {
                    messageBuffer.Clear();
                    messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                    while (!result.EndOfMessage)
                    {
                        result = await conn.WebSocket.ReceiveAsync(buffer.AsMemory(), ct);
                        messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                    }
                    frameBytes = messageBuffer.ToArray();
                }
                else
                {
                    frameBytes = buffer;
                }

                var totalLength = result.EndOfMessage && messageBuffer.Count == 0 ? result.Count : frameBytes.Length;
                var data = frameBytes.AsSpan(0, totalLength);
                var frameType = BoltCodec.PeekFrameType(data);

                switch (frameType)
                {
                    case FrameType.Response:
                        HandleIncomingResponse(data);
                        break;
                    case FrameType.Request:
                        var reqData = data.ToArray();
                        _ = HandleIncomingRequestAsync(conn, reqData, reqData.Length, ct);
                        break;
                    case FrameType.Push:
                        var pushData = data.ToArray();
                        _ = HandleIncomingPushAsync(pushData, pushData.Length);
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
                    default:
                        // Extensible dispatch: Bolt.Media registers handlers for 0x20-0x26
                        if (_frameHandlers.TryGetValue((byte)frameType, out var handler))
                        {
                            var handlerData = data.ToArray();
                            handler(conn, handlerData, result.Count);
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex) { _logger.LogWarning("WebSocket receive error: {Error}", ex.Message); }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (!_disposed)
            {
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
        if (!BoltCodec.TryReadResponse(data, out var frame, out _)) return;
        if (_pendingCalls.TryRemove(frame.RequestId, out var rpcCall))
        {
            var payload = frame.PayloadLength > 0 ? frame.GetPayload(data).ToArray() : Array.Empty<byte>();
            rpcCall.SetResult(new BoltRpcResponse { StatusCode = frame.StatusCode, Data = payload });
        }
    }

    private void HandleStreamOpen(BoltConnection conn, ReadOnlySpan<byte> data, CancellationToken ct)
    {
        if (!BoltCodec.TryReadStreamOpen(data, out var streamId, out _, out var commandHash)) return;
        var stream = new BoltStream(streamId, conn);
        _activeStreams[streamId] = stream;
        if (_streamHandlers.TryGetValue(commandHash, out var handler))
        {
            _ = Task.Run(async () =>
            {
                try { await handler(stream); }
                catch (Exception ex) { _logger.LogError(ex, "Stream handler error"); }
                finally { _activeStreams.TryRemove(streamId, out _); }
            }, ct);
        }
    }

    private void HandleStreamData(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamData(data, out var streamId, out var payloadOffset, out var payloadLength, out _)) return;
        if (_activeStreams.TryGetValue(streamId, out var stream))
            stream.EnqueueInbound(data.Slice(payloadOffset, payloadLength).ToArray());
    }

    private void HandleStreamClose(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamClose(data, out var streamId, out var statusCode)) return;
        if (_activeStreams.TryRemove(streamId, out var stream))
            stream.MarkClosed(statusCode);
    }

    private async Task HandleIncomingPushAsync(byte[] data, int length)
    {
        // Push uses same frame layout as Request — parse with TryReadRequest
        var span = data.AsSpan(0, length);
        if (!BoltCodec.TryReadRequest(span, out var frame, out _)) return;

        if (_handlers.TryGetValue(frame.CommandHash, out var handler))
        {
            try
            {
                var payload = frame.GetPayload(data.AsMemory(0, length));
                await handler(payload, frame.RequestId);
                // No response sent for Push (fire-and-forget)
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push handler error for command hash {CommandHash}", frame.CommandHash);
            }
        }
    }

    private async Task HandleIncomingRequestAsync(BoltConnection conn, byte[] data, int length, CancellationToken ct)
    {
        var span = data.AsSpan(0, length);
        if (!BoltCodec.TryReadRequest(span, out var frame, out _)) return;

        if (_handlers.TryGetValue(frame.CommandHash, out var handler))
        {
            try
            {
                var payload = frame.GetPayload(data.AsMemory(0, length));
                var (statusCode, responsePayload) = await handler(payload, frame.RequestId);
                await SendResponseAsync(conn, frame.RequestId, frame.SenderHash, statusCode, responsePayload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler error for command hash {CommandHash}", frame.CommandHash);
                await SendResponseAsync(conn, frame.RequestId, frame.SenderHash, HttpStatusCode.InternalServerError, ReadOnlyMemory<byte>.Empty, ct);
            }
        }
        else
        {
            await SendResponseAsync(conn, frame.RequestId, frame.SenderHash, HttpStatusCode.NotImplemented, ReadOnlyMemory<byte>.Empty, ct);
        }
    }

    /// <summary>
    /// Send an RPC response. Small → single Response frame (hub routes by requestId).
    /// Large → BoltStream chunking back to caller (using senderHash from Request frame).
    /// Fully symmetric with the request path.
    /// </summary>
    private async Task SendResponseAsync(BoltConnection conn, Guid requestId, int callerSenderHash,
        HttpStatusCode statusCode, ReadOnlyMemory<byte> responsePayload, CancellationToken ct)
    {
        if (responsePayload.Length <= _config.LargePayloadThreshold)
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(writer, requestId, statusCode, responsePayload.Span);
            await conn.SendAsync(writer.WrittenMemory, ct);
        }
        else
        {
            // Large response: BoltStream back to caller — same mechanism as request path
            var respStream = new BoltStream(Guid.NewGuid(), conn);
            _activeStreams[respStream.StreamId] = respStream;

            var openWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteStreamOpen(openWriter, respStream.StreamId, callerSenderHash, LargeRpcResponseStreamHash);
            await conn.SendAsync(openWriter.WrittenMemory, ct);

            // Header: [16:requestId][2:statusCode][4:totalSize]
            var header = new byte[22];
            requestId.TryWriteBytes(header);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(16), (short)statusCode);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18), responsePayload.Length);
            await respStream.SendAsync((ReadOnlyMemory<byte>)header, ct);

            var chunkSize = _config.StreamChunkSize;
            for (int offset = 0; offset < responsePayload.Length; offset += chunkSize)
            {
                var len = Math.Min(chunkSize, responsePayload.Length - offset);
                await respStream.SendAsync(responsePayload.Slice(offset, len), ct);
            }
            await respStream.CloseAsync(ct: ct);
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
        if (drained > 0) _logger.LogInformation("Drained {Count} offline messages", drained);
    }

    private async Task ReconnectAsync()
    {
        _logger.LogInformation("Attempting reconnection...");
        try { await ConnectWithRetryAsync(CancellationToken.None); }
        catch (Exception ex) { _logger.LogError(ex, "Reconnection failed"); }
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
            if (WebSocket.State == WebSocketState.Open)
                await WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
        }
        finally { _sendLock.Release(); Interlocked.Decrement(ref _pendingSends); }
    }
}

/// <summary>Response data from an RPC call.</summary>
public struct BoltRpcResponse
{
    public HttpStatusCode StatusCode;
    public ReadOnlyMemory<byte> Data;
}
