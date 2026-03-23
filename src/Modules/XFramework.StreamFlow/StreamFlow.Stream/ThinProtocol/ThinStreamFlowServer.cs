using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using StreamFlow.Domain.Shared.Protocol;

namespace StreamFlow.Stream.ThinProtocol;

/// <summary>
/// Thin binary WebSocket server that replaces SignalR hub.
/// Accepts raw WebSocket connections, handles registration,
/// routes Request frames to recipients, routes Response frames back to callers.
///
/// Zero SignalR overhead — frames go directly: binary WebSocket ↔ MemoryPack.
/// </summary>
public sealed class ThinStreamFlowServer
{
    private readonly ILogger<ThinStreamFlowServer> _logger;
    private readonly ConcurrentDictionary<string, ThinConnection> _connectionsByStreamId = new();
    private readonly ConcurrentDictionary<int, ConcurrentBag<ThinConnection>> _connectionsByServiceHash = new();
    private readonly ConcurrentDictionary<Guid, (ThinConnection Caller, long Timestamp)> _pendingInvocations = new();
    private readonly ConcurrentDictionary<int, int> _roundRobinIndex = new();

    private readonly Timer _cleanupTimer;
    private const int InvocationTimeoutMs = 30_000;

    public ThinStreamFlowServer(ILogger<ThinStreamFlowServer> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupStaleInvocations, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
    {
        var connection = new ThinConnection(webSocket);
        var receiveBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        try
        {
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(receiveBuffer.AsMemory(), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType != WebSocketMessageType.Binary || result.Count == 0)
                    continue;

                await ProcessFrameAsync(connection, receiveBuffer, result.Count, ct);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("Client {ClientId} disconnected", connection.ClientId ?? "unregistered");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection for client {ClientId}", connection.ClientId ?? "unregistered");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(receiveBuffer);
            RemoveConnection(connection);

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
                catch { }
            }
        }
    }

    private async Task ProcessFrameAsync(ThinConnection connection, byte[] buffer, int length, CancellationToken ct)
    {
        var frameType = (FrameType)buffer[0];

        switch (frameType)
        {
            case FrameType.Register:
                await HandleRegisterAsync(connection, buffer, length, ct);
                break;
            case FrameType.Request:
                await HandleRequestAsync(connection, buffer, length, ct);
                break;
            case FrameType.Response:
                await HandleResponseAsync(connection, buffer, length, ct);
                break;
            case FrameType.Push:
                await HandlePushAsync(connection, buffer, length, ct);
                break;
            default:
                _logger.LogWarning("Unknown frame type {FrameType} from {ClientId}", frameType, connection.ClientId);
                break;
        }
    }

    private async Task HandleRegisterAsync(ThinConnection connection, byte[] buffer, int length, CancellationToken ct)
    {
        if (!StreamFlowCodec.TryReadRegister(buffer.AsSpan(0, length), out var clientId, out var clientName, out _))
        {
            _logger.LogWarning("Invalid register frame");
            return;
        }

        connection.ClientId = clientId;
        connection.ClientName = clientName;
        connection.ServiceHash = StreamFlowCodec.Fnv1aHash(clientId);

        _connectionsByStreamId[connection.StreamId] = connection;
        _connectionsByServiceHash.AddOrUpdate(
            connection.ServiceHash,
            _ => new ConcurrentBag<ThinConnection> { connection },
            (_, bag) => { bag.Add(connection); return bag; });

        _logger.LogInformation("Client registered: {ClientId} ({ClientName}) [hash={ServiceHash}]",
            clientId, clientName, connection.ServiceHash);

        var writer = new ArrayBufferWriter<byte>(2);
        StreamFlowCodec.WriteRegisterAck(writer, true);
        await connection.SendAsync(writer.WrittenMemory, ct);
    }

    private async Task HandleRequestAsync(ThinConnection caller, byte[] buffer, int length, CancellationToken ct)
    {
        if (!StreamFlowCodec.TryReadRequest(buffer.AsSpan(0, length), out var frame, out var consumed))
        {
            _logger.LogWarning("Invalid request frame from {ClientId}", caller.ClientId);
            return;
        }

        _pendingInvocations[frame.RequestId] = (caller, Environment.TickCount64);

        var recipient = GetRecipient(frame.RecipientHash);
        if (recipient is null)
        {
            _logger.LogWarning("No recipient for hash {RecipientHash}, requestId={RequestId}", frame.RecipientHash, frame.RequestId);
            var errWriter = new ArrayBufferWriter<byte>(StreamFlowCodec.ResponseHeaderSize);
            StreamFlowCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.NotFound, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter.WrittenMemory, ct);
            _pendingInvocations.TryRemove(frame.RequestId, out _);
            return;
        }

        // Forward the raw frame bytes to recipient — zero intermediate processing
        await recipient.SendAsync(buffer.AsMemory(0, consumed), ct);
    }

    private async Task HandleResponseAsync(ThinConnection responder, byte[] buffer, int length, CancellationToken ct)
    {
        if (!StreamFlowCodec.TryReadResponse(buffer.AsSpan(0, length), out var frame, out var consumed))
        {
            _logger.LogWarning("Invalid response frame from {ClientId}", responder.ClientId);
            return;
        }

        if (_pendingInvocations.TryRemove(frame.RequestId, out var pending))
        {
            await pending.Caller.SendAsync(buffer.AsMemory(0, consumed), ct);
        }
        else
        {
            _logger.LogWarning("No pending invocation for response requestId={RequestId}", frame.RequestId);
        }
    }

    private async Task HandlePushAsync(ThinConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (!StreamFlowCodec.TryReadRequest(buffer.AsSpan(0, length), out var frame, out var consumed))
        {
            _logger.LogWarning("Invalid push frame from {ClientId}", sender.ClientId);
            return;
        }

        var recipient = GetRecipient(frame.RecipientHash);
        if (recipient is null)
        {
            _logger.LogWarning("No recipient for push, hash={RecipientHash}", frame.RecipientHash);
            return;
        }

        await recipient.SendAsync(buffer.AsMemory(0, consumed), ct);
    }

    private ThinConnection? GetRecipient(int serviceHash)
    {
        if (!_connectionsByServiceHash.TryGetValue(serviceHash, out var bag))
            return null;

        var clients = bag.Where(c => c.IsAlive).ToList();
        if (clients.Count == 0) return null;
        if (clients.Count == 1) return clients[0];

        var idx = _roundRobinIndex.AddOrUpdate(serviceHash, 0, (_, prev) => prev + 1);
        return clients[(int)((uint)idx % clients.Count)];
    }

    private void RemoveConnection(ThinConnection connection)
    {
        if (connection.ClientId is not null)
        {
            _connectionsByStreamId.TryRemove(connection.StreamId, out _);

            if (_connectionsByServiceHash.TryGetValue(connection.ServiceHash, out var bag))
            {
                var updated = new ConcurrentBag<ThinConnection>(bag.Where(c => c.StreamId != connection.StreamId));
                if (updated.IsEmpty)
                    _connectionsByServiceHash.TryRemove(connection.ServiceHash, out _);
                else
                    _connectionsByServiceHash[connection.ServiceHash] = updated;
            }

            _logger.LogInformation("Client disconnected: {ClientId} ({ClientName})", connection.ClientId, connection.ClientName);
        }

        foreach (var (requestId, pending) in _pendingInvocations)
        {
            if (pending.Caller.StreamId == connection.StreamId)
                _pendingInvocations.TryRemove(requestId, out _);
        }
    }

    private void CleanupStaleInvocations(object? state)
    {
        var now = Environment.TickCount64;
        foreach (var (requestId, pending) in _pendingInvocations)
        {
            if (now - pending.Timestamp > InvocationTimeoutMs)
            {
                if (_pendingInvocations.TryRemove(requestId, out _))
                    _logger.LogDebug("Cleaned up stale invocation {RequestId}", requestId);
            }
        }
    }

    public int ConnectedClients => _connectionsByStreamId.Count;
}

public sealed class ThinConnection
{
    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string StreamId { get; } = Guid.NewGuid().ToString("N");
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int ServiceHash { get; set; }
    public bool IsAlive => _webSocket.State == WebSocketState.Open;

    public ThinConnection(WebSocket webSocket) => _webSocket = webSocket;

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            if (_webSocket.State == WebSocketState.Open)
                await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
