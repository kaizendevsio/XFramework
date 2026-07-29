using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Numerics;
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
    private readonly int _maxLargeRpcPayloadBytes;
    private readonly long _maxBufferedLargeRpcBytes;
    private readonly int _largePayloadThreshold;
    private readonly int _streamChunkSize;
    private readonly int _maxLargeRpcChunksInFlight;
    private long _bufferedLargeRpcBytes;

    private readonly List<BoltConnection> _connections = [];
    private readonly object _connectionsLock = new();
    private volatile bool _isRegistered;
    private volatile bool _disposed;
    private long _totalSendFailures;
    private long _totalSendTimeouts;
    private long _totalReceiveLoopFaults;
    private long _totalUnexpectedDisconnects;
    private long _totalSuccessfulReconnects;
    private int _scaleUpInProgress;

    private readonly ConcurrentDictionary<Guid, PooledRpcCall> _pendingCalls = new();
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, BoltInboundRequestContext, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _handlers = new();
    private readonly ConcurrentDictionary<string, int> _hashCache = new();
    private readonly ConcurrentDictionary<int, string> _commandNamesByHash = new();
    private readonly ConcurrentDictionary<Guid, BoltStream> _activeStreams = new();
    private readonly ConcurrentDictionary<Guid, LargeRpcInboundCollector> _largeRpcCollectors = new();
    private int _activeStreamCount;
    private readonly ConcurrentDictionary<int, Func<BoltStream, Task>> _streamHandlers = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _inboundRequestCancellations = new();
    private readonly ConcurrentDictionary<Guid, long> _earlyInboundCancellations = new();
    private readonly SemaphoreSlim _inboundDispatchSlots;
    private TimeSpan _rpcTimeout;

    private static readonly Action<ILogger, string, string, int, long, int, int, Exception?> LogRpcCompletedDebug =
        LoggerMessage.Define<string, string, int, long, int, int>(
            LogLevel.Debug,
            new EventId(1001, nameof(InvokeAsync)),
            "Bolt RPC {Command} -> {Recipient} | {StatusCode} in {Elapsed}ms | RequestSize={RequestSize}B ResponseSize={ResponseSize}B");

    private static readonly Action<ILogger, string, string, int, long, int, int, Exception?> LogRpcCompletedWarning =
        LoggerMessage.Define<string, string, int, long, int, int>(
            LogLevel.Warning,
            new EventId(1002, nameof(InvokeAsync)),
            "Bolt RPC {Command} -> {Recipient} | {StatusCode} in {Elapsed}ms | RequestSize={RequestSize}B ResponseSize={ResponseSize}B");

    private static readonly Action<ILogger, string, string, long, int, Exception?> LogRpcFailed =
        LoggerMessage.Define<string, string, long, int>(
            LogLevel.Error,
            new EventId(1003, nameof(InvokeAsync)),
            "Bolt RPC {Command} -> {Recipient} | FAILED in {Elapsed}ms | RequestSize={RequestSize}B");

    private static readonly Action<ILogger, string, string, long, int, Exception?> LogRpcCanceled =
        LoggerMessage.Define<string, string, long, int>(
            LogLevel.Warning,
            new EventId(1004, nameof(InvokeAsync)),
            "Bolt RPC {Command} -> {Recipient} | CANCELED in {Elapsed}ms | RequestSize={RequestSize}B");

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
    /// Register a handler for a specific frame type. The buffer is valid only for the
    /// duration of the synchronous callback. Handlers must copy data they retain.
    /// </summary>
    public void RegisterFrameHandler(FrameType frameType, Action<BoltConnection, byte[], int> handler)
        => _frameHandlers[(byte)frameType] = handler;

    /// <summary>Remove a custom frame handler only when it is still the registered handler.</summary>
    public bool UnregisterFrameHandler(FrameType frameType, Action<BoltConnection, byte[], int> handler) =>
        _frameHandlers.TryRemove(
            new KeyValuePair<byte, Action<BoltConnection, byte[], int>>((byte)frameType, handler));

    /// <summary>Get the current primary connection for sending frames.</summary>
    public BoltConnection GetPrimaryConnection() => GetConnection();

    public bool IsConnected
    {
        get
        {
            lock (_connectionsLock)
                return _isRegistered && _connections.Exists(static connection => connection.IsAvailable);
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
        _maxLargeRpcPayloadBytes = Math.Max(1024, config.MaxLargeRpcPayloadBytes);
        _maxBufferedLargeRpcBytes = Math.Max(1, config.MaxBufferedLargeRpcBytes);
        var maxUnaryPayloadBytes = Math.Max(
            0,
            Math.Min(_maxFrameBytes, BoltCodec.DefaultMaxFrameBytes) - BoltCodec.RequestHeaderSize - 18);
        _largePayloadThreshold = Math.Clamp(config.LargePayloadThreshold, 0, maxUnaryPayloadBytes);
        var maxStreamChunkBytes = Math.Max(
            1,
            Math.Min(_maxFrameBytes, BoltCodec.DefaultMaxFrameBytes) - BoltCodec.StreamDataHeaderSize);
        _streamChunkSize = Math.Clamp(config.StreamChunkSize, 1, maxStreamChunkBytes);
        _maxLargeRpcChunksInFlight = Math.Clamp(
            Math.Max(_streamChunkSize, config.MaxLargeRpcPipelineBytes) / _streamChunkSize,
            1,
            128);
        _inboundDispatchSlots = new SemaphoreSlim(
            Math.Max(1, config.MaxConcurrentInboundHandlers),
            Math.Max(1, config.MaxConcurrentInboundHandlers));
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
            if (!_largeRpcCollectors.TryRemove(stream.StreamId, out var collector))
            {
                collector = new LargeRpcInboundCollector(
                    this,
                    headerSize: 28,
                    totalSizeOffset: 20,
                    usePooledBuffer: true);
                while (true)
                {
                    var (hasData, data) = await stream.ReadAsync();
                    if (!hasData)
                        break;
                    collector.Accept(data.Span);
                }
            }

            using var collectorOwner = collector;
            var closeTask = stream.IsClosed
                ? Task.CompletedTask
                : stream.WaitForCloseAsync(CancellationToken.None);
            if (!collector.HeaderProcessed)
                await Task.WhenAny(collector.HeaderReceived, closeTask);
            if (!collector.HasHeader)
                return;

            var headerSpan = collector.HeaderSpan;
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

                    var conn = stream.Connection;
                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WritePush(writer, Guid.NewGuid(), senderHash, _senderHash, LargeRpcResponseHash, respBuf.AsSpan(0, 18));
                    await conn.SendReliableAsync(writer, CancellationToken.None);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(respBuf);
                }
            }

            if (collector.RejectionStatus is { } rejectionStatus)
            {
                await SendLargeRpcErrorAsync(rejectionStatus);
                return;
            }

            if (!stream.IsClosed)
                await closeTask;

            if (collector.IsMalformed || collector.BytesRead != totalSize)
            {
                await SendLargeRpcErrorAsync(HttpStatusCode.BadRequest);
                return;
            }

            var bufferReservationSize = collector.ReservationSize;
            byte[]? buffer = collector.DetachBuffer(transferReservation: true);
            CancellationTokenSource? requestCts = null;
            try
            {
            // Build response
            HttpStatusCode statusCode;
            ReadOnlyMemory<byte> responsePayload;

            requestCts = new CancellationTokenSource(_rpcTimeout);
            if (!_inboundRequestCancellations.TryAdd(requestId, requestCts))
                return;
            if (_earlyInboundCancellations.TryRemove(requestId, out _))
                requestCts.Cancel();

            if (_handlers.TryGetValue(commandHash, out var handler))
            {
                try
                {
                    (statusCode, responsePayload) = await handler(
                        buffer.AsMemory(0, totalSize),
                        new BoltInboundRequestContext(requestId, senderHash),
                        requestCts.Token);
                }
                catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
                {
                    return;
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

            if (responsePayload.Length > _maxLargeRpcPayloadBytes)
            {
                await SendLargeRpcErrorAsync(HttpStatusCode.RequestEntityTooLarge);
                return;
            }

            // Response: small → single Push, large → stream back
            if (responsePayload.Length <= _largePayloadThreshold)
            {
                // Small response: single Push frame — pool the response data buffer
                var respLen = 18 + responsePayload.Length;
                var respBuf = ArrayPool<byte>.Shared.Rent(respLen);
                try
                {
                    requestId.TryWriteBytes(respBuf);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(respBuf.AsSpan(16), (short)statusCode);
                    responsePayload.CopyTo(respBuf.AsMemory(18));

                    var conn = stream.Connection;
                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WritePush(writer, Guid.NewGuid(), senderHash, _senderHash, LargeRpcResponseHash, respBuf.AsSpan(0, respLen));
                    await conn.SendReliableAsync(writer, requestCts.Token);
                }
                finally { ArrayPool<byte>.Shared.Return(respBuf); }
            }
            else
            {
                // Large response: stream it back via __bolt_large_rpc_response_stream__
                // Sender hash is used as recipientId for the reverse stream
                var responseConnection = stream.Connection;
                var respStream = new BoltStream(
                    Guid.NewGuid(),
                    responseConnection,
                    RemoveActiveStream,
                    _config.StreamInboundCapacity);
                if (!TryTrackStream(respStream))
                {
                    var rejected = new byte[18];
                    requestId.TryWriteBytes(rejected);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(
                        rejected.AsSpan(16),
                        (short)HttpStatusCode.TooManyRequests);
                    var rejectedWriter = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WritePush(
                        rejectedWriter,
                        Guid.NewGuid(),
                        senderHash,
                        _senderHash,
                        LargeRpcResponseHash,
                        rejected);
                    await responseConnection.SendReliableAsync(rejectedWriter, requestCts.Token);
                    return;
                }

                // StreamOpen to sender
                var openWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteStreamOpen(openWriter, respStream.StreamId, senderHash, LargeRpcResponseStreamHash);
                await responseConnection.SendReliableAsync(openWriter, requestCts.Token);

                // Header: [16:requestId][2:statusCode][4:totalSize]
                var respHeader = new byte[22];
                requestId.TryWriteBytes(respHeader);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(respHeader.AsSpan(16), (short)statusCode);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(respHeader.AsSpan(18), responsePayload.Length);
                await respStream.SendAsync((ReadOnlyMemory<byte>)respHeader, requestCts.Token);

                await SendLargePayloadPipelinedAsync(respStream, responsePayload, requestCts.Token);
                await respStream.CloseAsync(ct: requestCts.Token);
            }
            }
            finally
            {
                _earlyInboundCancellations.TryRemove(requestId, out _);
                if (requestCts is not null)
                {
                    _inboundRequestCancellations.TryRemove(
                        new KeyValuePair<Guid, CancellationTokenSource>(requestId, requestCts));
                    requestCts.Dispose();
                }
                if (buffer is not null && bufferReservationSize > 0)
                    ArrayPool<byte>.Shared.Return(buffer);
                ReleaseLargeRpcBuffer(bufferReservationSize);
            }
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
                var response = GC.AllocateUninitializedArray<byte>(payload.Length - 18);
                payload.Span[18..].CopyTo(response);
                respPayload = response;
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
            if (!_largeRpcCollectors.TryRemove(respStream.StreamId, out var collector))
            {
                collector = new LargeRpcInboundCollector(
                    this,
                    headerSize: 22,
                    totalSizeOffset: 18,
                    usePooledBuffer: false);
                while (true)
                {
                    var (hasData, data) = await respStream.ReadAsync();
                    if (!hasData)
                        break;
                    collector.Accept(data.Span);
                }
            }

            using var collectorOwner = collector;
            var closeTask = respStream.IsClosed
                ? Task.CompletedTask
                : respStream.WaitForCloseAsync(CancellationToken.None);
            if (!collector.HeaderProcessed)
                await Task.WhenAny(collector.HeaderReceived, closeTask);
            if (!collector.HasHeader)
                return;

            var hdr = collector.HeaderSpan;
            var requestId = new Guid(hdr[..16]);
            var statusCode = (HttpStatusCode)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(hdr[16..]);
            var totalSize = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(hdr[18..]);

            if (collector.RejectionStatus is { } rejectionStatus)
            {
                if (_pendingCalls.TryRemove(requestId, out var rejectedCall))
                {
                    rejectedCall.SetResult(new BoltRpcResponse
                    {
                        StatusCode = rejectionStatus,
                        Data = ReadOnlyMemory<byte>.Empty
                    });
                }
                return;
            }

            if (!respStream.IsClosed)
                await closeTask;

            if (!_pendingCalls.TryGetValue(requestId, out var pendingCall))
                return;

                if (collector.IsMalformed)
                {
                    if (_pendingCalls.TryRemove(requestId, out var rpcCall))
                        rpcCall.SetException(new InvalidOperationException("Large RPC response exceeded declared size"));
                    return;
                }

                if (collector.BytesRead != totalSize)
                {
                    if (_pendingCalls.TryRemove(requestId, out var rpcCall))
                    {
                        if (respStream.CloseStatus is { } closeStatus && closeStatus != HttpStatusCode.OK)
                        {
                            rpcCall.SetResult(new BoltRpcResponse
                            {
                                StatusCode = closeStatus,
                                Data = ReadOnlyMemory<byte>.Empty
                            });
                        }
                        else
                        {
                            rpcCall.SetException(new InvalidOperationException("Large RPC response ended before declared size"));
                        }
                    }
                    return;
                }

                var response = collector.DetachBuffer();
                if (_pendingCalls.TryRemove(
                        new KeyValuePair<Guid, PooledRpcCall>(requestId, pendingCall)))
                {
                    pendingCall.SetResult(new BoltRpcResponse { StatusCode = statusCode, Data = response });
                }
        });
    }

    private static readonly int LargeRpcResponseHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response__");
    private static readonly int LargeRpcResponseStreamHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response_stream__");
    private async Task SendLargePayloadPipelinedAsync(
        BoltStream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct)
    {
        PooledSendCompletion?[]? pending =
            ArrayPool<PooledSendCompletion?>.Shared.Rent(_maxLargeRpcChunksInFlight);
        var head = 0;
        var count = 0;
        var chunkSize = _streamChunkSize;

        try
        {
            for (var offset = 0; offset < payload.Length; offset += chunkSize)
            {
                var length = Math.Min(chunkSize, payload.Length - offset);
                var completion = await stream.EnqueueAsync(payload.Slice(offset, length), ct);
                pending[(head + count) % _maxLargeRpcChunksInFlight] = completion;
                count++;

                if (count == _maxLargeRpcChunksInFlight)
                {
                    var oldest = pending[head]!;
                    pending[head] = null;
                    head = (head + 1) % _maxLargeRpcChunksInFlight;
                    count--;
                    await oldest.WaitAsync(ct);
                }
            }

            while (count > 0)
            {
                var completion = pending[head]!;
                pending[head] = null;
                head = (head + 1) % _maxLargeRpcChunksInFlight;
                count--;
                await completion.WaitAsync(ct);
            }
        }
        catch
        {
            var pendingCleanup = pending;
            pending = null;
            _ = DrainLargeRpcSendCompletionsAsync(
                pendingCleanup,
                head,
                count,
                _maxLargeRpcChunksInFlight);
            throw;
        }
        finally
        {
            if (pending is not null)
            {
                Array.Clear(pending, 0, pending.Length);
                ArrayPool<PooledSendCompletion?>.Shared.Return(pending);
            }
        }
    }

    private static async Task DrainLargeRpcSendCompletionsAsync(
        PooledSendCompletion?[] pending,
        int head,
        int count,
        int capacity)
    {
        try
        {
            while (count > 0)
            {
                var completion = pending[head]!;
                pending[head] = null;
                head = (head + 1) % capacity;
                count--;
                try { await completion.WaitAsync(CancellationToken.None); }
                catch { }
            }
        }
        finally
        {
            Array.Clear(pending, 0, pending.Length);
            ArrayPool<PooledSendCompletion?>.Shared.Return(pending);
        }
    }

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
            var conn = new BoltConnection(
                transport,
                _config.SendQueueCapacity,
                sendEnqueueTimeoutMs,
                _config.EnableBatching);
            ObserveConnection(conn);

            // Registration send and ACK share one transport-attempt deadline.
            using var regWriter = new RentedBufferWriter(256);
            BoltCodec.WriteRegister(regWriter, _clientId, _clientName);
            await transport.SendAsync(regWriter.WrittenMemory, registrationCts.Token);

            var ackBuffer = ArrayPool<byte>.Shared.Rent(BoltCodec.RegisterAckSize);
            try
            {
                var (ackBytes, _) = await transport.ReceiveAsync(ackBuffer, registrationCts.Token);
                if (!BoltCodec.TryReadRegisterAck(
                        ackBuffer.AsSpan(0, ackBytes),
                        out var registrationAccepted,
                        out var serverWireVersion))
                {
                    throw new InvalidOperationException("Server returned an invalid Bolt registration acknowledgement.");
                }
                if (serverWireVersion != BoltCodec.WireVersion)
                {
                    throw new InvalidOperationException(
                        $"Bolt wire version mismatch. Client requires {BoltCodec.WireVersion}, server reported {serverWireVersion}.");
                }
                if (!registrationAccepted)
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
        if (Interlocked.CompareExchange(ref _scaleUpInProgress, 1, 0) != 0)
            return;

        try
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
        finally
        {
            Volatile.Write(ref _scaleUpInProgress, 0);
        }
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
        if (payload.Length > _maxLargeRpcPayloadBytes)
            return (HttpStatusCode.RequestEntityTooLarge, ReadOnlyMemory<byte>.Empty);

        var conn = GetConnection();

        // Auto-stream large payloads transparently
        if (payload.Length > _largePayloadThreshold)
            return await InvokeLargeAsync(conn, recipientId, commandName, payload, ct);

        var requestId = Guid.NewGuid();
        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = GetCommandHash(commandName);
        var rpcCall = PooledRpcCall.Rent(_config.RunRpcContinuationsAsynchronously);
        var responseTask = rpcCall.GetTask();
        _pendingCalls[requestId] = rpcCall;
        var requestSent = false;
        var responseConsumed = false;
        var measureElapsed = _logger.IsEnabled(LogLevel.Debug) ||
                             _logger.IsEnabled(LogLevel.Warning) ||
                             _logger.IsEnabled(LogLevel.Error);
        var startedAt = measureElapsed ? Stopwatch.GetTimestamp() : 0;

        try
        {
            using var deadlineCts = CreateDeadlineSource(ct, _rpcTimeout);
            var deadlineToken = deadlineCts.Token;
            rpcCall.RegisterCancellation(deadlineToken, this, requestId);

            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteRequest(writer, requestId, recipientHash, _senderHash, commandHash, payload.Span);

            // Proactive scale-up: if the least-loaded connection is still saturated, open a new one before sending
            if (conn.PendingSends > _config.ScaleUpThreshold && ConnectionCount < _config.MaxConnections)
            {
                _ = ScaleUpAsync(); // Start opening new connection in background
            }

            var sendCompletion = await conn.EnqueueReliableAsync(writer, deadlineToken);
            requestSent = true;
            await sendCompletion.WaitAsync(deadlineToken);

            BoltRpcResponse response;
            try { response = await responseTask; }
            finally { responseConsumed = true; }

            var elapsed = GetElapsedMilliseconds(startedAt);
            if ((int)response.StatusCode >= 400)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                    LogRpcCompletedWarning(_logger, commandName, recipientId, (int)response.StatusCode, elapsed, payload.Length, response.Data.Length, null);
            }
            else if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogRpcCompletedDebug(_logger, commandName, recipientId, (int)response.StatusCode, elapsed, payload.Length, response.Data.Length, null);
            }

            return (response.StatusCode, response.Data);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            if (requestSent) await TrySendRequestCancelAsync(conn, requestId);
            var timeout = new TimeoutException($"Bolt RPC {commandName} -> {recipientId} timed out before the request completed", ex);
            if (_logger.IsEnabled(LogLevel.Error))
                LogRpcFailed(_logger, commandName, recipientId, GetElapsedMilliseconds(startedAt), payload.Length, timeout);
            if (!responseConsumed)
                await CompleteFailedRpcAsync(requestId, rpcCall, responseTask, timeout);
            throw timeout;
        }
        catch (OperationCanceledException ex)
        {
            if (requestSent) await TrySendRequestCancelAsync(conn, requestId);
            if (_logger.IsEnabled(LogLevel.Warning))
                LogRpcCanceled(_logger, commandName, recipientId, GetElapsedMilliseconds(startedAt), payload.Length, ex);
            if (!responseConsumed)
                await CompleteFailedRpcAsync(requestId, rpcCall, responseTask, ex);
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                LogRpcFailed(_logger, commandName, recipientId, GetElapsedMilliseconds(startedAt), payload.Length, ex);
            if (!responseConsumed)
                await CompleteFailedRpcAsync(requestId, rpcCall, responseTask, ex);
            throw;
        }
        finally { _pendingCalls.TryRemove(new KeyValuePair<Guid, PooledRpcCall>(requestId, rpcCall)); }
    }

    /// <summary>
    /// Transparently stream a large payload via BoltStream, then wait for the RPC response.
    /// The recipient reassembles the stream and processes it as a normal RPC call.
    /// </summary>
    private async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Data)> InvokeLargeAsync(
        BoltConnection conn,
        string recipientId,
        string commandName,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct)
    {

        var requestId = Guid.NewGuid();
        var commandHash = GetCommandHash(commandName);

        // Register pending RPC — response comes back as normal Response frame
        var rpcCall = PooledRpcCall.Rent(_config.RunRpcContinuationsAsynchronously);
        var responseTask = rpcCall.GetTask();
        _pendingCalls[requestId] = rpcCall;
        var responseConsumed = false;
        BoltConnection? requestConnection = null;
        BoltStream? requestStream = null;

        try
        {
            using var deadlineCts = CreateDeadlineSource(ct, _rpcTimeout);
            var deadlineToken = deadlineCts.Token;
            rpcCall.RegisterCancellation(deadlineToken, this, requestId);

            // Open stream with special large-RPC command hash
            var stream = await OpenStreamAsync(conn, recipientId, "__bolt_large_rpc__", deadlineToken);
            requestStream = stream;
            requestConnection = stream.Connection;

            // First chunk: metadata header [16:requestId][4:commandHash][4:totalSize][4:senderHash]
            var header = new byte[28];
            requestId.TryWriteBytes(header);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), commandHash);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), payload.Length);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), _hashCache.GetOrAdd(_clientId, BoltCodec.Fnv1aHash));
            await stream.SendAsync((ReadOnlyMemory<byte>)header, deadlineToken);

            await SendLargePayloadPipelinedAsync(stream, payload, deadlineToken);

            // Close stream — signals "all data sent"
            await stream.CloseAsync(ct: deadlineToken);

            // Response arrives as a Request with __bolt_large_rpc_response__ command
            // (handled by RegisterLargeRpcResponseHandler, which resolves our pending call)
            BoltRpcResponse response;
            try { response = await responseTask; }
            finally { responseConsumed = true; }
            return (response.StatusCode, response.Data);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            if (requestConnection is not null) await TrySendRequestCancelAsync(requestConnection, requestId);
            if (requestStream is { IsClosed: false })
            {
                try { await requestStream.CloseAsync(HttpStatusCode.RequestTimeout, CancellationToken.None); }
                catch { RemoveActiveStream(requestStream.StreamId); }
            }
            var timeout = new TimeoutException($"Large Bolt RPC {commandName} -> {recipientId} timed out before the request completed", ex);
            if (!responseConsumed)
                await CompleteFailedRpcAsync(requestId, rpcCall, responseTask, timeout);
            throw timeout;
        }
        catch (OperationCanceledException ex)
        {
            if (requestConnection is not null) await TrySendRequestCancelAsync(requestConnection, requestId);
            if (requestStream is { IsClosed: false })
            {
                try { await requestStream.CloseAsync(HttpStatusCode.RequestTimeout, CancellationToken.None); }
                catch { RemoveActiveStream(requestStream.StreamId); }
            }
            if (!responseConsumed)
                await CompleteFailedRpcAsync(requestId, rpcCall, responseTask, ex);
            throw;
        }
        catch (Exception ex)
        {
            if (!responseConsumed)
                await CompleteFailedRpcAsync(requestId, rpcCall, responseTask, ex);
            throw;
        }
        finally { _pendingCalls.TryRemove(new KeyValuePair<Guid, PooledRpcCall>(requestId, rpcCall)); }
    }

    private async Task CompleteFailedRpcAsync(
        Guid requestId,
        PooledRpcCall rpcCall,
        ValueTask<BoltRpcResponse> responseTask,
        Exception ex)
    {
        if (_pendingCalls.TryRemove(new KeyValuePair<Guid, PooledRpcCall>(requestId, rpcCall)))
            rpcCall.SetException(ex);

        try { await responseTask; } catch { }
    }

    internal void CancelPendingCall(
        Guid requestId,
        PooledRpcCall rpcCall,
        CancellationToken cancellationToken)
    {
        if (_pendingCalls.TryRemove(new KeyValuePair<Guid, PooledRpcCall>(requestId, rpcCall)))
            rpcCall.SetException(new OperationCanceledException(cancellationToken));
    }

    private static CancellationTokenSource CreateDeadlineSource(
        CancellationToken callerToken,
        TimeSpan timeout)
    {
        var source = callerToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(callerToken)
            : new CancellationTokenSource();
        source.CancelAfter(timeout);
        return source;
    }

    private static long GetElapsedMilliseconds(long startedAt) =>
        startedAt == 0 ? 0 : (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    private bool TryReserveLargeRpcBuffer(int length)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _bufferedLargeRpcBytes);
            if (length > _maxBufferedLargeRpcBytes - current)
                return false;
            if (Interlocked.CompareExchange(ref _bufferedLargeRpcBytes, current + length, current) == current)
                return true;
        }
    }

    private void ReleaseLargeRpcBuffer(int length) =>
        Interlocked.Add(ref _bufferedLargeRpcBytes, -length);

    private static int GetPooledBufferReservationSize(int length)
    {
        if (length <= 0)
            return 0;
        if (length <= 16)
            return 16;
        return checked((int)BitOperations.RoundUpToPowerOf2((uint)length));
    }

    private async ValueTask TrySendRequestCancelAsync(BoltConnection connection, Guid requestId)
    {
        if (!connection.IsAvailable)
            return;
        try
        {
            using var cancelCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteRequestCancel(writer, requestId);
            await connection.SendReliableAsync(writer, cancelCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to propagate Bolt request cancellation for {RequestId}", requestId);
        }
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
        _handlers[hash] = (payload, context, _) => handler(payload, context.RequestId);
    }

    /// <summary>
    /// Register a handler with CancellationToken that is cancelled when the connection drops.
    /// </summary>
    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = GetCommandHash(commandName);
        _handlers[hash] = (payload, context, ct) => handler(payload, context.RequestId, ct);
    }

    /// <summary>
    /// Register a handler that receives the Hub-verified sender route hash.
    /// </summary>
    public void RegisterHandler(
        string commandName,
        Func<ReadOnlyMemory<byte>, BoltInboundRequestContext, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = GetCommandHash(commandName);
        _handlers[hash] = handler;
    }

    /// <summary>
    /// Send a fire-and-forget push message (no response expected).
    /// Use for typing indicators, presence updates, read receipts.
    /// </summary>
    public async ValueTask PushAsync(string recipientId, string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);
        if (!IsConnected) return;

        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = GetCommandHash(commandName);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WritePush(writer, Guid.NewGuid(), recipientHash, _senderHash, commandHash, payload.Span);
        await GetConnection().SendReliableAsync(writer, ct);
    }

    /// <summary>Typed push with MemoryPack serialization.</summary>
    public async ValueTask PushAsync<T>(string recipientId, string commandName, T data, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);
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
        var actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, ct);
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteSubscribe(writer, topic, _clientId, durable: false, actorAccessToken);
        await conn.SendReliableAsync(writer, ct);

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
                actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, CancellationToken.None);
                var w = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteUnsubscribe(w, topic, _clientId, actorAccessToken: actorAccessToken);
                await conn.SendReliableAsync(w, CancellationToken.None);
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
        var actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, ct);
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true, actorAccessToken);
        await conn.SendReliableAsync(writer, ct);

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
                actorAccessToken = await ResolveActorAccessTokenAsync(actorAccessTokenProvider, CancellationToken.None);
                var w = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteUnsubscribe(w, topic, subscriberId, permanent: false, actorAccessToken);
                await conn.SendReliableAsync(w, CancellationToken.None);
            }
            catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Permanently unregister a durable subscriber from a topic.
    /// Normal durable subscription cancellation only detaches the live connection.
    /// </summary>
    public ValueTask UnregisterDurableSubscriptionAsync(
        string topic,
        string subscriberId,
        CancellationToken ct = default) =>
        UnregisterDurableSubscriptionCoreAsync(topic, subscriberId, null, ct);

    /// <summary>
    /// Permanently unregister a durable subscriber using an end-user actor token.
    /// </summary>
    public ValueTask UnregisterDurableSubscriptionWithActorAsync(
        string topic,
        string subscriberId,
        string actorAccessToken,
        CancellationToken ct = default) =>
        UnregisterDurableSubscriptionCoreAsync(topic, subscriberId, actorAccessToken, ct);

    private async ValueTask UnregisterDurableSubscriptionCoreAsync(
        string topic,
        string subscriberId,
        string? actorAccessToken,
        CancellationToken ct)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        _durableSubscriptions.TryRemove((topicHash, subscriberId), out _);

        var conn = GetPrimaryConnection();
        var w = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteUnsubscribe(w, topic, subscriberId, actorAccessToken: actorAccessToken);
        await conn.SendReliableAsync(w, ct);
    }

    /// <summary>
    /// Permanently unregister a transient subscriber from a topic.
    /// </summary>
    public ValueTask UnsubscribeAsync(
        string topic,
        CancellationToken ct = default) =>
        UnsubscribeCoreAsync(topic, null, ct);

    /// <summary>
    /// Permanently unregister a transient subscriber using an end-user actor token.
    /// </summary>
    public ValueTask UnsubscribeWithActorAsync(
        string topic,
        string actorAccessToken,
        CancellationToken ct = default) =>
        UnsubscribeCoreAsync(topic, actorAccessToken, ct);

    private async ValueTask UnsubscribeCoreAsync(
        string topic,
        string? actorAccessToken,
        CancellationToken ct)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        _transientSubscriptions.TryRemove(topicHash, out _);

        var conn = GetPrimaryConnection();
        var w = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteUnsubscribe(w, topic, _clientId, actorAccessToken: actorAccessToken);
        await conn.SendReliableAsync(w, ct);
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
        await conn.SendReliableAsync(writer, ct);
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
        await conn.SendReliableAsync(writer, ct);
    }

    // ── Streaming ────────────────────────────────────────────

    public void RegisterStreamHandler(string commandName, Func<BoltStream, Task> handler)
    {
        var hash = GetCommandHash(commandName);
        _streamHandlers[hash] = handler;
    }

    public async Task<BoltStream> OpenStreamAsync(string recipientId, string commandName, CancellationToken ct = default)
    {
        var conn = GetConnection();
        return await OpenStreamAsync(conn, recipientId, commandName, ct);
    }

    private async Task<BoltStream> OpenStreamAsync(
        BoltConnection conn,
        string recipientId,
        string commandName,
        CancellationToken ct)
    {
        if (_activeStreams.Count >= Math.Max(1, _config.MaxActiveStreams))
            throw new InvalidOperationException("Bolt client active stream limit reached.");
        var streamId = Guid.NewGuid();
        var recipientHash = _hashCache.GetOrAdd(recipientId, BoltCodec.Fnv1aHash);
        var commandHash = GetCommandHash(commandName);
        var stream = new BoltStream(streamId, conn, RemoveActiveStream, _config.StreamInboundCapacity);
        if (!TryTrackStream(stream))
            throw new InvalidOperationException("Bolt client active stream limit reached.");
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteStreamOpen(writer, streamId, recipientHash, commandHash);
        try
        {
            await conn.SendReliableAsync(writer, ct);
            return stream;
        }
        catch
        {
            stream.MarkClosed(HttpStatusCode.ServiceUnavailable);
            throw;
        }
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
            BoltConnection? best = null;
            var bestPending = int.MaxValue;
            foreach (var connection in _connections)
            {
                if (!connection.IsAvailable)
                    continue;

                var pending = connection.PendingSends;
                if (pending < bestPending)
                {
                    best = connection;
                    bestPending = pending;
                }
            }

            return best ?? throw new InvalidOperationException("Not connected");
        }
    }

    private async Task ReceiveLoopAsync(BoltConnection conn, CancellationToken ct)
    {
        var receiveBufferBytes = Math.Min(_maxFrameBytes, Math.Max(1024, _config.ReceiveBufferBytes));
        var buffer = ArrayPool<byte>.Shared.Rent(receiveBufferBytes);
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

                await DispatchReceivedFrameAsync(conn, frameBytes, totalLength, ct);

                if (ReferenceEquals(frameBytes, largeBuffer))
                {
                    ArrayPool<byte>.Shared.Return(largeBuffer!);
                    largeBuffer = null;
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
            RetireStreamsForConnection(conn, HttpStatusCode.ServiceUnavailable);
            if (!_disposed)
            {
                conn.Retire(new IOException("Bolt receive loop ended before the connection was disposed."));
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
        finally
        {
            ArrayPool<byte>.Shared.Return(pooledBuf);
            _inboundDispatchSlots.Release();
        }
    }

    private async ValueTask DispatchReceivedFrameAsync(
        BoltConnection conn,
        byte[] frameBytes,
        int totalLength,
        CancellationToken ct)
    {
        var data = frameBytes.AsSpan(0, totalLength);
        var frameType = BoltCodec.PeekFrameType(data);
        switch (frameType)
        {
            case FrameType.Batch:
            {
                if (!BoltCodec.TryReadBatch(data, out var batch))
                    throw new InvalidDataException("Received a malformed Bolt batch frame.");

                var count = batch.Count;
                var offset = BoltCodec.BatchHeaderSize;
                for (var i = 0; i < count; i++)
                {
                    var frameLength = BinaryPrimitives.ReadInt32LittleEndian(frameBytes.AsSpan(offset));
                    offset += 4;
                    frameBytes.AsSpan(offset, frameLength).CopyTo(frameBytes);
                    offset += frameLength;
                    await DispatchReceivedFrameAsync(conn, frameBytes, frameLength, ct);
                }
                break;
            }
            case FrameType.Response:
                HandleIncomingResponse(data);
                break;
            case FrameType.Request:
            {
                if (!_inboundDispatchSlots.Wait(0))
                {
                    if (BoltCodec.TryReadRequest(data, out var rejected, out _))
                        await SendResponseAsync(conn, rejected.RequestId, rejected.SenderHash, HttpStatusCode.TooManyRequests, ReadOnlyMemory<byte>.Empty, ct);
                    break;
                }
                var reqBuf = ArrayPool<byte>.Shared.Rent(totalLength);
                data.CopyTo(reqBuf);
                _ = DispatchRequestPooledAsync(conn, reqBuf, totalLength, ct);
                break;
            }
            case FrameType.Push:
            {
                if (!_inboundDispatchSlots.Wait(0))
                    break;
                var pushBuf = ArrayPool<byte>.Shared.Rent(totalLength);
                data.CopyTo(pushBuf);
                _ = DispatchPushPooledAsync(pushBuf, totalLength, ct);
                break;
            }
            case FrameType.StreamOpen:
                HandleStreamOpen(conn, data);
                break;
            case FrameType.StreamData:
                HandleStreamData(data);
                break;
            case FrameType.StreamClose:
                HandleStreamClose(data);
                break;
            case FrameType.RequestCancel:
                HandleRequestCancel(data);
                break;
            default:
                if (_frameHandlers.TryGetValue((byte)frameType, out var handler))
                {
                    try { handler(conn, frameBytes, totalLength); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Bolt custom frame handler failed for frame type {FrameType}", frameType);
                    }
                }
                break;
        }
    }

    private async Task DispatchPushPooledAsync(byte[] pooledBuf, int length, CancellationToken ct)
    {
        try { await HandleIncomingPushAsync(pooledBuf, length, ct); }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooledBuf);
            _inboundDispatchSlots.Release();
        }
    }

    private void HandleIncomingResponse(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadResponse(data, out var frame, out _)) return;
        if (_pendingCalls.TryRemove(frame.RequestId, out var rpcCall))
        {
            ReadOnlyMemory<byte> payload;
            if (frame.PayloadLength > 0)
            {
                var response = GC.AllocateUninitializedArray<byte>(frame.PayloadLength);
                frame.GetPayload(data).CopyTo(response);
                payload = response;
            }
            else
            {
                payload = ReadOnlyMemory<byte>.Empty;
            }
            rpcCall.SetResult(new BoltRpcResponse { StatusCode = frame.StatusCode, Data = payload });
        }
    }

    private void HandleStreamOpen(BoltConnection conn, ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamOpen(data, out var streamId, out _, out var commandHash)) return;
        if (!_streamHandlers.TryGetValue(commandHash, out var handler))
        {
            _ = SendStreamCloseBestEffortAsync(conn, streamId, HttpStatusCode.NotImplemented);
            return;
        }

        LargeRpcInboundCollector? collector = commandHash switch
        {
            var hash when hash == LargeRpcCommandHash =>
                new LargeRpcInboundCollector(this, headerSize: 28, totalSizeOffset: 20, usePooledBuffer: true),
            var hash when hash == LargeRpcResponseStreamHash =>
                new LargeRpcInboundCollector(this, headerSize: 22, totalSizeOffset: 18, usePooledBuffer: false),
            _ => null
        };
        var stream = new BoltStream(streamId, conn, RemoveActiveStream, _config.StreamInboundCapacity);
        if (collector is not null &&
            (!_largeRpcCollectors.TryAdd(streamId, collector) || !stream.TrySetInboundSink(collector.Accept)))
        {
            _largeRpcCollectors.TryRemove(streamId, out _);
            collector.Dispose();
            _ = SendStreamCloseBestEffortAsync(conn, streamId, HttpStatusCode.ServiceUnavailable);
            return;
        }

        if (TryTrackStream(stream))
        {
            _ = Task.Run(async () =>
            {
                var statusCode = HttpStatusCode.OK;
                try
                {
                    await handler(stream);
                }
                catch (Exception ex)
                {
                    statusCode = HttpStatusCode.InternalServerError;
                    _logger.LogError(ex, "Stream handler error");
                }
                finally
                {
                    if (!stream.IsClosed)
                    {
                        try { await stream.CloseAsync(statusCode); }
                        catch { RemoveActiveStream(streamId); }
                    }
                    else
                    {
                        RemoveActiveStream(streamId);
                    }
                }
            });
        }
        else
        {
            if (_largeRpcCollectors.TryRemove(streamId, out var rejectedCollector))
                rejectedCollector.Dispose();
            _ = SendStreamCloseBestEffortAsync(conn, streamId, HttpStatusCode.TooManyRequests);
        }
    }

    private void HandleRequestCancel(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadRequestCancel(data, out var requestId))
            return;
        if (_inboundRequestCancellations.TryGetValue(requestId, out var cts))
            cts.Cancel();
        else
            TrackEarlyInboundCancellation(requestId);
    }

    private void HandleStreamData(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamData(data, out var streamId, out var payloadOffset, out var payloadLength, out _)) return;
        if (_activeStreams.TryGetValue(streamId, out var stream))
        {
            if (!stream.TryAcceptInbound(data.Slice(payloadOffset, payloadLength)))
                _ = SendStreamCloseBestEffortAsync(stream.Connection, streamId, HttpStatusCode.TooManyRequests);
        }
    }

    private void HandleStreamClose(ReadOnlySpan<byte> data)
    {
        if (!BoltCodec.TryReadStreamClose(data, out var streamId, out var statusCode)) return;
        if (_activeStreams.TryRemove(streamId, out var stream))
        {
            Interlocked.Decrement(ref _activeStreamCount);
            stream.MarkClosed(statusCode);
        }
    }

    private bool TryTrackStream(BoltStream stream)
    {
        if (Interlocked.Increment(ref _activeStreamCount) > Math.Max(1, _config.MaxActiveStreams))
        {
            Interlocked.Decrement(ref _activeStreamCount);
            return false;
        }

        if (_activeStreams.TryAdd(stream.StreamId, stream))
            return true;

        Interlocked.Decrement(ref _activeStreamCount);
        return false;
    }

    private void RemoveActiveStream(Guid streamId)
    {
        if (_activeStreams.TryRemove(streamId, out _))
            Interlocked.Decrement(ref _activeStreamCount);
    }

    private async Task SendStreamCloseBestEffortAsync(
        BoltConnection connection,
        Guid streamId,
        HttpStatusCode statusCode)
    {
        try
        {
            using var cts = new CancellationTokenSource(_rpcTimeout);
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteStreamClose(writer, streamId, statusCode);
            await connection.SendReliableAsync(writer, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to close rejected Bolt stream {StreamId}", streamId);
        }
    }

    private void RetireStreamsForConnection(BoltConnection connection, HttpStatusCode statusCode)
    {
        foreach (var (streamId, stream) in _activeStreams)
        {
            if (!ReferenceEquals(stream.Connection, connection) ||
                !_activeStreams.TryRemove(
                    new KeyValuePair<Guid, BoltStream>(streamId, stream)))
            {
                continue;
            }

            Interlocked.Decrement(ref _activeStreamCount);
            stream.MarkClosed(statusCode);
        }
    }

    private void TrackEarlyInboundCancellation(Guid requestId)
    {
        var now = Environment.TickCount64;
        var expiresBefore = now - Math.Max(1L, (long)_rpcTimeout.TotalMilliseconds);
        foreach (var entry in _earlyInboundCancellations)
        {
            if (entry.Value <= expiresBefore)
                _earlyInboundCancellations.TryRemove(entry);
        }

        var capacity = Math.Max(
            1,
            Math.Max(1, _config.MaxConcurrentInboundHandlers) +
            Math.Max(1, _config.MaxActiveStreams));
        if (_earlyInboundCancellations.Count < capacity)
            _earlyInboundCancellations.TryAdd(requestId, now);
    }

    private async Task HandleIncomingPushAsync(byte[] data, int length, CancellationToken ct)
    {
        // Push uses same frame layout as Request — parse with TryReadRequest
        var span = data.AsSpan(0, length);
        if (!BoltCodec.TryReadRequest(span, out var frame, out _)) return;

        if (_handlers.TryGetValue(frame.CommandHash, out var handler))
        {
            try
            {
                var payload = frame.GetPayload(data.AsMemory(0, length));
                await handler(
                    payload,
                    new BoltInboundRequestContext(frame.RequestId, frame.SenderHash),
                    ct);
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

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestCts.CancelAfter(_rpcTimeout);
        if (!_inboundRequestCancellations.TryAdd(frame.RequestId, requestCts))
            return;
        if (_earlyInboundCancellations.TryRemove(frame.RequestId, out _))
            requestCts.Cancel();

        try
        {
            if (_handlers.TryGetValue(frame.CommandHash, out var handler))
            {
                var payload = frame.GetPayload(data.AsMemory(0, length));
                var (statusCode, responsePayload) = await handler(
                    payload,
                    new BoltInboundRequestContext(frame.RequestId, frame.SenderHash),
                    requestCts.Token);
                await SendResponseAsync(conn, frame.RequestId, frame.SenderHash, statusCode, responsePayload, requestCts.Token);
            }
            else
            {
                await SendResponseAsync(conn, frame.RequestId, frame.SenderHash, HttpStatusCode.NotImplemented, ReadOnlyMemory<byte>.Empty, requestCts.Token);
            }
        }
        catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
        {
            // The caller no longer needs a response.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler error for command hash {CommandHash}", frame.CommandHash);
            await SendResponseAsync(conn, frame.RequestId, frame.SenderHash, HttpStatusCode.InternalServerError, ReadOnlyMemory<byte>.Empty, ct);
        }
        finally
        {
            _inboundRequestCancellations.TryRemove(
                new KeyValuePair<Guid, CancellationTokenSource>(frame.RequestId, requestCts));
            _earlyInboundCancellations.TryRemove(frame.RequestId, out _);
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
        if (responsePayload.Length <= _largePayloadThreshold)
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(writer, requestId, statusCode, responsePayload.Span);
            await conn.SendReliableAsync(writer, ct);
        }
        else
        {
            // Large response: BoltStream back to caller — same mechanism as request path
            var respStream = new BoltStream(
                Guid.NewGuid(),
                conn,
                RemoveActiveStream,
                _config.StreamInboundCapacity);
            if (!TryTrackStream(respStream))
            {
                var rejectedWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteResponse(
                    rejectedWriter,
                    requestId,
                    HttpStatusCode.TooManyRequests,
                    ReadOnlySpan<byte>.Empty);
                await conn.SendReliableAsync(rejectedWriter, ct);
                return;
            }

            var openWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteStreamOpen(openWriter, respStream.StreamId, callerSenderHash, LargeRpcResponseStreamHash);
            await conn.SendReliableAsync(openWriter, ct);

            // Header: [16:requestId][2:statusCode][4:totalSize]
            var header = new byte[22];
            requestId.TryWriteBytes(header);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(16), (short)statusCode);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18), responsePayload.Length);
            await respStream.SendAsync((ReadOnlyMemory<byte>)header, ct);

            await SendLargePayloadPipelinedAsync(respStream, responsePayload, ct);
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
                    var actorAccessToken = await ResolveActorAccessTokenAsync(sub.ActorAccessTokenProvider, CancellationToken.None);
                    var w = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteSubscribe(w, sub.Topic, _clientId, durable: false, actorAccessToken);
                    await GetPrimaryConnection().SendReliableAsync(w, CancellationToken.None);
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
                    var actorAccessToken = await ResolveActorAccessTokenAsync(sub.ActorAccessTokenProvider, CancellationToken.None);
                    var w = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteSubscribe(w, sub.Topic, sub.SubscriberId, durable: true, actorAccessToken);
                    await GetPrimaryConnection().SendReliableAsync(w, CancellationToken.None);
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
        foreach (var (requestId, _) in _pendingCalls)
        {
            if (_pendingCalls.TryRemove(requestId, out var call))
                call.SetException(new ObjectDisposedException(nameof(BoltClient)));
        }
        foreach (var cts in _inboundRequestCancellations.Values)
            cts.Cancel();
        var connections = ClearConnections();
        foreach (var connection in connections)
            RetireStreamsForConnection(connection, HttpStatusCode.ServiceUnavailable);
        _earlyInboundCancellations.Clear();
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
        connection.FailureObserver = failure => HandleConnectionFailure(connection, failure);

    private void HandleConnectionFailure(BoltConnection connection, BoltConnectionFailureKind failure)
    {
        RecordConnectionFailure(failure);
        if (failure is not (BoltConnectionFailureKind.SendFailure or BoltConnectionFailureKind.SendTimeout))
            return;

        var noConnections = RemoveConnection(connection, out var removed);
        if (!removed)
            return;

        RetireStreamsForConnection(connection, HttpStatusCode.ServiceUnavailable);
        connection.CompleteSendChannel();
        connection.ReceiveCts?.Cancel();
        _ = Task.Run(async () =>
        {
            try { await connection.Transport.CloseAsync(); } catch { }
        });

        if (!_disposed && _isRegistered && noConnections)
        {
            _isRegistered = false;
            RaiseLifecycleEvent(Disconnected);
            foreach (var (id, _) in _pendingCalls)
            {
                if (_pendingCalls.TryRemove(id, out var call))
                    call.SetException(new IOException("All Bolt connections failed while sending."));
            }
            _ = Task.Run(() => ReconnectAsync());
        }
    }

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

    private sealed class LargeRpcInboundCollector : IDisposable
    {
        private readonly BoltClient _owner;
        private readonly int _totalSizeOffset;
        private readonly bool _usePooledBuffer;
        private readonly byte[] _header;
        private readonly TaskCompletionSource _headerReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private byte[]? _buffer;
        private int _reservationSize;
        private bool _disposed;

        public bool HasHeader { get; private set; }
        public bool IsMalformed { get; private set; }
        public int TotalSize { get; private set; }
        public int BytesRead { get; private set; }
        public HttpStatusCode? RejectionStatus { get; private set; }
        public int ReservationSize => _reservationSize;
        public ReadOnlySpan<byte> HeaderSpan => _header;
        public bool HeaderProcessed => _headerReceived.Task.IsCompleted;
        public Task HeaderReceived => _headerReceived.Task;

        public LargeRpcInboundCollector(
            BoltClient owner,
            int headerSize,
            int totalSizeOffset,
            bool usePooledBuffer)
        {
            _owner = owner;
            _totalSizeOffset = totalSizeOffset;
            _usePooledBuffer = usePooledBuffer;
            _header = GC.AllocateUninitializedArray<byte>(headerSize);
        }

        public void Accept(ReadOnlySpan<byte> data)
        {
            if (_disposed || IsMalformed || RejectionStatus is not null)
                return;

            if (!HasHeader)
            {
                if (data.Length < _header.Length)
                {
                    IsMalformed = true;
                    _headerReceived.TrySetResult();
                    return;
                }

                data[.._header.Length].CopyTo(_header);
                HasHeader = true;
                TotalSize = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(_totalSizeOffset));
                if (TotalSize < 0 || TotalSize > _owner._maxLargeRpcPayloadBytes)
                {
                    RejectionStatus = HttpStatusCode.RequestEntityTooLarge;
                    _headerReceived.TrySetResult();
                    return;
                }

                var reservationSize = _usePooledBuffer
                    ? GetPooledBufferReservationSize(TotalSize)
                    : TotalSize;
                if (!_owner.TryReserveLargeRpcBuffer(reservationSize))
                {
                    RejectionStatus = HttpStatusCode.TooManyRequests;
                    _headerReceived.TrySetResult();
                    return;
                }

                _reservationSize = reservationSize;
                _buffer = TotalSize == 0
                    ? Array.Empty<byte>()
                    : _usePooledBuffer
                        ? ArrayPool<byte>.Shared.Rent(TotalSize)
                        : GC.AllocateUninitializedArray<byte>(TotalSize);

                if (_usePooledBuffer && _buffer.Length > reservationSize)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                    _buffer = null;
                    _owner.ReleaseLargeRpcBuffer(_reservationSize);
                    _reservationSize = 0;
                    RejectionStatus = HttpStatusCode.InternalServerError;
                }

                _headerReceived.TrySetResult();
                return;
            }

            if (_buffer is null || data.Length > TotalSize - BytesRead)
            {
                IsMalformed = true;
                return;
            }

            data.CopyTo(_buffer.AsSpan(BytesRead));
            BytesRead += data.Length;
        }

        public byte[] DetachBuffer(bool transferReservation = false)
        {
            var buffer = _buffer ?? throw new InvalidOperationException("Large RPC collector has no completed buffer.");
            _buffer = null;
            if (transferReservation)
                _reservationSize = 0;
            return buffer;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_usePooledBuffer && _buffer is { Length: > 0 } buffer)
                ArrayPool<byte>.Shared.Return(buffer);
            _buffer = null;
            if (_reservationSize > 0)
                _owner.ReleaseLargeRpcBuffer(_reservationSize);
            _reservationSize = 0;
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
    private const int BatchHeaderSize = BoltCodec.BatchHeaderSize;

    private readonly record struct PendingSend(
        byte[] Buffer,
        int Length,
        PooledSendCompletion? TransportCompletion);

    public IBoltConnection Transport { get; }
    public BoltTransport TransportType { get; }
    private readonly Channel<PendingSend> _sendChannel;
    private readonly TimeSpan _sendEnqueueTimeout;
    private readonly bool _enableBatching;
    private int _pendingSends;
    private int _activeSends;
    private long _activeSendStartedAt;
    private int _isClosing;
    private Exception? _sendFailure;

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
    internal bool IsAvailable => Volatile.Read(ref _isClosing) == 0 && Transport.IsConnected;
    internal Exception? SendFailure => Volatile.Read(ref _sendFailure);
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

    public BoltConnection(
        IBoltConnection transport,
        int sendQueueCapacity = 4096,
        int sendEnqueueTimeoutMs = 0,
        bool enableBatching = true)
    {
        Transport = transport;
        TransportType = transport.TransportType;
        _enableBatching = enableBatching;
        _sendEnqueueTimeout = sendEnqueueTimeoutMs > 0
            ? TimeSpan.FromMilliseconds(sendEnqueueTimeoutMs)
            : TimeSpan.Zero;
        _sendChannel = Channel.CreateBounded<PendingSend>(
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
        if (SendLoop is not null)
            throw new InvalidOperationException("The Bolt client send loop has already been started.");

        SendLoop = Task.Run(async () =>
        {
            Exception? terminalFailure = null;
            var staged = ArrayPool<PendingSend>.Shared.Rent(BoltCodec.MaxBatchFrames);
            using var sendDeadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                await foreach (var first in _sendChannel.Reader.ReadAllAsync(ct))
                {
                    var stagedCount = 1;
                    staged[0] = first;
                    var batchSize = BatchHeaderSize + 4 + first.Length;
                    byte[]? physicalBuffer = null;
                    var physicalLength = first.Length;
                    Task? transportSend = null;
                    var sendStarted = false;
                    var sendSucceeded = false;
                    try
                    {
                        if (!Transport.IsConnected)
                            throw new IOException("Bolt transport disconnected before a queued send completed.");

                        if (_enableBatching && IsBatchable(first) && batchSize <= BoltCodec.MaxBatchBytes)
                        {
                            while (stagedCount < BoltCodec.MaxBatchFrames &&
                                   _sendChannel.Reader.TryPeek(out var next))
                            {
                                if (!IsBatchable(next))
                                    break;

                                var nextSize = 4L + next.Length;
                                if (batchSize + nextSize > BoltCodec.MaxBatchBytes)
                                    break;

                                if (!_sendChannel.Reader.TryRead(out next))
                                    continue;

                                staged[stagedCount++] = next;
                                batchSize += (int)nextSize;
                            }
                        }

                        if (stagedCount > 1)
                        {
                            (physicalBuffer, physicalLength) = EncodeBatch(staged, stagedCount, batchSize);
                        }
                        else
                        {
                            physicalBuffer = first.Buffer;
                        }

                        Interlocked.Exchange(ref _activeSendStartedAt, Environment.TickCount64);
                        Interlocked.Increment(ref _activeSends);
                        sendStarted = true;
                        if (_sendEnqueueTimeout > TimeSpan.Zero)
                            sendDeadlineCts.CancelAfter(_sendEnqueueTimeout);

                        var sendValue = Transport.SendAsync(
                            physicalBuffer.AsMemory(0, physicalLength),
                            sendDeadlineCts.Token);
                        if (sendValue.IsCompletedSuccessfully)
                        {
                            sendValue.GetAwaiter().GetResult();
                        }
                        else
                        {
                            transportSend = sendValue.AsTask();
                            await transportSend.WaitAsync(sendDeadlineCts.Token);
                        }

                        if (_sendEnqueueTimeout > TimeSpan.Zero && !sendDeadlineCts.TryReset())
                            throw new OperationCanceledException(sendDeadlineCts.Token);

                        sendSucceeded = true;
                    }
                    catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
                    {
                        var timeout = new TimeoutException(
                            $"Bolt transport send timed out after {_sendEnqueueTimeout.TotalMilliseconds:0} ms.",
                            ex);
                        CompleteStaged(staged, stagedCount, completion => completion.SetException(timeout));
                        terminalFailure = timeout;
                        BeginClose(timeout);
                        FailureObserver?.Invoke(BoltConnectionFailureKind.SendTimeout);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        CompleteStaged(staged, stagedCount, completion => completion.SetCanceled(ct));
                        break;
                    }
                    catch (Exception ex)
                    {
                        CompleteStaged(staged, stagedCount, completion => completion.SetException(ex));
                        terminalFailure = ex;
                        BeginClose(ex);
                        FailureObserver?.Invoke(BoltConnectionFailureKind.SendFailure);
                        break;
                    }
                    finally
                    {
                        if (sendStarted && Interlocked.Decrement(ref _activeSends) <= 0)
                            Interlocked.Exchange(ref _activeSendStartedAt, 0);

                        ReleaseStagedBuffers(
                            staged,
                            stagedCount,
                            physicalBuffer,
                            transportSend);

                        if (sendSucceeded)
                            CompleteStaged(staged, stagedCount, static completion => completion.SetResult());

                        Array.Clear(staged, 0, stagedCount);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                terminalFailure ??= ex;
                BeginClose(ex);
            }
            finally
            {
                while (_sendChannel.Reader.TryRead(out var pending))
                {
                    if (terminalFailure is not null)
                        pending.TransportCompletion?.SetException(terminalFailure);
                    else
                        pending.TransportCompletion?.SetCanceled(ct);
                    ReleasePendingSend(pending);
                }

                ArrayPool<PendingSend>.Shared.Return(staged, clearArray: true);
            }
        });
    }

    private static bool IsBatchable(PendingSend pending)
    {
        if (pending.Length <= 0)
            return false;

        return BoltCodec.IsValidBatchInnerFrame(pending.Buffer.AsSpan(0, pending.Length));
    }

    private static (byte[] Buffer, int Length) EncodeBatch(
        PendingSend[] staged,
        int count,
        int totalSize)
    {
        using var writer = new RentedBufferWriter(totalSize);
        var span = writer.GetSpan(totalSize);
        span[0] = (byte)FrameType.Batch;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), count);

        var offset = BatchHeaderSize;
        for (var index = 0; index < count; index++)
        {
            var pending = staged[index];
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), pending.Length);
            offset += 4;
            pending.Buffer.AsSpan(0, pending.Length).CopyTo(span[offset..]);
            offset += pending.Length;
        }

        writer.Advance(totalSize);
        return writer.Detach();
    }

    private static void CompleteStaged(
        PendingSend[] staged,
        int count,
        Action<PooledSendCompletion> complete)
    {
        for (var index = 0; index < count; index++)
        {
            if (staged[index].TransportCompletion is { } completion)
                complete(completion);
        }
    }

    private void ReleaseStagedBuffers(
        PendingSend[] staged,
        int count,
        byte[]? physicalBuffer,
        Task? transportSend)
    {
        var deferredBuffer = count == 1 && ReferenceEquals(physicalBuffer, staged[0].Buffer)
            ? staged[0].Buffer
            : null;

        for (var index = 0; index < count; index++)
        {
            var pending = staged[index];
            if (!ReferenceEquals(pending.Buffer, deferredBuffer))
                ReleasePendingSend(pending);
        }

        if (physicalBuffer is null || !ReferenceEquals(physicalBuffer, deferredBuffer))
        {
            if (physicalBuffer is not null)
            {
                if (transportSend is { IsCompleted: false })
                    _ = ReleaseWhenTransportCompletesAsync(transportSend, physicalBuffer);
                else
                    ArrayPool<byte>.Shared.Return(physicalBuffer);
            }

            return;
        }

        if (transportSend is { IsCompleted: false })
            _ = ReleaseWhenTransportCompletesAsync(transportSend, staged[0]);
        else
            ReleasePendingSend(staged[0]);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        ThrowIfUnavailable();
        return EnqueueAsync(data, transportCompletion: null, ct);
    }

    internal ValueTask SendAsync(RentedBufferWriter writer, CancellationToken ct)
    {
        ThrowIfUnavailable();
        ct.ThrowIfCancellationRequested();
        var (buffer, length) = writer.Detach();
        return EnqueueOwnedAsync(buffer, length, transportCompletion: null, ct);
    }

    internal async ValueTask SendReliableAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        ThrowIfUnavailable();
        var completion = PooledSendCompletion.Rent();
        try
        {
            await EnqueueAsync(data, completion, ct);
        }
        catch
        {
            completion.ReturnUnused();
            throw;
        }

        await completion.WaitAsync(ct);
    }

    internal async ValueTask SendReliableAsync(RentedBufferWriter writer, CancellationToken ct)
    {
        var completion = await EnqueueReliableAsync(writer, ct);
        await completion.WaitAsync(ct);
    }

    internal async ValueTask<PooledSendCompletion> EnqueueReliableAsync(
        RentedBufferWriter writer,
        CancellationToken ct)
    {
        ThrowIfUnavailable();
        ct.ThrowIfCancellationRequested();
        var (buffer, length) = writer.Detach();
        var completion = PooledSendCompletion.Rent();
        try
        {
            await EnqueueOwnedAsync(buffer, length, completion, ct);
        }
        catch
        {
            completion.ReturnUnused();
            throw;
        }

        return completion;
    }

    private ValueTask EnqueueAsync(
        ReadOnlyMemory<byte> data,
        PooledSendCompletion? transportCompletion,
        CancellationToken ct)
    {
        // Snapshot into a pooled buffer — the caller's buffer (thread-local RentedBufferWriter)
        // may be reused before the async transport write completes.
        var len = data.Length;
        var buf = ArrayPool<byte>.Shared.Rent(len);
        try
        {
            data.Span.CopyTo(buf);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buf);
            throw;
        }

        return EnqueueOwnedAsync(buf, len, transportCompletion, ct);
    }

    private ValueTask EnqueueOwnedAsync(
        byte[] buf,
        int len,
        PooledSendCompletion? transportCompletion,
        CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buf);
            throw;
        }

        Interlocked.Increment(ref _pendingSends);
        var pending = new PendingSend(buf, len, transportCompletion);

        // All sends go through Channel (serialized single-writer)
        if (_sendChannel.Writer.TryWrite(pending))
            return ValueTask.CompletedTask;
        return SendSlowAsync(pending, ct);
    }

    private async ValueTask SendSlowAsync(PendingSend pending, CancellationToken ct)
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

            await _sendChannel.Writer.WriteAsync(pending, enqueueToken);
        }
        catch (OperationCanceledException) when (
            !ct.IsCancellationRequested &&
            timeoutCts is { IsCancellationRequested: true })
        {
            FailureObserver?.Invoke(BoltConnectionFailureKind.EnqueueTimeout);
            ReleasePendingSend(pending);
            throw;
        }
        catch (OperationCanceledException)
        {
            ReleasePendingSend(pending);
            throw;
        }
        catch
        {
            FailureObserver?.Invoke(BoltConnectionFailureKind.EnqueueFailure);
            ReleasePendingSend(pending);
            throw;
        }
        finally
        {
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
        }
    }

    /// <summary>Signal that no more sends will be enqueued. The send loop will drain and exit.</summary>
    public void CompleteSendChannel()
    {
        Interlocked.Exchange(ref _isClosing, 1);
        _sendChannel.Writer.TryComplete(_sendFailure);
    }

    private void BeginClose(Exception failure)
    {
        if (Interlocked.Exchange(ref _isClosing, 1) == 0)
            Volatile.Write(ref _sendFailure, failure);
        _sendChannel.Writer.TryComplete(SendFailure ?? failure);
    }

    internal void Retire(Exception failure) => BeginClose(failure);

    private void ThrowIfUnavailable()
    {
        if (IsAvailable)
            return;

        throw new InvalidOperationException("Bolt connection is not available for sending.", SendFailure);
    }

    private void ReleasePendingSend(PendingSend pending)
    {
        ArrayPool<byte>.Shared.Return(pending.Buffer);
        Interlocked.Decrement(ref _pendingSends);
    }

    private async Task ReleaseWhenTransportCompletesAsync(Task transportSend, PendingSend pending)
    {
        try { await transportSend; }
        catch { }
        finally { ReleasePendingSend(pending); }
    }

    private static async Task ReleaseWhenTransportCompletesAsync(Task transportSend, byte[] buffer)
    {
        try { await transportSend; }
        catch { }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

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
