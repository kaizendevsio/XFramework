using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Shared.Buffers;
using StreamFlow.Domain.Shared.Protocol;
using XFramework.Domain.Shared.Configurations;
using XFramework.Integration.Services;

namespace XFramework.Integration.ThinProtocol;

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
public sealed class ThinStreamFlowClient : IAsyncDisposable
{
    private readonly Uri _serverUri;
    private readonly string _clientId;
    private readonly string _clientName;
    private readonly StreamFlowConfiguration _config;
    private readonly ILogger _logger;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveLoop;
    private volatile bool _isRegistered;
    private volatile bool _disposed;

    // Pending RPC calls — response frames resolve these
    private readonly ConcurrentDictionary<Guid, PooledRpcCallThin> _pendingCalls = new();

    // Handler registry — maps command hash to handler delegate
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _handlers = new();

    // Hash cache — computed once per unique string, reused every call
    private readonly ConcurrentDictionary<string, int> _hashCache = new();

    // Offline queue
    private readonly ConcurrentQueue<byte[]> _offlineQueue = new();

    // Send lock — WebSocket only supports one concurrent send
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Cached timeout for RPC calls — avoids CancellationTokenSource.CreateLinkedTokenSource per call
    private TimeSpan _rpcTimeout;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open && _isRegistered;

    public ThinStreamFlowClient(Uri serverUri, string clientId, string clientName, StreamFlowConfiguration config, ILogger logger)
    {
        _serverUri = serverUri;
        _clientId = clientId;
        _clientName = clientName;
        _config = config;
        _logger = logger;
        _rpcTimeout = TimeSpan.FromSeconds(config.RpcTimeoutSeconds > 0 ? config.RpcTimeoutSeconds : 30);
    }

    /// <summary>
    /// Connect to the thin StreamFlow server and register.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(_serverUri, ct);

        // Send registration frame
        var writer = new ArrayBufferWriter<byte>(128);
        StreamFlowCodec.WriteRegister(writer, _clientId, _clientName);
        await SendRawAsync(writer.WrittenMemory, ct);

        // Wait for ack
        var ackBuffer = new byte[2];
        var result = await _webSocket.ReceiveAsync(ackBuffer, ct);
        if (result.Count >= 2 && (FrameType)ackBuffer[0] == FrameType.RegisterAck && ackBuffer[1] == 1)
        {
            _isRegistered = true;
            _logger.LogInformation("Thin client registered: {ClientId} ({ClientName})", _clientId, _clientName);
        }
        else
        {
            throw new InvalidOperationException("Server rejected registration");
        }

        // Start receive loop
        _receiveCts = new CancellationTokenSource();
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

        // Drain offline queue
        await DrainOfflineQueueAsync(ct);
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

                // Reset socket for retry
                _webSocket?.Dispose();
                _webSocket = null;
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

        // Cached hash lookups — computed once per unique string, O(1) thereafter
        var recipientHash = _hashCache.GetOrAdd(recipientId, StreamFlowCodec.Fnv1aHash);
        var commandHash = _hashCache.GetOrAdd(commandName, StreamFlowCodec.Fnv1aHash);

        var rpcCall = PooledRpcCallThin.Rent();
        _pendingCalls[requestId] = rpcCall;

        try
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            StreamFlowCodec.WriteRequest(writer, requestId, recipientHash, commandHash, payload.Span);

            if (!IsConnected)
            {
                _offlineQueue.Enqueue(writer.WrittenSpan.ToArray());
                rpcCall.SetException(new InvalidOperationException("Not connected"));
            }
            else
            {
                await SendRawAsync(writer.WrittenMemory, ct);
            }

            // Simple CTS timeout — avoids CreateLinkedTokenSource allocation
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
    /// Register a handler for incoming request frames (this client is the recipient).
    /// </summary>
    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = StreamFlowCodec.Fnv1aHash(commandName);
        _handlers[hash] = handler;
        _logger.LogDebug("Registered thin handler for {CommandName} [hash={Hash}]", commandName, hash);
    }

    /// <summary>
    /// Background receive loop — reads frames from the WebSocket and dispatches them.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer.AsMemory(), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType != WebSocketMessageType.Binary || result.Count == 0)
                    continue;

                var data = buffer.AsSpan(0, result.Count);
                var frameType = StreamFlowCodec.PeekFrameType(data);

                switch (frameType)
                {
                    case FrameType.Response:
                        HandleIncomingResponse(data);
                        break;

                    case FrameType.Request:
                        await HandleIncomingRequestAsync(buffer, result.Count, ct);
                        break;

                    default:
                        _logger.LogDebug("Received unexpected frame type {FrameType}", frameType);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning("WebSocket receive error: {Error}", ex.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _isRegistered = false;

            // Cancel all pending RPCs
            foreach (var (id, rpc) in _pendingCalls)
            {
                if (_pendingCalls.TryRemove(id, out var call))
                    call.SetException(new InvalidOperationException("Connection lost"));
            }

            // Attempt reconnection if not disposed
            if (!_disposed)
                _ = Task.Run(() => ReconnectAsync());
        }
    }

    private void HandleIncomingResponse(ReadOnlySpan<byte> data)
    {
        if (!StreamFlowCodec.TryReadResponse(data, out var frame, out _))
            return;

        if (_pendingCalls.TryRemove(frame.RequestId, out var rpcCall))
        {
            // Copy payload here since the receive buffer will be reused
            var payload = frame.PayloadLength > 0
                ? frame.GetPayload(data).ToArray()
                : Array.Empty<byte>();
            rpcCall.SetResult(new ThinRpcResponse { StatusCode = frame.StatusCode, Data = payload });
        }
    }

    private async Task HandleIncomingRequestAsync(byte[] data, int length, CancellationToken ct)
    {
        var span = data.AsSpan(0, length);
        if (!StreamFlowCodec.TryReadRequest(span, out var frame, out _))
            return;

        if (_handlers.TryGetValue(frame.CommandHash, out var handler))
        {
            try
            {
                // Zero-copy: pass payload slice from the original buffer
                var payload = frame.GetPayload(data.AsMemory(0, length));
                var (statusCode, responsePayload) = await handler(payload, frame.RequestId);

                var writer = RentedBufferWriter.GetThreadLocal();
                StreamFlowCodec.WriteResponse(writer, frame.RequestId, statusCode, responsePayload.Span);
                await SendRawAsync(writer.WrittenMemory, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler error for command hash {CommandHash}", frame.CommandHash);
                var errWriter = RentedBufferWriter.GetThreadLocal();
                StreamFlowCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.InternalServerError, ReadOnlySpan<byte>.Empty);
                await SendRawAsync(errWriter.WrittenMemory, ct);
            }
        }
        else
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            StreamFlowCodec.WriteResponse(writer, frame.RequestId, HttpStatusCode.NotImplemented, ReadOnlySpan<byte>.Empty);
            await SendRawAsync(writer.WrittenMemory, ct);
        }
    }

    private ValueTask SendRawAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        // Fast path: if lock is available, send synchronously
        if (_sendLock.Wait(0))
        {
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                    return _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
                return ValueTask.CompletedTask;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // Slow path: contention — await the lock
        return SendRawSlowAsync(data, ct);
    }

    private async ValueTask SendRawSlowAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            if (_webSocket?.State == WebSocketState.Open)
                await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task DrainOfflineQueueAsync(CancellationToken ct)
    {
        var drained = 0;
        while (_offlineQueue.TryDequeue(out var frame))
        {
            await SendRawAsync(frame, ct);
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
        _receiveCts?.Cancel();

        if (_receiveLoop is not null)
        {
            try { await _receiveLoop; } catch { }
        }

        if (_webSocket is not null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
            _webSocket.Dispose();
        }

        _receiveCts?.Dispose();
        _sendLock.Dispose();
    }
}

/// <summary>
/// Response data from a thin protocol RPC call.
/// </summary>
public struct ThinRpcResponse
{
    public HttpStatusCode StatusCode;
    public ReadOnlyMemory<byte> Data;
}
