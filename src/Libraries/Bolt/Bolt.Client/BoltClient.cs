using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Bolt.Client.Transport;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Bolt.Protocol.Transport;
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
    private readonly BoltTransportNegotiator _negotiator;

    private readonly List<BoltConnection> _connections = [];
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
        _negotiator = new BoltTransportNegotiator(logger);
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

            // Reassemble payload chunks into pooled buffer
            var buffer = ArrayPool<byte>.Shared.Rent(totalSize);
            try
            {
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
                    (statusCode, responsePayload) = await handler(buffer.AsMemory(0, totalSize), requestId);
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
                // Small response: single Push frame — pool the response data buffer
                var respLen = 18 + responsePayload.Length;
                var respBuf = ArrayPool<byte>.Shared.Rent(respLen);
                try
                {
                    requestId.TryWriteBytes(respBuf);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(respBuf.AsSpan(16), (short)statusCode);
                    responsePayload.CopyTo(respBuf.AsMemory(18));

                    var conn = GetConnection();
                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WritePush(writer, Guid.NewGuid(), senderHash, _senderHash, LargeRpcResponseHash, respBuf.AsSpan(0, respLen));
                    await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
                }
                finally { ArrayPool<byte>.Shared.Return(respBuf); }
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
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        });

        // Sender side: resolve pending large RPC calls when SMALL response arrives via Push
        RegisterHandler("__bolt_large_rpc_response__", (payload, _) =>
        {
            if (payload.Length < 18) return Task.FromResult((HttpStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty));

            var span = payload.Span;
            var requestId = new Guid(span[..16]);
            var statusCode = (HttpStatusCode)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(span[16..]);
            ReadOnlyMemory<byte> respPayload;
            if (payload.Length > 18)
            {
                var owner = new PooledMemoryOwner(payload.Length - 18);
                payload.Span[18..].CopyTo(owner.WritableSpan);
                respPayload = owner.Memory;
            }
            else
            {
                respPayload = ReadOnlyMemory<byte>.Empty;
            }

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

            var owner = new PooledMemoryOwner(totalSize);
            var bytesRead = 0;
            await foreach (var chunk in respStream.ReadAllAsync())
            {
                var len = Math.Min(chunk.Length, totalSize - bytesRead);
                chunk.Span[..len].CopyTo(owner.WritableSpan.Slice(bytesRead));
                bytesRead += len;
                if (bytesRead >= totalSize) break;
            }

            if (_pendingCalls.TryRemove(requestId, out var rpcCall))
                rpcCall.SetResult(new BoltRpcResponse { StatusCode = statusCode, Data = owner.Memory });
        });
    }

    private static readonly int LargeRpcResponseHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response__");
    private static readonly int LargeRpcResponseStreamHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response_stream__");

    private async Task<BoltConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var transport = await _negotiator.ConnectAsync(_serverUri, _config, ct);
        var conn = new BoltConnection(transport);

        // Send registration frame (same for all transports)
        var regWriter = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteRegister(regWriter, _clientId, _clientName);
        await transport.SendAsync(regWriter.WrittenMemory, ct);

        // Read registration ack
        var ackBuffer = ArrayPool<byte>.Shared.Rent(2);
        var (ackBytes, _) = await transport.ReceiveAsync(ackBuffer, ct);
        var ackValid = ackBytes >= 2 && (FrameType)ackBuffer[0] == FrameType.RegisterAck && ackBuffer[1] == 1;
        ArrayPool<byte>.Shared.Return(ackBuffer);
        if (!ackValid)
            throw new InvalidOperationException("Server rejected registration");

        var receiveCts = new CancellationTokenSource();
        conn.ReceiveCts = receiveCts;
        conn.StartSendLoop(receiveCts.Token);
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
                foreach (var c in _connections) { c.CompleteSendChannel(); c.ReceiveCts?.Cancel(); try { await c.Transport.DisposeAsync(); } catch { } }
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

                // Proactive scale-up: if the least-loaded connection is still saturated, open a new one before sending
                if (conn.PendingSends > _config.ScaleUpThreshold && _connections.Count < _config.MaxConnections)
                {
                    _ = ScaleUpAsync(); // Start opening new connection in background
                }

                await conn.SendAsync(writer.WrittenMemory, ct);
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
        var serWriter = new RentedBufferWriter(256);
        try
        {
            MemoryPackSerializer.Serialize(serWriter, request);
            var result = await InvokeAsync(recipientId, commandName, serWriter.WrittenMemory, ct);
            return result.Data.Length > 0 ? MemoryPackSerializer.Deserialize<TResponse>(result.Data.Span) : default;
        }
        finally { serWriter.Dispose(); }
    }

    public async Task<HttpStatusCode> SendAsync<TRequest>(string recipientId, string commandName, TRequest request, CancellationToken ct = default)
    {
        var serWriter = new RentedBufferWriter(256);
        try
        {
            MemoryPackSerializer.Serialize(serWriter, request);
            var result = await InvokeAsync(recipientId, commandName, serWriter.WrittenMemory, ct);
            return result.StatusCode;
        }
        finally { serWriter.Dispose(); }
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
        var serWriter = new RentedBufferWriter(256);
        try
        {
            MemoryPackSerializer.Serialize(serWriter, data);
            await PushAsync(recipientId, commandName, serWriter.WrittenMemory, ct);
        }
        finally { serWriter.Dispose(); }
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

        // Pick the connection with the fewest pending sends
        var best = _connections[0];
        var bestPending = best.PendingSends;
        for (int i = 1; i < count; i++)
        {
            var pending = _connections[i].PendingSends;
            if (pending < bestPending)
            {
                best = _connections[i];
                bestPending = pending;
            }
        }
        return best;
    }

    private async Task ReceiveLoopAsync(BoltConnection conn, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
        byte[]? largeBuffer = null; // Rented from pool for multi-frame messages
        try
        {
            while (!ct.IsCancellationRequested && conn.Transport.IsConnected)
            {
                var (bytesRead, endOfMessage) = await conn.Transport.ReceiveAsync(buffer.AsMemory(), ct);
                if (bytesRead == 0 && endOfMessage) break; // Connection closed
                if (bytesRead == 0) continue;

                // Handle multi-frame messages (large payloads)
                byte[] frameBytes;
                int totalLength;
                if (!endOfMessage)
                {
                    // Multi-frame: accumulate into a growing pooled buffer
                    var assembled = bytesRead;
                    var capacity = Math.Max(bytesRead * 4, 512 * 1024);
                    if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
                    largeBuffer = ArrayPool<byte>.Shared.Rent(capacity);
                    buffer.AsSpan(0, bytesRead).CopyTo(largeBuffer);

                    while (!endOfMessage)
                    {
                        (bytesRead, endOfMessage) = await conn.Transport.ReceiveAsync(buffer.AsMemory(), ct);
                        if (bytesRead == 0 && endOfMessage) break;
                        // Grow if needed
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
                    frameBytes = largeBuffer;
                    totalLength = assembled;
                }
                else
                {
                    frameBytes = buffer;
                    totalLength = bytesRead;
                }

                var data = frameBytes.AsSpan(0, totalLength);
                var frameType = BoltCodec.PeekFrameType(data);

                switch (frameType)
                {
                    case FrameType.Response:
                        HandleIncomingResponse(data);
                        break;
                    case FrameType.Request:
                    {
                        var reqBuf = ArrayPool<byte>.Shared.Rent(totalLength);
                        data.CopyTo(reqBuf);
                        _ = DispatchRequestPooledAsync(conn, reqBuf, totalLength, ct);
                        break;
                    }
                    case FrameType.Push:
                    {
                        var pushBuf = ArrayPool<byte>.Shared.Rent(totalLength);
                        data.CopyTo(pushBuf);
                        _ = DispatchPushPooledAsync(pushBuf, totalLength);
                        break;
                    }
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
                            var handlerBuf = ArrayPool<byte>.Shared.Rent(totalLength);
                            data.CopyTo(handlerBuf);
                            handler(conn, handlerBuf, totalLength);
                            // Note: frame handlers own the buffer lifetime (Bolt.Media returns it)
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogWarning("Bolt {Transport} receive error: {Error}", conn.TransportType, ex.Message); }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
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

    private async Task DispatchRequestPooledAsync(BoltConnection conn, byte[] pooledBuf, int length, CancellationToken ct)
    {
        try { await HandleIncomingRequestAsync(conn, pooledBuf, length, ct); }
        finally { ArrayPool<byte>.Shared.Return(pooledBuf); }
    }

    private async Task DispatchPushPooledAsync(byte[] pooledBuf, int length)
    {
        try { await HandleIncomingPushAsync(pooledBuf, length); }
        finally { ArrayPool<byte>.Shared.Return(pooledBuf); }
    }

    private void HandleIncomingResponse(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadResponse(data, out var frame, out _)) return;
        if (_pendingCalls.TryRemove(frame.RequestId, out var rpcCall))
        {
            // Copy payload into a PooledMemoryOwner — backed by ArrayPool, auto-returned via GC.
            // Cost: ~32B object header (vs 512KB+ LOH alloc before).
            ReadOnlyMemory<byte> payload;
            if (frame.PayloadLength > 0)
            {
                var owner = new PooledMemoryOwner(frame.PayloadLength);
                frame.GetPayload(data).CopyTo(owner.WritableSpan);
                payload = owner.Memory;
            }
            else
            {
                payload = ReadOnlyMemory<byte>.Empty;
            }
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
        {
            var owner = new PooledMemoryOwner(payloadLength);
            data.Slice(payloadOffset, payloadLength).CopyTo(owner.WritableSpan);
            stream.EnqueueInbound(owner.Memory);
        }
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
            conn.CompleteSendChannel();
            conn.ReceiveCts?.Cancel();
            if (conn.SendLoop is not null)
                try { await conn.SendLoop; } catch { }
            if (conn.ReceiveLoop is not null)
                try { await conn.ReceiveLoop; } catch { }
            try { await conn.Transport.CloseAsync(); } catch { }
            try { await conn.Transport.DisposeAsync(); } catch { }
            conn.ReceiveCts?.Dispose();
        }
        _connections.Clear();
    }
}

/// <summary>
/// A single transport connection in the Bolt client pool.
/// Uses a Channel-based send queue with a dedicated background send loop
/// so callers never block — writes go into the channel instantly.
/// The single-reader send loop drains the channel and writes to the transport
/// one at a time, eliminating lock contention for all transports (WebSocket, QUIC, WebTransport).
/// </summary>
public sealed class BoltConnection
{
    public IBoltConnection Transport { get; }
    public BoltTransport TransportType { get; }
    private readonly Channel<(byte[] Buffer, int Length, CancellationToken Ct)> _sendChannel;
    private int _pendingSends;

    /// <summary>
    /// Backward-compatible WebSocket accessor for Bolt.Media P2P code.
    /// Returns the underlying ClientWebSocket when transport is WebSocket, null otherwise.
    /// Will be removed when Bolt.Media migrates to IBoltConnection (Task 7/8).
    /// </summary>
    [Obsolete("Use Transport property instead. This exists for Bolt.Media backward compatibility.")]
    public System.Net.WebSockets.ClientWebSocket WebSocket =>
        Transport is WebSocketBoltConnection wsConn
            ? GetUnderlyingWebSocket(wsConn)
            : throw new InvalidOperationException($"WebSocket property not available on {TransportType} transport. Use Transport instead.");

    [Obsolete("Temporary helper for WebSocket backward compat")]
    private static System.Net.WebSockets.ClientWebSocket GetUnderlyingWebSocket(WebSocketBoltConnection wsConn)
    {
        // Access the underlying WebSocket field via the WebSocket property on WebSocketBoltConnection
        // WebSocketBoltConnection wraps a WebSocket (base class), but DirectConnectionManager needs ClientWebSocket
        // For P2P, the WebSocket is always a ClientWebSocket
        var ws = wsConn.UnderlyingWebSocket;
        return ws as System.Net.WebSockets.ClientWebSocket
            ?? throw new InvalidOperationException("Underlying WebSocket is not a ClientWebSocket");
    }

    public CancellationTokenSource? ReceiveCts { get; set; }
    public Task? ReceiveLoop { get; set; }
    public Task? SendLoop { get; set; }
    public int PendingSends => _pendingSends;

    public BoltConnection(IBoltConnection transport)
    {
        Transport = transport;
        TransportType = transport.TransportType;
        _sendChannel = Channel.CreateBounded<(byte[], int, CancellationToken)>(
            new BoundedChannelOptions(4096)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    /// <summary>
    /// Backward-compatible constructor for Bolt.Media P2P (DirectConnectionManager).
    /// Wraps a raw ClientWebSocket in a WebSocketBoltConnection.
    /// Will be removed when Bolt.Media migrates to IBoltConnection (Task 7/8).
    /// </summary>
    [Obsolete("Use BoltConnection(IBoltConnection) constructor instead.")]
    public BoltConnection(System.Net.WebSockets.ClientWebSocket webSocket)
        : this(new WebSocketBoltConnection(webSocket))
    {
    }

    /// <summary>Start the background send loop. Call once after construction.</summary>
    public void StartSendLoop(CancellationToken ct)
    {
        SendLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var (buf, len, sendCt) in _sendChannel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        if (Transport.IsConnected)
                            await Transport.SendAsync(buf.AsMemory(0, len), sendCt);
                    }
                    catch (OperationCanceledException) { }
                    catch { /* Transport error — receive loop will detect disconnect */ }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buf);
                        Interlocked.Decrement(ref _pendingSends);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, ct);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        // Snapshot into a pooled buffer — the caller's buffer (often a thread-local
        // RentedBufferWriter) may be reused before the send loop processes this item.
        // The pooled buffer is returned to ArrayPool after the transport write completes.
        var len = data.Length;
        var buf = ArrayPool<byte>.Shared.Rent(len);
        data.Span.CopyTo(buf);
        Interlocked.Increment(ref _pendingSends);
        if (_sendChannel.Writer.TryWrite((buf, len, ct)))
            return ValueTask.CompletedTask;
        return SendSlowAsync(buf, len, ct);
    }

    private async ValueTask SendSlowAsync(byte[] buf, int len, CancellationToken ct)
    {
        await _sendChannel.Writer.WriteAsync((buf, len, ct), ct);
    }

    /// <summary>Signal that no more sends will be enqueued. The send loop will drain and exit.</summary>
    public void CompleteSendChannel() => _sendChannel.Writer.TryComplete();
}

/// <summary>Response data from an RPC call.</summary>
public struct BoltRpcResponse
{
    public HttpStatusCode StatusCode;
    public ReadOnlyMemory<byte> Data;
}
