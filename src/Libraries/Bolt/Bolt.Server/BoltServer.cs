using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Bolt.Server.Media;
using Microsoft.Extensions.Logging;

namespace Bolt.Server;

/// <summary>
/// Thin binary WebSocket server that replaces SignalR hub.
/// Accepts raw WebSocket connections, handles registration,
/// routes Request frames to recipients, routes Response frames back to callers.
///
/// Zero SignalR overhead — frames go directly: binary WebSocket ↔ MemoryPack.
/// </summary>
public sealed class BoltServer : IDisposable
{
    private readonly ILogger<BoltServer> _logger;
    private readonly ConcurrentDictionary<string, BoltHubConnection> _connectionsByStreamId = new();
    private readonly ConcurrentDictionary<int, ConcurrentBag<BoltHubConnection>> _connectionsByServiceHash = new();
    private readonly ConcurrentDictionary<Guid, (BoltHubConnection Caller, long Timestamp)> _pendingInvocations = new();
    private readonly ConcurrentDictionary<int, int> _roundRobinIndex = new();

    // Stream routing: streamId → (sender connection, recipient connection)
    private readonly ConcurrentDictionary<Guid, (BoltHubConnection Sender, BoltHubConnection Recipient)> _activeStreams = new();

    // Media routing: streamId → route (sender + recipients for multicast)
    private readonly ConcurrentDictionary<Guid, MediaStreamRoute> _activeMediaStreams = new();

    // Call state management: callId → state
    private readonly ConcurrentDictionary<Guid, ServerCallState> _activeCalls = new();

    // Direct handlers — when registered, server handles requests locally instead of routing
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _localHandlers = new();

    // Media processor tap: registered processors receive copies of media frames on a background thread
    private readonly List<IMediaProcessor> _mediaProcessors = new();
    private readonly Channel<(Guid CallId, Guid StreamId, byte[] Data, uint Timestamp, uint Seq)> _mediaTapChannel;
    private readonly CancellationTokenSource _mediaTapCts = new();

    private readonly Timer _cleanupTimer;
    private const int InvocationTimeoutMs = 30_000;

    public BoltServer(ILogger<BoltServer> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupStaleInvocations, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        _mediaTapChannel = Channel.CreateBounded<(Guid, Guid, byte[], uint, uint)>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            });
        _ = Task.Run(() => MediaTapLoopAsync(_mediaTapCts.Token));
    }

    /// <summary>
    /// Register a media processor that will receive copies of media frames for server-side processing.
    /// Call before accepting connections.
    /// </summary>
    public void RegisterMediaProcessor(IMediaProcessor processor) => _mediaProcessors.Add(processor);

    /// <summary>
    /// Register a local handler. When a request arrives with this command hash,
    /// the server handles it directly instead of routing to another client.
    /// Enables direct client-to-server mode (no hub routing needed).
    /// </summary>
    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = BoltCodec.Fnv1aHash(commandName);
        _localHandlers[hash] = handler;
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
    {
        var connection = new BoltHubConnection(webSocket);
        var receiveBuffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
        byte[]? largeBuffer = null;

        try
        {
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(receiveBuffer.AsMemory(), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType != WebSocketMessageType.Binary || result.Count == 0)
                    continue;

                byte[] frameBytes;
                int totalLength;
                if (!result.EndOfMessage)
                {
                    // Multi-frame: accumulate into growing pooled buffer (zero MemoryStream alloc)
                    var assembled = result.Count;
                    var capacity = Math.Max(result.Count * 4, 512 * 1024);
                    if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
                    largeBuffer = ArrayPool<byte>.Shared.Rent(capacity);
                    receiveBuffer.AsSpan(0, result.Count).CopyTo(largeBuffer);

                    while (!result.EndOfMessage)
                    {
                        result = await webSocket.ReceiveAsync(receiveBuffer.AsMemory(), ct);
                        if (assembled + result.Count > largeBuffer.Length)
                        {
                            var newBuf = ArrayPool<byte>.Shared.Rent(largeBuffer.Length * 2);
                            largeBuffer.AsSpan(0, assembled).CopyTo(newBuf);
                            ArrayPool<byte>.Shared.Return(largeBuffer);
                            largeBuffer = newBuf;
                        }
                        receiveBuffer.AsSpan(0, result.Count).CopyTo(largeBuffer.AsSpan(assembled));
                        assembled += result.Count;
                    }
                    frameBytes = largeBuffer;
                    totalLength = assembled;
                }
                else
                {
                    frameBytes = receiveBuffer;
                    totalLength = result.Count;
                }

                await ProcessFrameAsync(connection, frameBytes, totalLength, ct);
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
            if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
            RemoveConnection(connection);

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
                catch { }
            }
        }
    }

    private async Task ProcessFrameAsync(BoltHubConnection connection, byte[] buffer, int length, CancellationToken ct)
    {
        var frameType = (FrameType)buffer[0];

        switch (frameType)
        {
            case FrameType.Register:
                await HandleRegisterAsync(connection, buffer, length, ct);
                break;
            case FrameType.Request:
                // Process inline — hub work is just header parse + queue to writer channel (non-blocking)
                await HandleRequestAsync(connection, buffer, length, ct);
                break;
            case FrameType.Response:
                await HandleResponseAsync(connection, buffer, length, ct);
                break;
            case FrameType.Push:
                await HandlePushAsync(connection, buffer, length, ct);
                break;
            case FrameType.StreamOpen:
                HandleStreamOpen(connection, buffer, length);
                await RouteStreamFrameAsync(buffer, length, ct);
                break;
            case FrameType.StreamData:
                await RouteStreamFrameAsync(buffer, length, ct);
                break;
            case FrameType.StreamClose:
                await RouteStreamFrameAsync(buffer, length, ct);
                CleanupStream(buffer);
                break;

            // ── Media frame routing ──
            case FrameType.MediaFrame:
            case FrameType.FecFrame:
                await RouteMediaFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.MediaConfig:
                await HandleMediaConfigAsync(connection, buffer, length, ct);
                break;
            case FrameType.MediaFeedback:
            case FrameType.MediaKeyRequest:
            case FrameType.NackRequest:
                await RouteMediaFeedbackAsync(connection, buffer, length, ct);
                break;
            case FrameType.CallSignal:
                await HandleCallSignalAsync(connection, buffer, length, ct);
                break;

            default:
                _logger.LogWarning("Unknown frame type {FrameType} from {ClientId}", frameType, connection.ClientId);
                break;
        }
    }

    private async Task HandleRegisterAsync(BoltHubConnection connection, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadRegister(buffer.AsSpan(0, length), out var clientId, out var clientName, out _))
        {
            _logger.LogWarning("Invalid register frame");
            return;
        }

        connection.ClientId = clientId;
        connection.ClientName = clientName;
        connection.ServiceHash = BoltCodec.Fnv1aHash(clientId);

        _connectionsByStreamId[connection.StreamId] = connection;
        _connectionsByServiceHash.AddOrUpdate(
            connection.ServiceHash,
            _ => new ConcurrentBag<BoltHubConnection> { connection },
            (_, bag) => { bag.Add(connection); return bag; });

        _logger.LogInformation("Client registered: {ClientId} ({ClientName}) [hash={ServiceHash}]",
            clientId, clientName, connection.ServiceHash);

        var writer = new ArrayBufferWriter<byte>(2);
        BoltCodec.WriteRegisterAck(writer, true);
        await connection.SendAsync(writer.WrittenMemory, ct);
    }

    private async Task HandleRequestAsync(BoltHubConnection caller, byte[] buffer, int length, CancellationToken ct)
    {
        var span = buffer.AsSpan(0, length);

        // Check for local handler first (direct mode — server handles request itself)
        if (_localHandlers.Count > 0 && BoltCodec.TryReadRequest(span, out var frame, out var consumed))
        {
            if (_localHandlers.TryGetValue(frame.CommandHash, out var handler))
            {
                try
                {
                    var payload = frame.GetPayload(buffer.AsMemory(0, length));
                    var (statusCode, responsePayload) = await handler(payload, frame.RequestId);

                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteResponse(writer, frame.RequestId, statusCode, responsePayload.Span);
                    await caller.SendAsync(writer.WrittenMemory, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Local handler error for command hash {Hash}", frame.CommandHash);
                    var errWriter = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.InternalServerError, ReadOnlySpan<byte>.Empty);
                    await caller.SendAsync(errWriter.WrittenMemory, ct);
                }
                return;
            }
        }

        // Hub mode — route to recipient
        if (!BoltCodec.TryReadRequestHeader(span, out var requestId, out var recipientHash, out var totalSize))
        {
            _logger.LogWarning("Invalid request frame from {ClientId}", caller.ClientId);
            return;
        }

        _pendingInvocations[requestId] = (caller, Environment.TickCount64);

        var recipient = GetRecipient(recipientHash);
        if (recipient is null)
        {
            var errWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(errWriter, requestId, HttpStatusCode.NotFound, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter.WrittenMemory, ct);
            _pendingInvocations.TryRemove(requestId, out _);
            return;
        }

        // Forward raw bytes — zero decode, zero copy
        await recipient.SendAsync(buffer.AsMemory(0, totalSize), ct);
    }

    private async Task HandleResponseAsync(BoltHubConnection responder, byte[] buffer, int length, CancellationToken ct)
    {
        // Header-only read — extract RequestId for routing without touching payload
        if (!BoltCodec.TryReadResponseHeader(buffer.AsSpan(0, length), out var requestId, out var totalSize))
        {
            _logger.LogWarning("Invalid response frame from {ClientId}", responder.ClientId);
            return;
        }

        if (_pendingInvocations.TryRemove(requestId, out var pending))
        {
            await pending.Caller.SendAsync(buffer.AsMemory(0, totalSize), ct);
        }
    }

    private async Task HandlePushAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadRequestHeader(buffer.AsSpan(0, length), out _, out var recipientHash, out var totalSize))
            return;

        // Broadcast: recipientHash == 0 → send to all connected clients (except sender)
        if (recipientHash == 0)
        {
            var data = buffer.AsMemory(0, totalSize);
            foreach (var (_, bag) in _connectionsByServiceHash)
            {
                foreach (var client in bag)
                {
                    // Backpressure: skip push to congested clients (push is best-effort)
                    if (client.IsAlive && client.StreamId != sender.StreamId && !client.IsUnderPressure)
                        await client.SendAsync(data, ct);
                }
            }
            return;
        }

        var recipient = GetRecipient(recipientHash);
        if (recipient is not null && !recipient.IsUnderPressure)
            await recipient.SendAsync(buffer.AsMemory(0, totalSize), ct);
    }

    /// <summary>Get the count of currently connected clients.</summary>
    public int ConnectedClientCount => _connectionsByStreamId.Count;

    /// <summary>Get all connected client IDs for presence queries.</summary>
    public IEnumerable<string> GetConnectedClientIds()
    {
        foreach (var (_, conn) in _connectionsByStreamId)
        {
            if (conn.IsAlive && conn.ClientId is not null)
                yield return conn.ClientId;
        }
    }

    // ── Stream routing ──

    private void HandleStreamOpen(BoltHubConnection sender, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadStreamOpen(buffer.AsSpan(0, length), out var streamId, out var recipientHash, out _))
            return;

        var recipient = GetRecipient(recipientHash);
        if (recipient is not null)
        {
            _activeStreams[streamId] = (sender, recipient);
            _logger.LogDebug("Stream opened: {StreamId} from {Sender} to {Recipient}",
                streamId, sender.ClientId, recipient.ClientId);
        }
    }

    /// <summary>
    /// Route a stream frame (Data or Close) to the correct peer.
    /// If the sender is the stream's Sender, forward to Recipient and vice versa.
    /// </summary>
    private async Task RouteStreamFrameAsync(byte[] buffer, int length, CancellationToken ct)
    {
        if (length < 17) return; // Need at least 1 byte type + 16 bytes streamId

        var streamId = BoltCodec.ReadStreamId(buffer.AsSpan(0, length));

        if (!_activeStreams.TryGetValue(streamId, out var peers))
            return;

        // Forward raw bytes to the other side — zero decode, zero copy
        // Determine direction: if frame came from the sender, forward to recipient and vice versa
        // Since we can't easily tell which connection this came from in this method,
        // forward to both peers (the one that sent it will ignore its own frame in its receive loop)
        // Actually, we route based on the stream open: sender→recipient for data, recipient→sender for data back
        // For simplicity, forward to recipient (sender initiated the stream)
        await peers.Recipient.SendAsync(buffer.AsMemory(0, length), ct);
    }

    private void CleanupStream(byte[] buffer)
    {
        if (buffer.Length >= 17)
        {
            var streamId = BoltCodec.ReadStreamId(buffer.AsSpan());
            _activeStreams.TryRemove(streamId, out _);
        }
    }

    // ── Media frame routing ──

    /// <summary>
    /// Hot path for MediaFrame and FecFrame: header-only decode (streamId from bytes 1-16),
    /// look up route, forward raw bytes to all recipients. Skip sender.
    /// If media processors are registered, write a copy to the tap channel.
    /// </summary>
    private async Task RouteMediaFrameAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadMediaFrameHeader(buffer.AsSpan(0, length), out var streamId))
            return;

        if (!_activeMediaStreams.TryGetValue(streamId, out var route))
            return;

        var data = buffer.AsMemory(0, length);

        // Simulcast-aware routing: if this stream has a layer ID, only forward to
        // recipients whose preferred layer matches (or who have no preference = forward all)
        var isSimulcast = route.SimulcastLayerId.HasValue;

        foreach (var recipient in route.Recipients)
        {
            if (recipient.StreamId == sender.StreamId || !recipient.IsAlive)
                continue;

            // Simulcast filtering: skip if recipient prefers a different layer
            if (isSimulcast && _activeCalls.TryGetValue(route.CallId, out var callState))
            {
                if (callState.RecipientPreferredLayer.TryGetValue(recipient.StreamId, out var preferred)
                    && preferred != route.SimulcastLayerId!.Value)
                    continue; // Recipient prefers a different layer — skip
            }

            // Backpressure: skip drop-eligible media frames if recipient is congested
            if (recipient.IsUnderPressure)
            {
                // Check if frame is drop-eligible (flag 0x40)
                if (length > 25 && (buffer[25] & 0x40) != 0)
                    continue; // Drop this frame — recipient can't keep up
            }

            await recipient.SendAsync(data, ct);
        }

        // Tap: send a copy to media processors (non-blocking, drops if full)
        if (_mediaProcessors.Count > 0)
        {
            if (BoltCodec.TryReadMediaFrame(buffer.AsSpan(0, length), out var mfHeader))
            {
                var dataCopy = buffer.AsSpan(0, length).ToArray();
                _mediaTapChannel.Writer.TryWrite((route.CallId, mfHeader.StreamId, dataCopy, mfHeader.Timestamp, mfHeader.SequenceNumber));
            }
        }
    }

    /// <summary>
    /// Handle MediaConfig: register the media stream in the routing table and forward to recipients.
    /// </summary>
    private async Task HandleMediaConfigAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadMediaConfig(buffer.AsSpan(0, length), out var config))
        {
            _logger.LogWarning("Invalid MediaConfig frame from {ClientId}", sender.ClientId);
            return;
        }

        // Register or update the media stream route
        var route = _activeMediaStreams.GetOrAdd(config.StreamId, _ => new MediaStreamRoute
        {
            Sender = sender,
            CallId = config.CallId,
        });

        // If the call exists, add stream to its tracking list
        if (_activeCalls.TryGetValue(config.CallId, out var callState))
        {
            lock (callState.MediaStreamIds)
            {
                if (!callState.MediaStreamIds.Contains(config.StreamId))
                    callState.MediaStreamIds.Add(config.StreamId);
            }

            // Add all call participants (except sender) as recipients
            lock (callState.Participants)
            {
                foreach (var participant in callState.Participants)
                {
                    if (participant.StreamId != sender.StreamId && !route.Recipients.Any(r => r.StreamId == participant.StreamId))
                        route.Recipients.Add(participant);
                }
            }
        }

        // Forward config to all recipients
        var data = buffer.AsMemory(0, length);
        foreach (var recipient in route.Recipients)
        {
            if (recipient.IsAlive)
                await recipient.SendAsync(data, ct);
        }

        _logger.LogDebug("Media stream registered: {StreamId} (call={CallId}, type={MediaType}, codec={CodecId}) from {ClientId}",
            config.StreamId, config.CallId, config.MediaType, config.CodecId, sender.ClientId);
    }

    /// <summary>
    /// Route MediaFeedback and MediaKeyRequest back to the stream sender (reverse direction).
    /// </summary>
    private async Task RouteMediaFeedbackAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        var span = buffer.AsSpan(0, length);
        if (!BoltCodec.TryReadMediaFrameHeader(span, out var streamId))
            return;

        if (!_activeMediaStreams.TryGetValue(streamId, out var route))
            return;

        // Simulcast layer selection: if feedback contains Decrease/KeyframeNeeded,
        // downgrade the recipient to a lower layer; if Increase, upgrade.
        if (route.SimulcastLayerId.HasValue
            && (FrameType)buffer[0] == FrameType.MediaFeedback
            && BoltCodec.TryReadMediaFeedback(span, out var feedback)
            && _activeCalls.TryGetValue(route.CallId, out var callState))
        {
            var currentLayer = callState.RecipientPreferredLayer.GetOrAdd(sender.StreamId, route.SimulcastLayerId.Value);
            switch (feedback.QualityHint)
            {
                case QualityHint.Decrease or QualityHint.KeyframeNeeded when currentLayer > 0:
                    callState.RecipientPreferredLayer[sender.StreamId] = (byte)(currentLayer - 1);
                    break;
                case QualityHint.Increase when currentLayer < 2:
                    callState.RecipientPreferredLayer[sender.StreamId] = (byte)(currentLayer + 1);
                    break;
            }
        }

        // Feedback goes back to the stream's sender
        if (route.Sender.IsAlive)
            await route.Sender.SendAsync(buffer.AsMemory(0, length), ct);
    }

    // ── Call signaling ──

    /// <summary>
    /// Handle call signaling frames. Manages call lifecycle and routes signals between parties.
    /// </summary>
    private async Task HandleCallSignalAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        var span = buffer.AsSpan(0, length);
        if (!BoltCodec.TryReadCallSignal(span, out var header))
        {
            _logger.LogWarning("Invalid CallSignal frame from {ClientId}", sender.ClientId);
            return;
        }

        switch (header.SignalType)
        {
            case SignalType.Initiate:
                await HandleCallInitiateAsync(sender, buffer, length, header, ct);
                break;
            case SignalType.Answer:
                await HandleCallAnswerAsync(sender, buffer, length, header, ct);
                break;
            case SignalType.Reject:
                await HandleCallRejectAsync(sender, buffer, length, header, ct);
                break;
            case SignalType.End:
                await HandleCallEndAsync(sender, buffer, length, header, ct);
                break;
            case SignalType.Hold:
            case SignalType.Unhold:
                await HandleCallHoldAsync(sender, buffer, length, header, ct);
                break;
            case SignalType.AddParticipant:
                await HandleAddParticipantAsync(sender, buffer, length, header, ct);
                break;
            case SignalType.RemoveParticipant:
                await HandleRemoveParticipantAsync(sender, buffer, length, header, ct);
                break;
            case SignalType.DirectOffer:
            case SignalType.DirectAnswer:
                await RelayCallSignalAsync(sender, buffer, length, header, ct);
                break;
            default:
                _logger.LogWarning("Unhandled call signal type {SignalType} from {ClientId}", header.SignalType, sender.ClientId);
                break;
        }
    }

    /// <summary>
    /// Handle Initiate: create call state, look up callee from payload (first 4 bytes = recipientHash),
    /// send Ring back to caller, forward Initiate to callee.
    /// </summary>
    private async Task HandleCallInitiateAsync(BoltHubConnection caller, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        // Payload starts with recipientHash (4 bytes, little-endian)
        if (header.PayloadLength < 4)
        {
            _logger.LogWarning("CallSignal Initiate from {ClientId} has no recipient hash in payload", caller.ClientId);
            return;
        }

        var recipientHash = BinaryPrimitives.ReadInt32LittleEndian(
            buffer.AsSpan(header.PayloadOffset, 4));

        var callee = GetRecipient(recipientHash);
        if (callee is null)
        {
            // No recipient found — send End back to caller
            _logger.LogDebug("Call {CallId} initiate failed: no recipient for hash {RecipientHash}", header.CallId, recipientHash);
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteCallSignal(writer, header.CallId, SignalType.End, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(writer.WrittenMemory, ct);
            return;
        }

        var callState = new ServerCallState
        {
            CallId = header.CallId,
            Status = ServerCallStatus.Ringing,
            CallerConnection = caller,
            CalleeConnection = callee,
        };
        callState.Participants.Add(caller);
        callState.Participants.Add(callee);

        _activeCalls[header.CallId] = callState;

        // Send Ring back to caller
        var ringWriter = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(ringWriter, header.CallId, SignalType.Ring, ReadOnlySpan<byte>.Empty);
        await caller.SendAsync(ringWriter.WrittenMemory, ct);

        // Forward the full Initiate frame to the callee
        await callee.SendAsync(buffer.AsMemory(0, length), ct);

        _logger.LogDebug("Call {CallId} initiated: {Caller} → {Callee}",
            header.CallId, caller.ClientId, callee.ClientId);
    }

    /// <summary>
    /// Handle Answer: transition to Active, forward to caller, notify media processors.
    /// </summary>
    private async Task HandleCallAnswerAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
        {
            _logger.LogDebug("Call {CallId} Answer from {ClientId} but call not found", header.CallId, sender.ClientId);
            return;
        }

        callState.Status = ServerCallStatus.Active;

        // Forward Answer to the caller
        await callState.CallerConnection.SendAsync(buffer.AsMemory(0, length), ct);

        // Notify media processors that the call is now active
        await NotifyProcessorsCallStartedAsync(header.CallId);

        _logger.LogDebug("Call {CallId} answered by {ClientId}", header.CallId, sender.ClientId);
    }

    /// <summary>
    /// Handle Reject: transition to Rejected, forward to caller, cleanup, notify media processors.
    /// </summary>
    private async Task HandleCallRejectAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
            return;

        callState.Status = ServerCallStatus.Rejected;

        // Forward Reject to the caller
        await callState.CallerConnection.SendAsync(buffer.AsMemory(0, length), ct);

        CleanupCall(header.CallId);

        // Notify media processors that the call ended
        await NotifyProcessorsCallEndedAsync(header.CallId);

        _logger.LogDebug("Call {CallId} rejected by {ClientId}", header.CallId, sender.ClientId);
    }

    /// <summary>
    /// Handle End: transition to Ended, forward to all other participants, cleanup media streams, notify processors.
    /// </summary>
    private async Task HandleCallEndAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
            return;

        callState.Status = ServerCallStatus.Ended;

        // Forward End to all other participants (supports group calls)
        var data = buffer.AsMemory(0, length);
        lock (callState.Participants)
        {
            foreach (var participant in callState.Participants)
            {
                if (participant.StreamId != sender.StreamId && participant.IsAlive)
                    _ = participant.SendAsync(data, ct);
            }
        }

        CleanupCall(header.CallId);

        // Notify media processors that the call ended
        await NotifyProcessorsCallEndedAsync(header.CallId);

        _logger.LogDebug("Call {CallId} ended by {ClientId}", header.CallId, sender.ClientId);
    }

    /// <summary>
    /// Handle Hold/Unhold: update state, forward to the other party.
    /// </summary>
    private async Task HandleCallHoldAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
            return;

        callState.Status = header.SignalType == SignalType.Hold ? ServerCallStatus.Held : ServerCallStatus.Active;

        // Forward to the other party
        var otherParty = callState.CallerConnection.StreamId == sender.StreamId
            ? callState.CalleeConnection
            : callState.CallerConnection;

        if (otherParty is { IsAlive: true })
            await otherParty.SendAsync(buffer.AsMemory(0, length), ct);
    }

    /// <summary>
    /// Pure relay for DirectOffer/DirectAnswer: forward to the other party without state changes.
    /// </summary>
    private async Task RelayCallSignalAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
            return;

        var otherParty = callState.CallerConnection.StreamId == sender.StreamId
            ? callState.CalleeConnection
            : callState.CallerConnection;

        if (otherParty is { IsAlive: true })
            await otherParty.SendAsync(buffer.AsMemory(0, length), ct);
    }

    // ── Group call: Add/Remove participant ──

    /// <summary>
    /// Handle AddParticipant: look up the new participant by recipientHash from payload,
    /// add to call state, add to all existing media stream routes, request keyframes from all senders,
    /// and forward the signal to all existing participants.
    /// </summary>
    private async Task HandleAddParticipantAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
        {
            _logger.LogDebug("Call {CallId} AddParticipant from {ClientId} but call not found", header.CallId, sender.ClientId);
            return;
        }

        if (header.PayloadLength < 4)
        {
            _logger.LogWarning("CallSignal AddParticipant from {ClientId} has no recipient hash in payload", sender.ClientId);
            return;
        }

        var recipientHash = BinaryPrimitives.ReadInt32LittleEndian(
            buffer.AsSpan(header.PayloadOffset, 4));

        var newParticipant = GetRecipient(recipientHash);
        if (newParticipant is null)
        {
            _logger.LogDebug("Call {CallId} AddParticipant failed: no recipient for hash {RecipientHash}", header.CallId, recipientHash);
            return;
        }

        // Add to participants list
        lock (callState.Participants)
        {
            if (!callState.Participants.Any(p => p.StreamId == newParticipant.StreamId))
                callState.Participants.Add(newParticipant);
        }

        // Add to all existing media stream routes as a recipient + request keyframes from senders
        List<Guid> streamIds;
        lock (callState.MediaStreamIds)
        {
            streamIds = new List<Guid>(callState.MediaStreamIds);
        }

        foreach (var streamId in streamIds)
        {
            if (!_activeMediaStreams.TryGetValue(streamId, out var route))
                continue;

            // Add new participant as recipient (if not already present and not the sender)
            if (route.Sender.StreamId != newParticipant.StreamId &&
                !route.Recipients.Any(r => r.StreamId == newParticipant.StreamId))
            {
                route.Recipients.Add(newParticipant);
            }

            // Send MediaKeyRequest to the stream's sender so the new participant gets a keyframe
            if (route.Sender.IsAlive)
            {
                var keyReqWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteMediaKeyRequest(keyReqWriter, streamId);
                await route.Sender.SendAsync(keyReqWriter.WrittenMemory, ct);
            }
        }

        // Forward the AddParticipant signal to all existing participants
        var data = buffer.AsMemory(0, length);
        lock (callState.Participants)
        {
            foreach (var participant in callState.Participants)
            {
                if (participant.StreamId != sender.StreamId && participant.StreamId != newParticipant.StreamId && participant.IsAlive)
                    _ = participant.SendAsync(data, ct);
            }
        }

        // Also send the signal to the new participant
        if (newParticipant.IsAlive)
            await newParticipant.SendAsync(data, ct);

        _logger.LogDebug("Call {CallId} participant added: {NewParticipant} (by {Sender})",
            header.CallId, newParticipant.ClientId, sender.ClientId);
    }

    /// <summary>
    /// Handle RemoveParticipant: remove from call state and all media stream routes,
    /// forward the signal to remaining participants.
    /// </summary>
    private async Task HandleRemoveParticipantAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
        {
            _logger.LogDebug("Call {CallId} RemoveParticipant from {ClientId} but call not found", header.CallId, sender.ClientId);
            return;
        }

        if (header.PayloadLength < 4)
        {
            _logger.LogWarning("CallSignal RemoveParticipant from {ClientId} has no recipient hash in payload", sender.ClientId);
            return;
        }

        var recipientHash = BinaryPrimitives.ReadInt32LittleEndian(
            buffer.AsSpan(header.PayloadOffset, 4));

        // Remove from participants list
        string? removedClientId = null;
        lock (callState.Participants)
        {
            var idx = callState.Participants.FindIndex(p => p.ServiceHash == recipientHash);
            if (idx >= 0)
            {
                removedClientId = callState.Participants[idx].ClientId;
                callState.Participants.RemoveAt(idx);
            }
        }

        // Remove from all media stream recipient lists
        List<Guid> streamIds;
        lock (callState.MediaStreamIds)
        {
            streamIds = new List<Guid>(callState.MediaStreamIds);
        }

        foreach (var streamId in streamIds)
        {
            if (_activeMediaStreams.TryGetValue(streamId, out var route))
                route.Recipients.RemoveAll(r => r.ServiceHash == recipientHash);
        }

        // Forward the RemoveParticipant signal to remaining participants
        var data = buffer.AsMemory(0, length);
        lock (callState.Participants)
        {
            foreach (var participant in callState.Participants)
            {
                if (participant.StreamId != sender.StreamId && participant.IsAlive)
                    _ = participant.SendAsync(data, ct);
            }
        }

        _logger.LogDebug("Call {CallId} participant removed: {Removed} (by {Sender})",
            header.CallId, removedClientId ?? $"hash={recipientHash}", sender.ClientId);
    }

    // ── Media processor tap ──

    /// <summary>
    /// Background loop that reads media frame copies from the tap channel
    /// and dispatches them to all registered media processors.
    /// </summary>
    private async Task MediaTapLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var (callId, streamId, data, ts, seq) in _mediaTapChannel.Reader.ReadAllAsync(ct))
            {
                foreach (var processor in _mediaProcessors)
                {
                    try
                    {
                        await processor.ProcessFrameAsync(callId, streamId, data, ts, seq);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Media processor error for call {CallId}", callId);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Notify all media processors that a call has started.</summary>
    private async Task NotifyProcessorsCallStartedAsync(Guid callId)
    {
        foreach (var processor in _mediaProcessors)
        {
            try
            {
                await processor.OnCallStartedAsync(callId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media processor OnCallStarted error for call {CallId}", callId);
            }
        }
    }

    /// <summary>Notify all media processors that a call has ended.</summary>
    private async Task NotifyProcessorsCallEndedAsync(Guid callId)
    {
        foreach (var processor in _mediaProcessors)
        {
            try
            {
                await processor.OnCallEndedAsync(callId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media processor OnCallEnded error for call {CallId}", callId);
            }
        }
    }

    /// <summary>
    /// Remove all media streams and call state for a given call.
    /// </summary>
    private void CleanupCall(Guid callId)
    {
        if (_activeCalls.TryRemove(callId, out var callState))
        {
            lock (callState.MediaStreamIds)
            {
                foreach (var streamId in callState.MediaStreamIds)
                    _activeMediaStreams.TryRemove(streamId, out _);
            }
        }
    }

    private BoltHubConnection? GetRecipient(int serviceHash)
    {
        if (!_connectionsByServiceHash.TryGetValue(serviceHash, out var bag))
            return null;

        // Direct iteration — no LINQ, no List allocation
        BoltHubConnection? firstAlive = null;
        int aliveCount = 0;

        foreach (var client in bag)
        {
            if (client.IsAlive)
            {
                firstAlive ??= client;
                aliveCount++;
            }
        }

        if (aliveCount <= 1) return firstAlive;

        // Round-robin for multiple clients
        var idx = _roundRobinIndex.AddOrUpdate(serviceHash, 0, (_, prev) => prev + 1);
        var targetIdx = (int)((uint)idx % aliveCount);
        var current = 0;
        foreach (var client in bag)
        {
            if (client.IsAlive)
            {
                if (current == targetIdx) return client;
                current++;
            }
        }

        return firstAlive;
    }

    private void RemoveConnection(BoltHubConnection connection)
    {
        if (connection.ClientId is not null)
        {
            _connectionsByStreamId.TryRemove(connection.StreamId, out _);

            if (_connectionsByServiceHash.TryGetValue(connection.ServiceHash, out var bag))
            {
                var updated = new ConcurrentBag<BoltHubConnection>(bag.Where(c => c.StreamId != connection.StreamId));
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

        // End any active calls this connection is part of
        foreach (var (callId, callState) in _activeCalls)
        {
            var isParticipant = callState.CallerConnection.StreamId == connection.StreamId
                || (callState.CalleeConnection is not null && callState.CalleeConnection.StreamId == connection.StreamId);

            if (!isParticipant) continue;

            callState.Status = ServerCallStatus.Ended;

            // Notify the other party
            var otherParty = callState.CallerConnection.StreamId == connection.StreamId
                ? callState.CalleeConnection
                : callState.CallerConnection;

            if (otherParty is { IsAlive: true })
            {
                try
                {
                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteCallSignal(writer, callId, SignalType.End, ReadOnlySpan<byte>.Empty);
                    // Fire-and-forget: we're in cleanup, can't await reliably
                    _ = otherParty.SendAsync(writer.WrittenMemory, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send End signal for call {CallId} during disconnect cleanup", callId);
                }
            }

            CleanupCall(callId);
        }

        // Remove connection from any media stream recipient lists
        foreach (var (streamId, route) in _activeMediaStreams)
        {
            route.Recipients.RemoveAll(r => r.StreamId == connection.StreamId);

            // If sender disconnected, remove the whole route
            if (route.Sender.StreamId == connection.StreamId)
                _activeMediaStreams.TryRemove(streamId, out _);
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

        // Cleanup stale Ringing calls (unanswered for > 30 seconds)
        var utcNow = DateTime.UtcNow;
        foreach (var (callId, callState) in _activeCalls)
        {
            if (callState.Status != ServerCallStatus.Ringing) continue;
            if ((utcNow - callState.CreatedAt).TotalSeconds <= 30) continue;

            callState.Status = ServerCallStatus.Missed;

            // Send End to the caller
            if (callState.CallerConnection.IsAlive)
            {
                try
                {
                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteCallSignal(writer, callId, SignalType.End, ReadOnlySpan<byte>.Empty);
                    _ = callState.CallerConnection.SendAsync(writer.WrittenMemory, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send End for missed call {CallId}", callId);
                }
            }

            // Send End to the callee too
            if (callState.CalleeConnection is { IsAlive: true })
            {
                try
                {
                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteCallSignal(writer, callId, SignalType.End, ReadOnlySpan<byte>.Empty);
                    _ = callState.CalleeConnection.SendAsync(writer.WrittenMemory, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send End for missed call {CallId}", callId);
                }
            }

            CleanupCall(callId);

            // Notify media processors (fire-and-forget in timer callback)
            _ = NotifyProcessorsCallEndedAsync(callId);

            _logger.LogDebug("Call {CallId} timed out (missed) after 30s ringing", callId);
        }
    }

    public int ConnectedClients => _connectionsByStreamId.Count;

    public void Dispose()
    {
        _mediaTapCts.Cancel();
        _mediaTapChannel.Writer.TryComplete();
        _cleanupTimer.Dispose();
        _mediaTapCts.Dispose();
    }
}

public sealed class BoltHubConnection
{
    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string StreamId { get; } = Guid.NewGuid().ToString("N");
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int ServiceHash { get; set; }
    public bool IsAlive => _webSocket.State == WebSocketState.Open;

    /// <summary>Pending bytes queued for this connection. Used for backpressure decisions.</summary>
    public long PendingBytes => Interlocked.Read(ref _pendingBytes);
    private long _pendingBytes;

    /// <summary>Backpressure threshold: drop media frames when pending exceeds this (1MB).</summary>
    public const long BackpressureDropThreshold = 1024 * 1024;

    /// <summary>Backpressure threshold: send feedback signal to reduce sender rate (2MB).</summary>
    public const long BackpressureFeedbackThreshold = 2 * 1024 * 1024;

    /// <summary>True if this connection is under backpressure (pending > drop threshold).</summary>
    public bool IsUnderPressure => PendingBytes > BackpressureDropThreshold;

    public BoltHubConnection(WebSocket webSocket) => _webSocket = webSocket;

    /// <summary>
    /// Send data with fast-path for uncontended lock. Tracks pending bytes for backpressure.
    /// </summary>
    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        Interlocked.Add(ref _pendingBytes, data.Length);

        // Fast path: no contention — send synchronously, zero overhead
        if (_sendLock.Wait(0))
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    var task = _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
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
            finally
            {
                _sendLock.Release();
            }
        }
        // Slow path: contention — await lock
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
            if (_webSocket.State == WebSocketState.Open)
                await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
        }
        finally
        {
            _sendLock.Release();
            Interlocked.Add(ref _pendingBytes, -data.Length);
        }
    }
}

/// <summary>
/// Routing entry for an active media stream.
/// Sender produces frames; Recipients receive them (multicast).
/// </summary>
internal sealed class MediaStreamRoute
{
    public BoltHubConnection Sender { get; init; } = null!;
    public List<BoltHubConnection> Recipients { get; } = new();
    public Guid CallId { get; init; }

    /// <summary>
    /// For simulcast: maps this stream to a simulcast layer group.
    /// All streams in the same group (callId + sender) represent different quality layers.
    /// The hub forwards only the selected layer per recipient.
    /// </summary>
    public byte? SimulcastLayerId { get; set; }
}

/// <summary>Server-side call status.</summary>
internal enum ServerCallStatus { Ringing, Active, Held, Ended, Rejected, Missed }

/// <summary>
/// Server-side call state tracking. Manages participants, associated media streams, and lifecycle.
/// </summary>
internal sealed class ServerCallState
{
    public Guid CallId { get; init; }
    public ServerCallStatus Status { get; set; }
    public BoltHubConnection CallerConnection { get; init; } = null!;
    public BoltHubConnection? CalleeConnection { get; set; }
    public List<BoltHubConnection> Participants { get; } = new();
    public List<Guid> MediaStreamIds { get; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Simulcast: per-recipient preferred layer. Key = recipient StreamId, Value = preferred SimulcastLayerId.
    /// When a recipient sends MediaFeedback with a quality hint, the hub updates this
    /// and only forwards media streams matching the preferred layer.
    /// </summary>
    public ConcurrentDictionary<string, byte> RecipientPreferredLayer { get; } = new();

    /// <summary>
    /// Simulcast: maps sender StreamId → list of simulcast stream IDs (grouped by layer).
    /// Key = sender connection StreamId, Value = dict of layerId → media streamId.
    /// </summary>
    public ConcurrentDictionary<string, ConcurrentDictionary<byte, Guid>> SimulcastGroups { get; } = new();
}
