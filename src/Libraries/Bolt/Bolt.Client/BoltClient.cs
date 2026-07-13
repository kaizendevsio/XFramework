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
    private readonly int _maxFrameBytes;

    private readonly List<BoltConnection> _connections = [];
    private readonly object _connectionsLock = new();
    private volatile bool _isRegistered;
    private volatile bool _disposed;
    private long _totalSendFailures;
    private long _totalSendTimeouts;
    private long _totalReceiveLoopFaults;
    private long _totalUnexpectedDisconnects;
    private long _totalSuccessfulReconnects;

    private readonly ConcurrentDictionary<Guid, PooledRpcCall> _pendingCalls = new();
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _handlers = new();
    private readonly ConcurrentDictionary<string, int> _hashCache = new();
    private readonly ConcurrentDictionary<int, string> _commandNamesByHash = new();
    private readonly ConcurrentDictionary<Guid, BoltStream> _activeStreams = new();
    private readonly ConcurrentDictionary<int, Func<BoltStream, Task>> _streamHandlers = new();
    private TimeSpan _rpcTimeout;

    public event Action? Reconnecting;
    public event Action? Reconnected;
    public event Action? Disconnected;

    // Large RPC: internal command hash for auto-streamed payloads
    private static readonly int LargeRpcCommandHash = BoltCodec.Fnv1aHash("__bolt_large_rpc__");

    // Frame handler extensibility — allows Bolt.Media to hook into the receive loop
    private readonly ConcurrentDictionary<byte, Action<BoltConnection, byte[], int>> _frameHandlers = new();

    // Pub/sub state
    private readonly ConcurrentDictionary<int, TransientSubscription> _transientSubscriptions = new();
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), DurableSubscription> _durableSubscriptions = new();

    /// <summary>
    /// Register a handler for a specific frame type. Used by Bolt.Media to handle media frames (0x20-0x26).
    /// </summary>
    public void RegisterFrameHandler(FrameType frameType, Action<BoltConnection, byte[], int> handler)
        => _frameHandlers[(byte)frameType] = handler;

    /// <summary>Get the current primary connection for sending frames.</summary>
    public BoltConnection GetPrimaryConnection() => GetConnection();

    public bool IsConnected
    {
        get
        {
            lock (_connectionsLock)
                return _connections.Count > 0 && _isRegistered;
        }
    }

    public BoltClientHealthSnapshot GetHealthSnapshot()
    {
        BoltConnection[] connections;
        lock (_connectionsLock)
            connections = _connections.ToArray();

        var connectedTransports = 0;
        var pendingSends = 0;
        var activeSends = 0;
        var maxActiveSendElapsedMs = 0L;
        var runningSendLoops = 0;
        var runningReceiveLoops = 0;
        var faultedSendLoops = 0;
        var faultedReceiveLoops = 0;
        var pendingSendsUnhealthyThreshold = Math.Max(1, _config.ScaleUpThreshold * Math.Max(1, connections.Length));
        var activeSendUnhealthyThresholdMs = _config.SendEnqueueTimeoutMs > 0
            ? _config.SendEnqueueTimeoutMs
            : (int)Math.Min(int.MaxValue, _rpcTimeout.TotalMilliseconds);

        foreach (var connection in connections)
        {
            if (connection.Transport.IsConnected)
                connectedTransports++;

            pendingSends += connection.PendingSends;
            activeSends += connection.ActiveSends;
            maxActiveSendElapsedMs = Math.Max(maxActiveSendElapsedMs, connection.ActiveSendElapsedMs);

            if (connection.SendLoop is { IsFaulted: true })
                faultedSendLoops++;
            else if (connection.SendLoop is { IsCompleted: false })
                runningSendLoops++;

            if (connection.ReceiveLoop is { IsFaulted: true })
                faultedReceiveLoops++;
            else if (connection.ReceiveLoop is { IsCompleted: false })
                runningReceiveLoops++;
        }

        return new BoltClientHealthSnapshot(
            IsConnected,
            connections.Length,
            connectedTransports,
            pendingSends,
            activeSends,
            maxActiveSendElapsedMs,
            runningSendLoops,
            runningReceiveLoops,
            faultedSendLoops,
            faultedReceiveLoops,
            pendingSendsUnhealthyThreshold,
            activeSendUnhealthyThresholdMs,
            Interlocked.Read(ref _totalSendFailures),
            Interlocked.Read(ref _totalSendTimeouts),
            Interlocked.Read(ref _totalReceiveLoopFaults),
            Interlocked.Read(ref _totalUnexpectedDisconnects),
            Interlocked.Read(ref _totalSuccessfulReconnects));
    }

    public BoltClient(Uri serverUri, string clientId, string clientName, BoltClientOptions config, ILogger logger)
    {
        _serverUri = serverUri;
        _clientId = clientId;
        _senderHash = BoltCodec.Fnv1aHash(clientId);
        _clientName = clientName;
        _config = config;
        _logger = logger;
        _rpcTimeout = TimeSpan.FromSeconds(config.RpcTimeoutSeconds > 0 ? config.RpcTimeoutSeconds : 30);
        _maxFrameBytes = Math.Max(1024, config.MaxFrameBytes);
        _negotiator = new BoltTransportNegotiator(logger);

        // Wire pub/sub Event frame dispatch
        RegisterFrameHandler(FrameType.Event, HandleEventFrame);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Auto-register the internal large RPC stream handler
        RegisterLargeRpcStreamHandler();

        var minConns = Math.Max(1, _config.MinConnections);
        for (int i = 0; i < minConns; i++)
        {
            var conn = await CreateConnectionAsync(ct);
            lock (_connectionsLock)
                _connections.Add(conn);
        }
        _isRegistered = true;
        _logger.LogInformation("Bolt client connected: {ClientId} ({ClientName}), {Count} connection(s)",
            _clientId, _clientName, ConnectionCount);
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

            async Task SendLargeRpcErrorAsync(HttpStatusCode statusCode)
            {
                var respBuf = ArrayPool<byte>.Shared.Rent(18);
                try
                {
                    requestId.TryWriteBytes(respBuf);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(respBuf.AsSpan(16), (short)statusCode);

                    var conn = GetConnection();
                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WritePush(writer, Guid.NewGuid(), senderHash, _senderHash, LargeRpcResponseHash, respBuf.AsSpan(0, 18));
                    await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(respBuf);
                }
            }

            if (totalSize < 0 || totalSize > _maxFrameBytes)
            {
                await SendLargeRpcErrorAsync(HttpStatusCode.RequestEntityTooLarge);
                return;
            }

            // Reassemble payload chunks into pooled buffer
            var buffer = ArrayPool<byte>.Shared.Rent(totalSize);
            try
            {
            var bytesRead = 0;
            await foreach (var chunk in stream.ReadAllAsync())
            {
                if (chunk.Length > totalSize - bytesRead)
                {
                    await SendLargeRpcErrorAsync(HttpStatusCode.BadRequest);
                    return;
                }

                chunk.CopyTo(buffer.AsMemory(bytesRead));
                bytesRead += chunk.Length;
                if (bytesRead >= totalSize) break;
            }

            if (bytesRead != totalSize)
            {
                await SendLargeRpcErrorAsync(HttpStatusCode.BadRequest);
                return;
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
                var respStream = new BoltStream(Guid.NewGuid(), GetConnection(), RemoveActiveStream);
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

            if (totalSize < 0 || totalSize > _maxFrameBytes) return;

            var owner = new PooledMemoryOwner(totalSize);
            var bytesRead = 0;
            await foreach (var chunk in respStream.ReadAllAsync())
            {
                if (chunk.Length > totalSize - bytesRead)
                {
                    ((IDisposable)owner).Dispose();
                    if (_pendingCalls.TryRemove(requestId, out var rpcCall))
                        rpcCall.SetException(new InvalidOperationException("Large RPC response exceeded declared size"));
                    return;
                }

                chunk.Span.CopyTo(owner.WritableSpan.Slice(bytesRead));
                bytesRead += chunk.Length;
                if (bytesRead >= totalSize) break;
            }

            if (bytesRead != totalSize)
            {
                ((IDisposable)owner).Dispose();
                if (_pendingCalls.TryRemove(requestId, out var rpcCall))
                    rpcCall.SetException(new InvalidOperationException("Large RPC response ended before declared size"));
                return;
            }

            if (_pendingCalls.TryRemove(requestId, out var completedCall))
                completedCall.SetResult(new BoltRpcResponse { StatusCode = statusCode, Data = owner.Memory });
            else
                ((IDisposable)owner).Dispose();
        });
    }

    private static readonly int LargeRpcResponseHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response__");
    private static readonly int LargeRpcResponseStreamHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response_stream__");

    private async Task<BoltConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var transport = await _negotiator.ConnectAsync(_serverUri, _config, ct);
        using var registrationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        registrationCts.CancelAfter(Math.Max(1, _config.TransportAttemptTimeoutMs));

        try
        {
            var sendEnqueueTimeoutMs = _config.SendEnqueueTimeoutMs > 0
                ? _config.SendEnqueueTimeoutMs
                : (int)Math.Min(int.MaxValue, _rpcTimeout.TotalMilliseconds);
            var conn = new BoltConnection(transport, _config.SendQueueCapacity, sendEnqueueTimeoutMs);
            ObserveConnection(conn);

            // Registration send and ACK share one transport-attempt deadline.
            var regWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteRegister(regWriter, _clientId, _clientName);
            await transport.SendAsync(regWriter.WrittenMemory, registrationCts.Token);

            var ackBuffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                var (ackBytes, _) = await transport.ReceiveAsync(ackBuffer, registrationCts.Token);
                var ackValid = ackBytes >= 2 &&
                               (FrameType)ackBuffer[0] == FrameType.RegisterAck &&
                               ackBuffer[1] == 1;
                if (!ackValid)
                    throw new InvalidOperationException("Server rejected registration");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(ackBuffer);
            }

            var receiveCts = new CancellationTokenSource();
            conn.ReceiveCts = receiveCts;
            conn.StartSendLoop(receiveCts.Token);
            conn.ReceiveLoop = Task.Run(() => ReceiveLoopAsync(conn, receiveCts.Token));
            return conn;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            await DisposeFailedTransportAsync(transport, registrationCts.Token);
            throw new TimeoutException(
                $"Bolt registration timed out after {_config.TransportAttemptTimeoutMs} ms.",
                ex);
        }
        catch
        {
            await DisposeFailedTransportAsync(transport, registrationCts.Token);
            throw;
        }
    }

    private static async ValueTask DisposeFailedTransportAsync(
        IBoltConnection transport,
        CancellationToken deadlineToken)
    {
        try
        {
            var disposal = transport.DisposeAsync();
            if (disposal.IsCompletedSuccessfully)
            {
                disposal.GetAwaiter().GetResult();
                return;
            }

            await disposal.AsTask().WaitAsync(deadlineToken);
        }
        catch (OperationCanceledException) when (deadlineToken.IsCancellationRequested) { }
        catch { }
    }

    private async Task ScaleUpAsync()
    {
        lock (_connectionsLock)
        {
            if (_connections.Count >= _config.MaxConnections)
                return;
        }

        try
        {
            var conn = await CreateConnectionAsync(CancellationToken.None);
            var added = false;
            lock (_connectionsLock)
            {
                if (!_disposed && _connections.Count < _config.MaxConnections)
                {
                    _connections.Add(conn);
                    added = true;
                }
            }

            if (!added)
            {
                conn.CompleteSendChannel();
                conn.ReceiveCts?.Cancel();
                try { await conn.Transport.DisposeAsync(); } catch { }
                conn.ReceiveCts?.Dispose();
            }
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
                var connections = ClearConnections();
                foreach (var c in connections) { c.CompleteSendChannel(); c.ReceiveCts?.Cancel(); try { await c.Transport.DisposeAsync(); } catch { } }
            }
        }
        throw new InvalidOperationException($"Failed to connect after {maxRetries} attempts");
    }

    // ── RPC ──────────────────────────────────────────────────

    public async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Data)> InvokeAsync(
        string recipientId, string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected");

        // Auto-stream large payloads transparently
        if (payload.Length > _config.LargePayloadThreshold)
            return await InvokeLargeAsync(recipientId, commandName, payload, ct);

        var requestId = Guid.NewGuid();
        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = GetCommandHash(commandName);
        var conn = GetConnection();
        var rpcCall = PooledRpcCall.Rent();
        _pendingCalls[requestId] = rpcCall;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        ValueTask<BoltRpcResponse> responseTask = default;
        var responseTaskCreated = false;

        try
        {
            using var timeoutCts = new CancellationTokenSource(_rpcTimeout);
            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            rpcCall.RegisterTimeout(timeoutCts.Token);
            responseTask = rpcCall.GetTask();
            responseTaskCreated = true;

            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteRequest(writer, requestId, recipientHash, _senderHash, commandHash, payload.Span);

            // Proactive scale-up: if the least-loaded connection is still saturated, open a new one before sending
            if (conn.PendingSends > _config.ScaleUpThreshold && ConnectionCount < _config.MaxConnections)
            {
                _ = ScaleUpAsync(); // Start opening new connection in background
            }

            await conn.SendAsync(writer.WrittenMemory, sendCts.Token);

            responseTaskCreated = false;
            var response = await responseTask;
            sw.Stop();

            var level = (int)response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Debug;
            _logger.Log(level, "Bolt RPC {Command} -> {Recipient} | {StatusCode} in {Elapsed}ms | RequestSize={RequestSize}B ResponseSize={ResponseSize}B",
                commandName, recipientId, (int)response.StatusCode, sw.ElapsedMilliseconds, payload.Length, response.Data.Length);

            return (response.StatusCode, response.Data);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            var timeout = new TimeoutException($"Bolt RPC {commandName} -> {recipientId} timed out before the request was enqueued", ex);
            _logger.LogError(timeout, "Bolt RPC {Command} -> {Recipient} | FAILED in {Elapsed}ms | RequestSize={RequestSize}B",
                commandName, recipientId, sw.ElapsedMilliseconds, payload.Length);
            rpcCall.SetException(timeout);
            if (responseTaskCreated)
            {
                try { await responseTask; } catch { }
            }
            throw timeout;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Bolt RPC {Command} -> {Recipient} | FAILED in {Elapsed}ms | RequestSize={RequestSize}B",
                commandName, recipientId, sw.ElapsedMilliseconds, payload.Length);
            if (responseTaskCreated)
            {
                rpcCall.SetException(ex);
                try { await responseTask; } catch { }
            }
            throw;
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
        var commandHash = GetCommandHash(commandName);

        // Register pending RPC — response comes back as normal Response frame
        var rpcCall = PooledRpcCall.Rent();
        _pendingCalls[requestId] = rpcCall;
        ValueTask<BoltRpcResponse> responseTask = default;
        var responseTaskCreated = false;

        try
        {
            using var timeoutCts = new CancellationTokenSource(_rpcTimeout);
            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            rpcCall.RegisterTimeout(timeoutCts.Token);
            responseTask = rpcCall.GetTask();
            responseTaskCreated = true;

            // Open stream with special large-RPC command hash
            var stream = await OpenStreamAsync(recipientId, "__bolt_large_rpc__", sendCts.Token);

            // First chunk: metadata header [16:requestId][4:commandHash][4:totalSize][4:senderHash]
            var header = new byte[28];
            requestId.TryWriteBytes(header);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), commandHash);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), payload.Length);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), _hashCache.GetOrAdd(_clientId, BoltCodec.Fnv1aHash));
            await stream.SendAsync((ReadOnlyMemory<byte>)header, sendCts.Token);

            // Send payload in chunks
            var chunkSize = _config.StreamChunkSize;
            for (int offset = 0; offset < payload.Length; offset += chunkSize)
            {
                var len = Math.Min(chunkSize, payload.Length - offset);
                await stream.SendAsync(payload.Slice(offset, len), sendCts.Token);
            }

            // Close stream — signals "all data sent"
            await stream.CloseAsync(ct: sendCts.Token);

            // Response arrives as a Request with __bolt_large_rpc_response__ command
            // (handled by RegisterLargeRpcResponseHandler, which resolves our pending call)
            responseTaskCreated = false;
            var response = await responseTask;
            return (response.StatusCode, response.Data);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            var timeout = new TimeoutException($"Large Bolt RPC {commandName} -> {recipientId} timed out before the request completed", ex);
            rpcCall.SetException(timeout);
            if (responseTaskCreated)
            {
                try { await responseTask; } catch { }
            }
            throw timeout;
        }
        catch (Exception ex)
        {
            if (responseTaskCreated)
            {
                rpcCall.SetException(ex);
                try { await responseTask; } catch { }
            }
            throw;
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
        var hash = GetCommandHash(commandName);
        _handlers[hash] = handler;
    }

    /// <summary>
    /// Register a handler with CancellationToken that is cancelled when the connection drops.
    /// </summary>
    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = GetCommandHash(commandName);
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
        var commandHash = GetCommandHash(commandName);

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

    // ── Pub/Sub ──────────────────────────────────────────────

    private void HandleEventFrame(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadEvent(
                buffer.AsSpan(0, length),
                out var topicHash,
                out var sequence,
                out var isReplay,
                out var subscriberId,
                out var payloadOffset,
                out var payloadLength,
                out _))
            return;

        var payload = new byte[payloadLength];
        buffer.AsSpan(payloadOffset, payloadLength).CopyTo(payload);

        // Try transient first
        if (string.IsNullOrEmpty(subscriberId) && _transientSubscriptions.TryGetValue(topicHash, out var transient))
        {
            if (!transient.Channel.Writer.TryWrite(payload))
                _transientSubscriptions.TryRemove(topicHash, out _);
            return;
        }

        if (!string.IsNullOrEmpty(subscriberId))
        {
            var durableKey = (topicHash, subscriberId);
            if (_durableSubscriptions.TryGetValue(durableKey, out var durable) &&
                !durable.Channel.Writer.TryWrite((sequence, isReplay, payload)))
            {
                FailDurableSubscription(durableKey, durable);
            }
            return;
        }

        // Backward-compatible fallback for frames from older hubs without a subscriber id.
        foreach (var kvp in _durableSubscriptions)
        {
            if (kvp.Key.TopicHash == topicHash)
            {
                if (!kvp.Value.Channel.Writer.TryWrite((sequence, isReplay, payload)))
                    FailDurableSubscription(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// Subscribe to a topic. Receives published messages as they arrive (transient — no persistence, no replay).
    /// Cancelling the cancellation token unsubscribes.
    /// </summary>
    public async IAsyncEnumerable<T> SubscribeAsync<T>(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default,
        string? actorAccessToken = null)
    {
        await foreach (var item in SubscribeAsync<T>(
                           topic,
                           ct,
                           _ => ValueTask.FromResult(actorAccessToken)))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<T> SubscribeAsync<T>(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        var channel = CreateTransientPubSubChannel<byte[]>();
        var sub = new TransientSubscription
        {
            Topic = topic,
            Channel = channel,
            ActorAccessTokenProvider = actorAccessTokenProvider
        };

        if (!_transientSubscriptions.TryAdd(topicHash, sub))
            throw new InvalidOperationException($"Already subscribed to topic '{topic}'");

        // Send Subscribe frame
        var conn = GetPrimaryConnection();
        var writer = RentedBufferWriter.GetThreadLocal();
        var actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, ct);
        BoltCodec.WriteSubscribe(writer, topic, _clientId, durable: false, actorAccessToken);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();

        try
        {
            while (await channel.Reader.WaitToReadAsync(ct))
            {
                while (channel.Reader.TryRead(out var payload))
                {
                    var item = MemoryPackSerializer.Deserialize<T>(payload);
                    if (item is not null) yield return item;
                }
            }
        }
        finally
        {
            _transientSubscriptions.TryRemove(topicHash, out _);
            try
            {
                var w = RentedBufferWriter.GetThreadLocal();
                actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, CancellationToken.None);
                BoltCodec.WriteUnsubscribe(w, topic, _clientId, actorAccessToken: actorAccessToken);
                await conn.SendAsync(w.WrittenMemory, CancellationToken.None);
                w.Reset();
            }
            catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Subscribe to a topic durably. On reconnect, queued messages are replayed.
    /// Each message must be acked via DurableMessage.AckAsync to prevent re-delivery.
    /// </summary>
    public async IAsyncEnumerable<DurableMessage<T>> SubscribeDurableAsync<T>(
        string topic,
        string subscriberId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default,
        string? actorAccessToken = null)
    {
        await foreach (var item in SubscribeDurableAsync<T>(
                           topic,
                           subscriberId,
                           ct,
                           _ => ValueTask.FromResult(actorAccessToken)))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<DurableMessage<T>> SubscribeDurableAsync<T>(
        string topic,
        string subscriberId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        var key = (topicHash, subscriberId);
        var channel = CreateDurablePubSubChannel<(long, bool, byte[])>();
        var sub = new DurableSubscription
        {
            Topic = topic,
            SubscriberId = subscriberId,
            Channel = channel,
            ActorAccessTokenProvider = actorAccessTokenProvider
        };

        if (!_durableSubscriptions.TryAdd(key, sub))
            throw new InvalidOperationException($"Already subscribed to topic '{topic}' with subscriberId '{subscriberId}'");

        var conn = GetPrimaryConnection();
        var writer = RentedBufferWriter.GetThreadLocal();
        var actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, ct);
        BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true, actorAccessToken);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();

        try
        {
            while (await channel.Reader.WaitToReadAsync(ct))
            {
                while (channel.Reader.TryRead(out var entry))
                {
                    var (seq, isReplay, payload) = entry;
                    var item = MemoryPackSerializer.Deserialize<T>(payload);
                    if (item is null) continue;

                    yield return new DurableMessage<T>(item, seq, isReplay, async (s, c) =>
                    {
                        var freshActorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, c);
                        await AckAsync(topic, subscriberId, s, c, freshActorAccessToken);
                    });
                }
            }
        }
        finally
        {
            _durableSubscriptions.TryRemove(key, out _);
            // Durable cancellation/disconnect is a live detach, not a permanent unregister.
            // The hub must keep the durable subscriber registered so offline messages can
            // queue and replay when the same subscriber id reconnects.
            try
            {
                var w = RentedBufferWriter.GetThreadLocal();
                actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, CancellationToken.None);
                BoltCodec.WriteUnsubscribe(w, topic, subscriberId, permanent: false, actorAccessToken);
                await conn.SendAsync(w.WrittenMemory, CancellationToken.None);
                w.Reset();
            }
            catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Permanently unregister a durable subscriber from a topic.
    /// Normal durable subscription cancellation only detaches the live connection.
    /// </summary>
    public async ValueTask UnregisterDurableSubscriptionAsync(string topic, string subscriberId, CancellationToken ct = default)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        _durableSubscriptions.TryRemove((topicHash, subscriberId), out _);

        var conn = GetPrimaryConnection();
        var w = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteUnsubscribe(w, topic, subscriberId);
        await conn.SendAsync(w.WrittenMemory, ct);
        w.Reset();
    }

    /// <summary>
    /// Permanently unregister a transient subscriber from a topic.
    /// </summary>
    public async ValueTask UnsubscribeAsync(string topic, CancellationToken ct = default)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        _transientSubscriptions.TryRemove(topicHash, out _);

        var conn = GetPrimaryConnection();
        var w = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteUnsubscribe(w, topic, _clientId);
        await conn.SendAsync(w.WrittenMemory, ct);
        w.Reset();
    }

    /// <summary>
    /// Publish a message to a topic. If durable=true, the Hub queues the message for any
    /// currently-registered durable subscribers (so offline subscribers receive it on reconnect).
    /// If durable=false, the message is fan-out only.
    /// </summary>
    public async ValueTask PublishAsync<T>(string topic, T payload, bool durable = false, CancellationToken ct = default)
    {
        var bytes = MemoryPackSerializer.Serialize(payload);
        var conn = GetPrimaryConnection();
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WritePublish(writer, topic, durable, bytes);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();
    }

    /// <summary>
    /// Acknowledge durable messages up to and including upToSequence for a (topic, subscriber) pair.
    /// </summary>
    public async ValueTask AckAsync(
        string topic,
        string subscriberId,
        long upToSequence,
        CancellationToken ct = default,
        string? actorAccessToken = null)
    {
        var conn = GetPrimaryConnection();
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteAck(writer, topic, subscriberId, upToSequence, actorAccessToken);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();
    }

    // ── Streaming ────────────────────────────────────────────

    public void RegisterStreamHandler(string commandName, Func<BoltStream, Task> handler)
    {
        var hash = GetCommandHash(commandName);
        _streamHandlers[hash] = handler;
    }

    public async Task<BoltStream> OpenStreamAsync(string recipientId, string commandName, CancellationToken ct = default)
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected");
        var streamId = Guid.NewGuid();
        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = GetCommandHash(commandName);
        var conn = GetConnection();
        var stream = new BoltStream(streamId, conn, RemoveActiveStream);
        _activeStreams[streamId] = stream;
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteStreamOpen(writer, streamId, recipientHash, commandHash);
        await conn.SendAsync(writer.WrittenMemory, ct);
        return stream;
    }

    private int GetCommandHash(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var hash = _hashCache.GetOrAdd(commandName, BoltCodec.Fnv1aHash);
        var existing = _commandNamesByHash.GetOrAdd(hash, commandName);
        if (!string.Equals(existing, commandName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Bolt command hash collision detected. hash={hash}, existing='{existing}', rejected='{commandName}'");
        }

        return hash;
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
        lock (_connectionsLock)
        {
            var count = _connections.Count;
            if (count == 0) throw new InvalidOperationException("Not connected");
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
                if (bytesRead > _maxFrameBytes)
                {
                    _logger.LogWarning("Closing Bolt {Transport} connection because frame fragment exceeded max size. bytes={Bytes} max={Max}",
                        conn.TransportType,
                        bytesRead,
                        _maxFrameBytes);
                    break;
                }

                // Handle multi-frame messages (large payloads)
                byte[] frameBytes;
                int totalLength;
                if (!endOfMessage)
                {
                    // Multi-frame: accumulate into a growing pooled buffer
                    var assembled = bytesRead;
                    var capacity = Math.Min(_maxFrameBytes, Math.Max(bytesRead * 4, 512 * 1024));
                    if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
                    largeBuffer = ArrayPool<byte>.Shared.Rent(capacity);
                    buffer.AsSpan(0, bytesRead).CopyTo(largeBuffer);

                    while (!endOfMessage)
                    {
                        (bytesRead, endOfMessage) = await conn.Transport.ReceiveAsync(buffer.AsMemory(), ct);
                        if (bytesRead == 0 && endOfMessage) break;
                        if (bytesRead > _maxFrameBytes || assembled > _maxFrameBytes - bytesRead)
                        {
                            _logger.LogWarning("Closing Bolt {Transport} connection because assembled frame exceeded max size. current={Current} fragment={Fragment} max={Max}",
                                conn.TransportType,
                                assembled,
                                bytesRead,
                                _maxFrameBytes);
                            return;
                        }

                        // Grow if needed
                        if (assembled + bytesRead > largeBuffer.Length)
                        {
                            var newCapacity = Math.Min(_maxFrameBytes, Math.Max(assembled + bytesRead, largeBuffer.Length * 2));
                            var newBuf = ArrayPool<byte>.Shared.Rent(newCapacity);
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

                if (totalLength <= 0 || totalLength > _maxFrameBytes)
                {
                    _logger.LogWarning("Closing Bolt {Transport} connection because frame size was invalid. size={Size} max={Max}",
                        conn.TransportType,
                        totalLength,
                        _maxFrameBytes);
                    break;
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
                        await HandleStreamDataAsync(frameBytes.AsMemory(0, totalLength), ct);
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
        catch (Exception ex)
        {
            conn.RecordReceiveLoopFault();
            _logger.LogWarning("Bolt {Transport} receive error: {Error}", conn.TransportType, ex.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
            if (!_disposed)
            {
                var noConnections = RemoveConnection(conn, out var removed);
                if (removed && _isRegistered)
                {
                    conn.RecordUnexpectedDisconnect();
                    if (noConnections)
                    {
                        _isRegistered = false;
                        RaiseLifecycleEvent(Disconnected);
                        foreach (var (id, _) in _pendingCalls)
                            if (_pendingCalls.TryRemove(id, out var call))
                                call.SetException(new InvalidOperationException("Connection lost"));
                        _ = Task.Run(() => ReconnectAsync());
                    }
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
        var stream = new BoltStream(streamId, conn, RemoveActiveStream);
        _activeStreams[streamId] = stream;
        if (_streamHandlers.TryGetValue(commandHash, out var handler))
        {
            _ = Task.Run(async () =>
            {
                try { await handler(stream); }
                catch (Exception ex) { _logger.LogError(ex, "Stream handler error"); }
                finally { RemoveActiveStream(streamId); }
            }, ct);
        }
    }

    private async ValueTask HandleStreamDataAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var span = data.Span;
        if (!BoltCodec.TryReadStreamData(span, out var streamId, out var payloadOffset, out var payloadLength, out _)) return;
        if (_activeStreams.TryGetValue(streamId, out var stream))
        {
            var owner = new PooledMemoryOwner(payloadLength);
            span.Slice(payloadOffset, payloadLength).CopyTo(owner.WritableSpan);
            await stream.EnqueueInboundAsync(owner.Memory, ct);
        }
    }

    private void HandleStreamClose(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamClose(data, out var streamId, out var statusCode)) return;
        if (_activeStreams.TryRemove(streamId, out var stream))
            stream.MarkClosed(statusCode);
    }

    private void RemoveActiveStream(Guid streamId)
        => _activeStreams.TryRemove(streamId, out _);

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
            var respStream = new BoltStream(Guid.NewGuid(), conn, RemoveActiveStream);
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

    private async Task ReconnectAsync()
    {
        _logger.LogInformation("Attempting reconnection...");
        try
        {
            RaiseLifecycleEvent(Reconnecting);
            await ConnectWithRetryAsync(CancellationToken.None);
            Interlocked.Increment(ref _totalSuccessfulReconnects);
            RaiseLifecycleEvent(Reconnected);

            // Re-send all active subscriptions after reconnect
            foreach (var (_, sub) in _transientSubscriptions)
            {
                try
                {
                    var w = RentedBufferWriter.GetThreadLocal();
                    var actorAccessToken = await ResolveActorAccessTokenAsync(sub.ActorAccessTokenProvider, CancellationToken.None);
                    BoltCodec.WriteSubscribe(w, sub.Topic, _clientId, durable: false, actorAccessToken);
                    await GetPrimaryConnection().SendAsync(w.WrittenMemory, CancellationToken.None);
                    w.Reset();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to re-send transient subscription for topic {Topic}", sub.Topic);
                }
            }

            foreach (var (_, sub) in _durableSubscriptions)
            {
                try
                {
                    var w = RentedBufferWriter.GetThreadLocal();
                    var actorAccessToken = await ResolveActorAccessTokenAsync(sub.ActorAccessTokenProvider, CancellationToken.None);
                    BoltCodec.WriteSubscribe(w, sub.Topic, sub.SubscriberId, durable: true, actorAccessToken);
                    await GetPrimaryConnection().SendAsync(w.WrittenMemory, CancellationToken.None);
                    w.Reset();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to re-send durable subscription for topic {Topic} subscriber {SubscriberId}", sub.Topic, sub.SubscriberId);
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Reconnection failed"); }
    }

    private void RaiseLifecycleEvent(Action? handler)
    {
        if (handler is null)
            return;

        foreach (Action subscriber in handler.GetInvocationList())
        {
            try { subscriber(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Bolt lifecycle callback failed"); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        var connections = ClearConnections();
        foreach (var conn in connections)
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
        _isRegistered = false;
    }

    private int ConnectionCount
    {
        get
        {
            lock (_connectionsLock)
                return _connections.Count;
        }
    }

    private bool RemoveConnection(BoltConnection conn, out bool removed)
    {
        lock (_connectionsLock)
        {
            removed = _connections.Remove(conn);
            return _connections.Count == 0;
        }
    }

    private BoltConnection[] ClearConnections()
    {
        lock (_connectionsLock)
        {
            var connections = _connections.ToArray();
            _connections.Clear();
            return connections;
        }
    }

    private void ObserveConnection(BoltConnection connection) =>
        connection.FailureObserver = RecordConnectionFailure;

    private void RecordConnectionFailure(BoltConnectionFailureKind failure)
    {
        switch (failure)
        {
            case BoltConnectionFailureKind.SendFailure:
            case BoltConnectionFailureKind.EnqueueFailure:
                Interlocked.Increment(ref _totalSendFailures);
                break;
            case BoltConnectionFailureKind.SendTimeout:
            case BoltConnectionFailureKind.EnqueueTimeout:
                Interlocked.Increment(ref _totalSendTimeouts);
                break;
            case BoltConnectionFailureKind.ReceiveLoopFault:
                Interlocked.Increment(ref _totalReceiveLoopFaults);
                break;
            case BoltConnectionFailureKind.UnexpectedDisconnect:
                Interlocked.Increment(ref _totalUnexpectedDisconnects);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure), failure, null);
        }
    }

    private sealed class TransientSubscription
    {
        public required string Topic { get; init; }
        public required Func<CancellationToken, ValueTask<string?>> ActorAccessTokenProvider { get; init; }
        public required Channel<byte[]> Channel { get; init; }
    }

    private sealed class DurableSubscription
    {
        public required string Topic { get; init; }
        public required string SubscriberId { get; init; }
        public required Func<CancellationToken, ValueTask<string?>> ActorAccessTokenProvider { get; init; }
        public required Channel<(long Sequence, bool IsReplay, byte[] Payload)> Channel { get; init; }
    }

    private static async ValueTask<string?> ResolveActorAccessTokenAsync(
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct)
    {
        var token = await actorAccessTokenProvider(ct);
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private Channel<T> CreateTransientPubSubChannel<T>() =>
        Channel.CreateBounded<T>(new BoundedChannelOptions(Math.Max(1, _config.PubSubChannelCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private Channel<T> CreateDurablePubSubChannel<T>() =>
        Channel.CreateBounded<T>(new BoundedChannelOptions(Math.Max(1, _config.PubSubChannelCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    private void FailDurableSubscription(
        (int TopicHash, string SubscriberId) key,
        DurableSubscription durable)
    {
        if (_durableSubscriptions.TryRemove(key, out var removed) && ReferenceEquals(removed, durable))
        {
            removed.Channel.Writer.TryComplete(new InvalidOperationException(
                $"Durable Bolt subscription buffer is full. topic={removed.Topic} subscriber={removed.SubscriberId}"));
        }
    }
}

internal enum BoltConnectionFailureKind
{
    SendFailure,
    SendTimeout,
    EnqueueFailure,
    EnqueueTimeout,
    ReceiveLoopFault,
    UnexpectedDisconnect
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
    private readonly TimeSpan _sendEnqueueTimeout;
    private int _pendingSends;
    private int _activeSends;
    private long _activeSendStartedAt;

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
    internal Action<BoltConnectionFailureKind>? FailureObserver { get; set; }
    public int PendingSends => _pendingSends;
    public int ActiveSends => _activeSends;
    public long ActiveSendElapsedMs
    {
        get
        {
            var startedAt = Interlocked.Read(ref _activeSendStartedAt);
            return startedAt == 0 ? 0 : Math.Max(0, Environment.TickCount64 - startedAt);
        }
    }

    public BoltConnection(IBoltConnection transport, int sendQueueCapacity = 4096, int sendEnqueueTimeoutMs = 0)
    {
        Transport = transport;
        TransportType = transport.TransportType;
        _sendEnqueueTimeout = sendEnqueueTimeoutMs > 0
            ? TimeSpan.FromMilliseconds(sendEnqueueTimeoutMs)
            : TimeSpan.Zero;
        _sendChannel = Channel.CreateBounded<(byte[], int, CancellationToken)>(
            new BoundedChannelOptions(Math.Max(1, sendQueueCapacity))
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
                        Interlocked.Exchange(ref _activeSendStartedAt, Environment.TickCount64);
                        Interlocked.Increment(ref _activeSends);
                        if (Transport.IsConnected)
                        {
                            using var sendTimeoutCts = _sendEnqueueTimeout > TimeSpan.Zero
                                ? new CancellationTokenSource(_sendEnqueueTimeout)
                                : null;
                            using var linkedSendCts = sendTimeoutCts is null
                                ? CancellationTokenSource.CreateLinkedTokenSource(ct, sendCt)
                                : CancellationTokenSource.CreateLinkedTokenSource(ct, sendCt, sendTimeoutCts.Token);
                            await Transport.SendAsync(buf.AsMemory(0, len), linkedSendCts.Token);
                        }
                    }
                    catch (OperationCanceledException) when (
                        !ct.IsCancellationRequested &&
                        !sendCt.IsCancellationRequested)
                    {
                        FailureObserver?.Invoke(BoltConnectionFailureKind.SendTimeout);
                    }
                    catch (OperationCanceledException) { }
                    catch
                    {
                        FailureObserver?.Invoke(BoltConnectionFailureKind.SendFailure);
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref _activeSends) <= 0)
                            Interlocked.Exchange(ref _activeSendStartedAt, 0);
                        ArrayPool<byte>.Shared.Return(buf);
                        Interlocked.Decrement(ref _pendingSends);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                while (_sendChannel.Reader.TryRead(out var pending))
                {
                    ArrayPool<byte>.Shared.Return(pending.Buffer);
                    Interlocked.Decrement(ref _pendingSends);
                }
            }
        }, ct);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        // Snapshot into a pooled buffer — the caller's buffer (thread-local RentedBufferWriter)
        // may be reused before the async transport write completes.
        var len = data.Length;
        var buf = ArrayPool<byte>.Shared.Rent(len);
        data.Span.CopyTo(buf);
        Interlocked.Increment(ref _pendingSends);

        // All sends go through Channel (serialized single-writer)
        if (_sendChannel.Writer.TryWrite((buf, len, ct)))
            return ValueTask.CompletedTask;
        return SendSlowAsync(buf, len, ct);
    }

    private async ValueTask SendSlowAsync(byte[] buf, int len, CancellationToken ct)
    {
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;
        var enqueueToken = ct;
        try
        {
            if (_sendEnqueueTimeout > TimeSpan.Zero)
            {
                timeoutCts = new CancellationTokenSource(_sendEnqueueTimeout);
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                enqueueToken = linkedCts.Token;
            }

            await _sendChannel.Writer.WriteAsync((buf, len, ct), enqueueToken);
        }
        catch (OperationCanceledException) when (
            !ct.IsCancellationRequested &&
            timeoutCts is { IsCancellationRequested: true })
        {
            FailureObserver?.Invoke(BoltConnectionFailureKind.EnqueueTimeout);
            ArrayPool<byte>.Shared.Return(buf);
            Interlocked.Decrement(ref _pendingSends);
            throw;
        }
        catch (OperationCanceledException)
        {
            ArrayPool<byte>.Shared.Return(buf);
            Interlocked.Decrement(ref _pendingSends);
            throw;
        }
        catch
        {
            FailureObserver?.Invoke(BoltConnectionFailureKind.EnqueueFailure);
            ArrayPool<byte>.Shared.Return(buf);
            Interlocked.Decrement(ref _pendingSends);
            throw;
        }
        finally
        {
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
        }
    }

    /// <summary>Signal that no more sends will be enqueued. The send loop will drain and exit.</summary>
    public void CompleteSendChannel() => _sendChannel.Writer.TryComplete();

    internal void RecordReceiveLoopFault() =>
        FailureObserver?.Invoke(BoltConnectionFailureKind.ReceiveLoopFault);

    internal void RecordUnexpectedDisconnect() =>
        FailureObserver?.Invoke(BoltConnectionFailureKind.UnexpectedDisconnect);
}

/// <summary>Response data from an RPC call.</summary>
public struct BoltRpcResponse
{
    public HttpStatusCode StatusCode;
    public ReadOnlyMemory<byte> Data;
}

public sealed record BoltClientHealthSnapshot(
    bool IsRegistered,
    int ConnectionCount,
    int ConnectedTransports,
    int PendingSends,
    int ActiveSends,
    long MaxActiveSendElapsedMs,
    int RunningSendLoops,
    int RunningReceiveLoops,
    int FaultedSendLoops,
    int FaultedReceiveLoops,
    int PendingSendsUnhealthyThreshold,
    int ActiveSendUnhealthyThresholdMs,
    long TotalSendFailures,
    long TotalSendTimeouts,
    long TotalReceiveLoopFaults,
    long TotalUnexpectedDisconnects,
    long TotalSuccessfulReconnects)
{
    public bool IsHealthy =>
        IsRegistered &&
        ConnectionCount > 0 &&
        ConnectedTransports == ConnectionCount &&
        RunningSendLoops == ConnectionCount &&
        RunningReceiveLoops == ConnectionCount &&
        FaultedSendLoops == 0 &&
        FaultedReceiveLoops == 0 &&
        MaxActiveSendElapsedMs <= ActiveSendUnhealthyThresholdMs &&
        PendingSends <= PendingSendsUnhealthyThreshold;
}
