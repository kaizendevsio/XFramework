using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Bolt.Protocol.Transport;
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
    private const string DefaultRequiredServiceScope = "bolt.service";
    private static readonly string[] DefaultServiceIdentityClaimTypes = ["client_id", "service", "azp", "sub"];

    private sealed class DurableReplayState
    {
        public object SyncRoot { get; } = new();
        public ConcurrentQueue<(long Sequence, byte[] Payload)> DeferredEvents { get; } = new();
        public bool AcceptingDeferredEvents { get; set; } = true;
    }

    private readonly ILogger<BoltServer> _logger;
    private readonly ConcurrentDictionary<string, BoltHubConnection> _connectionsByStreamId = new();
    private readonly ConcurrentDictionary<int, ConcurrentBag<BoltHubConnection>> _connectionsByServiceHash = new();
    private readonly ConcurrentDictionary<int, string> _clientIdsByServiceHash = new();
    private readonly ConcurrentDictionary<Guid, PendingInvocation> _pendingInvocations = new();
    private readonly ConcurrentDictionary<int, int> _roundRobinIndex = new();

    // Stream routing: streamId → (sender connection, recipient connection)
    private readonly ConcurrentDictionary<Guid, StreamRoute> _activeStreams = new();

    // Media routing: streamId → route (sender + recipients for multicast)
    private readonly ConcurrentDictionary<Guid, MediaStreamRoute> _activeMediaStreams = new();

    // Call state management: callId → state
    private readonly ConcurrentDictionary<Guid, ServerCallState> _activeCalls = new();

    // Direct handlers — when registered, server handles requests locally instead of routing
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _localHandlers = new();
    private readonly ConcurrentDictionary<int, Func<BoltRequestContext, ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _contextLocalHandlers = new();
    private readonly ConcurrentDictionary<int, string> _commandNamesByHash = new();

    // Media processor tap: registered processors receive copies of media frames on a background thread
    private readonly List<IMediaProcessor> _mediaProcessors = new();
    private readonly Channel<(Guid CallId, Guid StreamId, byte[] Data, uint Timestamp, uint Seq)> _mediaTapChannel;
    private readonly CancellationTokenSource _mediaTapCts = new();

    private readonly Timer _cleanupTimer;
    private readonly int _invocationTimeoutMs;
    private readonly int _maxFrameBytes;
    private readonly int _sendQueueCapacity;
    private readonly int _sendEnqueueTimeoutMs;
    private readonly BoltRegistrationIdentityBindingMode _registrationIdentityBindingMode;
    private readonly string _requiredServiceScope;
    private readonly string[] _serviceIdentityClaimTypes;
    private readonly HashSet<string> _reservedServiceNames;
    private readonly string[] _reservedServiceNamePrefixes;
    private readonly HashSet<string> _reservedServiceClientIds;
    private static readonly int LargeRpcCommandHash = BoltCodec.Fnv1aHash("__bolt_large_rpc__");
    private static readonly int LargeRpcResponseHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response__");
    private static readonly int LargeRpcResponseStreamHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response_stream__");

    // Pub/sub state — transient (live fan-out only)
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<BoltHubConnection, byte>> _liveSubscribersByTopic = new();
    private readonly ConcurrentDictionary<BoltHubConnection, ConcurrentDictionary<int, byte>> _liveSubscriptionsByConnection = new();

    // Pub/sub state — durable (persistent identity)
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), BoltHubConnection> _liveDurableConnections = new();
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), DurableReplayState> _replayingDurableSubscriptions = new();
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), SemaphoreSlim> _durablePublishLocks = new();
    private readonly ConcurrentDictionary<int, string> _topicNamesByHash = new();

    // Durable queue backend (injected)
    private readonly Bolt.Server.Durable.IDurableQueueStore? _durableStore;
    private readonly Bolt.Server.Durable.DurableQueueOptions? _durableOptions;
    private readonly IReadOnlyList<IBoltTopicAuthorizer> _topicAuthorizers;

    public BoltServer(ILogger<BoltServer> logger)
        : this(logger, new BoltServerOptions())
    {
    }

    public BoltServer(
        ILogger<BoltServer> logger,
        BoltServerOptions options,
        IEnumerable<IBoltTopicAuthorizer>? topicAuthorizers = null)
    {
        _logger = logger;
        _topicAuthorizers = topicAuthorizers?.ToList() ?? [];
        _invocationTimeoutMs = Math.Max(1, options.InvocationTimeoutMs);
        _maxFrameBytes = Math.Max(1024, options.MaxFrameBytes);
        _sendQueueCapacity = Math.Max(1, options.SendQueueCapacity);
        _sendEnqueueTimeoutMs = options.SendEnqueueTimeoutMs > 0
            ? options.SendEnqueueTimeoutMs
            : _invocationTimeoutMs;
        if (!Enum.IsDefined(options.RegistrationIdentityBindingMode))
        {
            throw new InvalidOperationException(
                $"Unsupported Bolt registration identity binding mode '{options.RegistrationIdentityBindingMode}'.");
        }

        _registrationIdentityBindingMode = options.RegistrationIdentityBindingMode;
        _requiredServiceScope = string.IsNullOrWhiteSpace(options.RequiredServiceScope)
            ? DefaultRequiredServiceScope
            : options.RequiredServiceScope.Trim();
        _serviceIdentityClaimTypes = options.ServiceIdentityClaimTypes
            .Where(static claimType => !string.IsNullOrWhiteSpace(claimType))
            .Select(static claimType => claimType.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (_serviceIdentityClaimTypes.Length == 0)
            _serviceIdentityClaimTypes = DefaultServiceIdentityClaimTypes;

        _reservedServiceNames = NormalizeOptionSet(options.ReservedServiceNames, StringComparer.Ordinal);
        _reservedServiceNamePrefixes = NormalizeOptionList(options.ReservedServiceNamePrefixes);
        _reservedServiceClientIds = NormalizeOptionSet(options.ReservedServiceClientIds, StringComparer.OrdinalIgnoreCase);
        foreach (var reservedServiceName in _reservedServiceNames)
            _reservedServiceClientIds.Add(Sha256Hex(reservedServiceName));

        var cleanupInterval = TimeSpan.FromSeconds(Math.Max(1, options.CleanupIntervalSeconds));
        _cleanupTimer = new Timer(CleanupStaleInvocations, null, cleanupInterval, cleanupInterval);
        _mediaTapChannel = Channel.CreateBounded<(Guid, Guid, byte[], uint, uint)>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            });
        _ = Task.Run(() => MediaTapLoopAsync(_mediaTapCts.Token));
    }

    public BoltServer(
        ILogger<BoltServer> logger,
        Bolt.Server.Durable.IDurableQueueStore durableStore,
        Microsoft.Extensions.Options.IOptions<Bolt.Server.Durable.DurableQueueOptions> durableOptions)
        : this(logger, new BoltServerOptions(), durableStore, durableOptions)
    {
    }

    public BoltServer(
        ILogger<BoltServer> logger,
        BoltServerOptions options,
        Bolt.Server.Durable.IDurableQueueStore durableStore,
        Microsoft.Extensions.Options.IOptions<Bolt.Server.Durable.DurableQueueOptions> durableOptions,
        IEnumerable<IBoltTopicAuthorizer>? topicAuthorizers = null)
        : this(logger, options, topicAuthorizers)
    {
        _durableStore = durableStore;
        _durableOptions = durableOptions.Value;
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
        var hash = GetCommandHash(commandName);
        _localHandlers[hash] = handler;
    }

    /// <summary>
    /// Register a local handler that receives sender connection context.
    /// This is for hub-local infrastructure commands such as service discovery.
    /// </summary>
    public void RegisterHandler(
        string commandName,
        Func<BoltRequestContext, ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = GetCommandHash(commandName);
        _contextLocalHandlers[hash] = handler;
    }

    private int GetCommandHash(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var hash = BoltCodec.Fnv1aHash(commandName);
        var existing = _commandNamesByHash.GetOrAdd(hash, commandName);
        if (!string.Equals(existing, commandName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Bolt command hash collision detected. hash={hash}, existing='{existing}', rejected='{commandName}'");
        }

        return hash;
    }

    public event Func<BoltClientConnectionEvent, CancellationToken, Task>? ClientRegistered;

    public event Func<BoltClientConnectionEvent, CancellationToken, Task>? ClientDisconnected;

    public Task HandleConnectionAsync(IBoltConnection transport, CancellationToken ct) =>
        HandleConnectionAsync(transport, user: null, ct);

    public async Task HandleConnectionAsync(IBoltConnection transport, ClaimsPrincipal? user, CancellationToken ct)
    {
        var connection = new BoltHubConnection(transport, _sendQueueCapacity, _sendEnqueueTimeoutMs)
        {
            User = user
        };
        connection.StartSendLoop(ct);
        var receiveBuffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
        byte[]? largeBuffer = null;

        try
        {
            while (transport.IsConnected && !ct.IsCancellationRequested)
            {
                var (bytesRead, endOfMessage) = await transport.ReceiveAsync(receiveBuffer.AsMemory(), ct);

                // (0, true) signals connection closed
                if (bytesRead == 0 && endOfMessage)
                    break;

                if (bytesRead == 0)
                    continue;

                if (bytesRead > _maxFrameBytes)
                {
                    _logger.LogWarning("Closing Bolt connection because frame fragment exceeded max size. bytes={Bytes} max={Max}", bytesRead, _maxFrameBytes);
                    break;
                }

                byte[] frameBytes;
                int totalLength;
                if (!endOfMessage)
                {
                    // Multi-frame: accumulate into growing pooled buffer (zero MemoryStream alloc)
                    var assembled = bytesRead;
                    var capacity = Math.Min(_maxFrameBytes, Math.Max(bytesRead * 4, 512 * 1024));
                    if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
                    largeBuffer = ArrayPool<byte>.Shared.Rent(capacity);
                    receiveBuffer.AsSpan(0, bytesRead).CopyTo(largeBuffer);

                    while (!endOfMessage)
                    {
                        (bytesRead, endOfMessage) = await transport.ReceiveAsync(receiveBuffer.AsMemory(), ct);
                        if (bytesRead == 0 && endOfMessage)
                            break;

                        if (bytesRead > _maxFrameBytes || assembled > _maxFrameBytes - bytesRead)
                        {
                            _logger.LogWarning(
                                "Closing Bolt connection because assembled frame exceeded max size. assembled={Assembled} next={Bytes} max={Max}",
                                assembled,
                                bytesRead,
                                _maxFrameBytes);
                            return;
                        }

                        if (assembled + bytesRead > largeBuffer.Length)
                        {
                            var newCapacity = Math.Min(_maxFrameBytes, Math.Max(assembled + bytesRead, largeBuffer.Length * 2));
                            var newBuf = ArrayPool<byte>.Shared.Rent(newCapacity);
                            largeBuffer.AsSpan(0, assembled).CopyTo(newBuf);
                            ArrayPool<byte>.Shared.Return(largeBuffer);
                            largeBuffer = newBuf;
                        }
                        receiveBuffer.AsSpan(0, bytesRead).CopyTo(largeBuffer.AsSpan(assembled));
                        assembled += bytesRead;
                    }
                    frameBytes = largeBuffer;
                    totalLength = assembled;
                }
                else
                {
                    frameBytes = receiveBuffer;
                    totalLength = bytesRead;
                }

                if (totalLength <= 0 || totalLength > _maxFrameBytes)
                {
                    _logger.LogWarning("Closing Bolt connection because frame size was invalid. size={Size} max={Max}", totalLength, _maxFrameBytes);
                    break;
                }

                await ProcessFrameAsync(connection, frameBytes, totalLength, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection for client {ClientId}", connection.ClientId ?? "unregistered");
        }
        finally
        {
            connection.CompleteSendChannel();
            if (connection.SendLoop is not null)
                try { await connection.SendLoop; } catch { }
            ArrayPool<byte>.Shared.Return(receiveBuffer);
            if (largeBuffer != null) ArrayPool<byte>.Shared.Return(largeBuffer);
            await RemoveConnectionAsync(connection);

            try { await transport.CloseAsync(); }
            catch { }
        }
    }

    private async Task ProcessFrameAsync(BoltHubConnection connection, byte[] buffer, int length, CancellationToken ct)
    {
        var frameType = (FrameType)buffer[0];

        if (frameType != FrameType.Register && !connection.IsRegistered)
        {
            _logger.LogWarning("Closing unregistered Bolt connection after {FrameType} frame", frameType);
            await connection.CloseAsync(ct);
            return;
        }

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
            case FrameType.Subscribe:
                await HandleSubscribeFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.Unsubscribe:
                await HandleUnsubscribeFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.Publish:
                await HandlePublishFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.Ack:
                await HandleAckFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.StreamOpen:
                HandleStreamOpen(connection, buffer, length);
                await RouteStreamFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.StreamData:
                await RouteStreamFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.StreamClose:
                await RouteStreamFrameAsync(connection, buffer, length, ct);
                CleanupStream(connection, buffer, length);
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
        if (connection.IsRegistered)
        {
            _logger.LogWarning("Rejected duplicate Bolt register frame from {ClientId}", connection.ClientId);
            await connection.CloseAsync(ct);
            return;
        }

        if (!BoltCodec.TryReadRegister(buffer.AsSpan(0, length), out var clientId, out var clientName, out _))
        {
            _logger.LogWarning("Invalid register frame");
            return;
        }

        if (!ValidateRegisterIdentity(connection, clientId, clientName))
        {
            _logger.LogWarning(
                "Rejected Bolt register identity. stream={StreamId} clientId={ClientId} clientName={ClientName}",
                connection.StreamId,
                clientId,
                clientName);

            var rejectWriter = new ArrayBufferWriter<byte>(2);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAsync(rejectWriter.WrittenMemory, ct);
            await connection.CloseAsync(ct);
            return;
        }

        var serviceHash = BoltCodec.Fnv1aHash(clientId);
        var existingClientId = _clientIdsByServiceHash.GetOrAdd(serviceHash, clientId);
        if (!string.Equals(existingClientId, clientId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Rejected Bolt register identity because service hash collision was detected. hash={ServiceHash} existingClientId={ExistingClientId} rejectedClientId={RejectedClientId}",
                serviceHash,
                existingClientId,
                clientId);

            var rejectWriter = new ArrayBufferWriter<byte>(2);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAsync(rejectWriter.WrittenMemory, ct);
            await connection.CloseAsync(ct);
            return;
        }

        connection.ClientId = clientId;
        connection.ClientName = clientName;
        connection.ServiceHash = serviceHash;
        connection.IsRegistered = true;

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

        await NotifyClientRegisteredAsync(connection, ct);
    }

    private async Task HandleRequestAsync(BoltHubConnection caller, byte[] buffer, int length, CancellationToken ct)
    {
        var span = buffer.AsSpan(0, length);

        if (!BoltCodec.TryReadRequest(span, out var frame, out var consumed))
        {
            _logger.LogWarning("Invalid request frame from {ClientId}", caller.ClientId);
            return;
        }

        if (frame.SenderHash != caller.ServiceHash)
        {
            _logger.LogWarning(
                "Rejected request with spoofed sender hash. client={ClientId} expected={ExpectedHash} actual={ActualHash}",
                caller.ClientId,
                caller.ServiceHash,
                frame.SenderHash);
            return;
        }

        // Check for local handler first (direct mode - server handles request itself)
        if (_contextLocalHandlers.Count > 0 || _localHandlers.Count > 0)
        {
            if (_contextLocalHandlers.TryGetValue(frame.CommandHash, out var contextHandler))
            {
                try
                {
                    var payload = frame.GetPayload(buffer.AsMemory(0, length));
                    var context = BoltRequestContext.FromConnection(caller);
                    var (statusCode, responsePayload) = await contextHandler(context, payload, frame.RequestId);

                    var writer = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteResponse(writer, frame.RequestId, statusCode, responsePayload.Span);
                    await caller.SendAsync(writer.WrittenMemory, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Context local handler error for command hash {Hash}", frame.CommandHash);
                    var errWriter = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.InternalServerError, ReadOnlySpan<byte>.Empty);
                    await caller.SendAsync(errWriter.WrittenMemory, ct);
                }
                return;
            }

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

        var recipient = GetRecipient(frame.RecipientHash);
        if (recipient is null)
        {
            var errWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.NotFound, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter.WrittenMemory, ct);
            return;
        }

        if (!_pendingInvocations.TryAdd(frame.RequestId, new PendingInvocation(caller, recipient, Environment.TickCount64)))
        {
            var errWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.Conflict, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter.WrittenMemory, ct);
            return;
        }

        try
        {
            await recipient.SendAsync(buffer.AsMemory(0, consumed), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _pendingInvocations.TryRemove(frame.RequestId, out _);
            _logger.LogWarning(
                ex,
                "Failed to enqueue routed request {RequestId} to recipient {Recipient}",
                frame.RequestId,
                recipient.ClientId);

            var errWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.ServiceUnavailable, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter.WrittenMemory, ct);
        }
    }

    private async Task HandleResponseAsync(BoltHubConnection responder, byte[] buffer, int length, CancellationToken ct)
    {
        // Header-only read — extract RequestId for routing without touching payload
        if (!BoltCodec.TryReadResponseHeader(buffer.AsSpan(0, length), out var requestId, out var totalSize))
        {
            _logger.LogWarning("Invalid response frame from {ClientId}", responder.ClientId);
            return;
        }

        if (_pendingInvocations.TryGetValue(requestId, out var pending))
        {
            if (pending.ExpectedResponder.StreamId != responder.StreamId)
            {
                _logger.LogWarning(
                    "Rejected response from unexpected responder. requestId={RequestId} expected={Expected} actual={Actual}",
                    requestId,
                    pending.ExpectedResponder.ClientId,
                    responder.ClientId);
                return;
            }

            if (_pendingInvocations.TryRemove(requestId, out var removed))
                await removed.Caller.SendAsync(buffer.AsMemory(0, totalSize), ct);
        }
    }

    private async Task HandlePushAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadRequest(buffer.AsSpan(0, length), out var frame, out var totalSize))
        {
            _logger.LogWarning("Invalid push frame from {ClientId}", sender.ClientId);
            return;
        }

        if (frame.SenderHash != sender.ServiceHash)
        {
            _logger.LogWarning(
                "Rejected push with spoofed sender hash. client={ClientId} expected={ExpectedHash} actual={ActualHash}",
                sender.ClientId,
                sender.ServiceHash,
                frame.SenderHash);
            return;
        }

        // Broadcast: recipientHash == 0 → send to all connected clients (except sender)
        if (frame.CommandHash == LargeRpcResponseHash)
        {
            await RouteLargeRpcResponsePushAsync(sender, frame, buffer, totalSize, ct);
            return;
        }

        if (frame.RecipientHash == 0)
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

        var recipient = GetRecipient(frame.RecipientHash);
        if (recipient is not null && !recipient.IsUnderPressure)
            await recipient.SendAsync(buffer.AsMemory(0, totalSize), ct);
    }

    private async Task RouteLargeRpcResponsePushAsync(
        BoltHubConnection sender,
        RequestFrame frame,
        byte[] buffer,
        int totalSize,
        CancellationToken ct)
    {
        var payload = frame.GetPayload(buffer.AsMemory(0, totalSize));
        if (payload.Length < 18)
        {
            _logger.LogWarning("Rejected malformed large RPC response push from {ClientId}", sender.ClientId);
            return;
        }

        var requestId = new Guid(payload.Span[..16]);
        if (!_pendingInvocations.TryGetValue(requestId, out var pending))
        {
            _logger.LogWarning(
                "Rejected large RPC response push without pending invocation. requestId={RequestId} sender={ClientId}",
                requestId,
                sender.ClientId);
            return;
        }

        if (pending.ExpectedResponder.StreamId != sender.StreamId ||
            pending.Caller.ServiceHash != frame.RecipientHash)
        {
            _logger.LogWarning(
                "Rejected large RPC response push from unexpected responder or recipient. requestId={RequestId} sender={Sender} recipientHash={RecipientHash}",
                requestId,
                sender.ClientId,
                frame.RecipientHash);
            return;
        }

        if (_pendingInvocations.TryRemove(requestId, out var removed))
            await removed.Caller.SendAsync(buffer.AsMemory(0, totalSize), ct);
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
        if (!BoltCodec.TryReadStreamOpen(buffer.AsSpan(0, length), out var streamId, out var recipientHash, out var commandHash))
            return;

        var recipient = GetRecipient(recipientHash);
        if (recipient is not null)
        {
            _activeStreams[streamId] = new StreamRoute(sender, recipient, commandHash);
            _logger.LogDebug("Stream opened: {StreamId} from {Sender} to {Recipient}",
                streamId, sender.ClientId, recipient.ClientId);
        }
    }

    /// <summary>
    /// Route a stream frame (Data or Close) to the correct peer.
    /// If the sender is the stream's Sender, forward to Recipient and vice versa.
    /// </summary>
    private async Task RouteStreamFrameAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (length < 17) return; // Need at least 1 byte type + 16 bytes streamId

        if ((FrameType)buffer[0] == FrameType.StreamClose &&
            !BoltCodec.TryReadStreamClose(buffer.AsSpan(0, length), out _, out _))
        {
            _logger.LogWarning("Rejected truncated stream close from {ClientId}", sender.ClientId);
            return;
        }

        var streamId = BoltCodec.ReadStreamId(buffer.AsSpan(0, length));

        if (!_activeStreams.TryGetValue(streamId, out var peers))
            return;

        // Forward raw bytes to the other side — zero decode, zero copy
        // Determine direction: if frame came from the sender, forward to recipient and vice versa
        // Since we can't easily tell which connection this came from in this method,
        // forward to both peers (the one that sent it will ignore its own frame in its receive loop)
        // Actually, we route based on the stream open: sender→recipient for data, recipient→sender for data back
        // For simplicity, forward to recipient (sender initiated the stream)
        if ((FrameType)buffer[0] == FrameType.StreamData &&
            !TryAuthorizeStreamData(sender, peers, buffer, length))
        {
            return;
        }

        BoltHubConnection? recipient = null;
        if (peers.Sender.StreamId == sender.StreamId)
            recipient = peers.Recipient;
        else if (peers.Recipient.StreamId == sender.StreamId)
            recipient = peers.Sender;

        if (recipient is not { IsAlive: true })
            return;

        await recipient.SendAsync(buffer.AsMemory(0, length), ct);
    }

    private bool TryAuthorizeStreamData(BoltHubConnection sender, StreamRoute route, byte[] buffer, int length)
    {
        if (route.CommandHash == LargeRpcCommandHash && route.Sender.StreamId == sender.StreamId)
            return TryTrackLargeRpcRequest(route, buffer, length);

        if (route.CommandHash == LargeRpcResponseStreamHash)
        {
            if (route.Sender.StreamId != sender.StreamId)
            {
                _logger.LogWarning("Rejected large RPC response stream data from non-owner {ClientId}", sender.ClientId);
                return false;
            }

            return TryValidateLargeRpcResponseStream(route, buffer, length);
        }

        return true;
    }

    private bool TryTrackLargeRpcRequest(StreamRoute route, byte[] buffer, int length)
    {
        lock (route.SyncRoot)
        {
            if (route.LargeRpcRequestTracked)
                return true;

            if (!BoltCodec.TryReadStreamData(buffer.AsSpan(0, length), out _, out var payloadOffset, out var payloadLength, out _))
                return false;

            if (payloadLength < 28)
            {
                _logger.LogWarning("Rejected malformed large RPC request header from {ClientId}", route.Sender.ClientId);
                return false;
            }

            var payload = buffer.AsSpan(payloadOffset, payloadLength);
            var requestId = new Guid(payload[..16]);
            var metadataSenderHash = BinaryPrimitives.ReadInt32LittleEndian(payload[24..]);
            if (metadataSenderHash != route.Sender.ServiceHash)
            {
                _logger.LogWarning(
                    "Rejected large RPC request with spoofed metadata sender hash. requestId={RequestId} client={ClientId}",
                    requestId,
                    route.Sender.ClientId);
                return false;
            }

            if (!_pendingInvocations.TryAdd(requestId, new PendingInvocation(route.Sender, route.Recipient, Environment.TickCount64)))
            {
                _logger.LogWarning(
                    "Rejected large RPC request with duplicate request id. requestId={RequestId} client={ClientId}",
                    requestId,
                    route.Sender.ClientId);
                return false;
            }

            route.LargeRpcRequestTracked = true;
            return true;
        }
    }

    private bool TryValidateLargeRpcResponseStream(StreamRoute route, byte[] buffer, int length)
    {
        lock (route.SyncRoot)
        {
            if (route.LargeRpcResponseValidated)
                return true;

            if (!BoltCodec.TryReadStreamData(buffer.AsSpan(0, length), out _, out var payloadOffset, out var payloadLength, out _))
                return false;

            if (payloadLength < 22)
            {
                _logger.LogWarning("Rejected malformed large RPC response stream header from {ClientId}", route.Sender.ClientId);
                return false;
            }

            var payload = buffer.AsSpan(payloadOffset, payloadLength);
            var requestId = new Guid(payload[..16]);
            if (!_pendingInvocations.TryGetValue(requestId, out var pending))
            {
                _logger.LogWarning(
                    "Rejected large RPC response stream without pending invocation. requestId={RequestId} sender={ClientId}",
                    requestId,
                    route.Sender.ClientId);
                return false;
            }

            if (pending.ExpectedResponder.StreamId != route.Sender.StreamId ||
                pending.Caller.ServiceHash != route.Recipient.ServiceHash)
            {
                _logger.LogWarning(
                    "Rejected large RPC response stream from unexpected responder or recipient. requestId={RequestId} sender={Sender}",
                    requestId,
                    route.Sender.ClientId);
                return false;
            }

            if (!_pendingInvocations.TryRemove(requestId, out _))
                return false;

            route.LargeRpcResponseValidated = true;
            return true;
        }
    }

    private void CleanupStream(BoltHubConnection sender, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadStreamClose(buffer.AsSpan(0, length), out var streamId, out _))
            return;

        if (!_activeStreams.TryGetValue(streamId, out var peers))
            return;

        if (peers.Sender.StreamId != sender.StreamId && peers.Recipient.StreamId != sender.StreamId)
        {
            _logger.LogWarning(
                "Rejected stream close from nonparticipant. streamId={StreamId} client={ClientId}",
                streamId,
                sender.ClientId);
            return;
        }

        _activeStreams.TryRemove(streamId, out _);
    }

    // ── Media frame routing ──

    private static bool IsSameConnection(BoltHubConnection? left, BoltHubConnection right)
        => left is not null && left.StreamId == right.StreamId;

    private static bool IsCallParticipant(ServerCallState callState, BoltHubConnection connection)
    {
        lock (callState.Participants)
        {
            return callState.Participants.Any(p => p.StreamId == connection.StreamId);
        }
    }

    private static List<BoltHubConnection> GetParticipantSnapshot(ServerCallState callState)
    {
        lock (callState.Participants)
        {
            return new List<BoltHubConnection>(callState.Participants);
        }
    }

    private static bool TryAddCallParticipant(ServerCallState callState, BoltHubConnection connection)
    {
        lock (callState.Participants)
        {
            if (callState.Participants.Any(p => p.StreamId == connection.StreamId))
                return false;

            callState.Participants.Add(connection);
            return true;
        }
    }

    private static bool TryRemoveCallParticipantByServiceHash(ServerCallState callState, int serviceHash, out string? removedClientId)
    {
        lock (callState.Participants)
        {
            var idx = callState.Participants.FindIndex(p => p.ServiceHash == serviceHash);
            if (idx < 0)
            {
                removedClientId = null;
                return false;
            }

            removedClientId = callState.Participants[idx].ClientId;
            callState.Participants.RemoveAt(idx);
            return true;
        }
    }

    private static bool IsCallMediaActive(ServerCallState callState)
        => callState.Status is ServerCallStatus.Active or ServerCallStatus.Held;

    private static bool IsCallOwner(ServerCallState callState, BoltHubConnection connection)
        => callState.CallerConnection.StreamId == connection.StreamId;

    private static List<Guid> GetMediaStreamSnapshot(ServerCallState callState)
    {
        lock (callState.MediaStreamIds)
        {
            return new List<Guid>(callState.MediaStreamIds);
        }
    }

    private static void AddMediaStream(ServerCallState callState, Guid streamId)
    {
        lock (callState.MediaStreamIds)
        {
            if (!callState.MediaStreamIds.Contains(streamId))
                callState.MediaStreamIds.Add(streamId);
        }
    }

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

        if (route.Sender.StreamId != sender.StreamId)
        {
            _logger.LogWarning(
                "Rejected media frame from non-owner. streamId={StreamId} owner={Owner} sender={Sender}",
                streamId,
                route.Sender.ClientId,
                sender.ClientId);
            return;
        }

        if (!_activeCalls.TryGetValue(route.CallId, out var owningCall) ||
            !IsCallMediaActive(owningCall) ||
            !IsCallParticipant(owningCall, sender))
        {
            _logger.LogWarning(
                "Rejected media frame for inactive or unauthorized call. streamId={StreamId} call={CallId} sender={Sender}",
                streamId,
                route.CallId,
                sender.ClientId);
            return;
        }

        var data = buffer.AsMemory(0, length);

        // Simulcast-aware routing: if this stream has a layer ID, only forward to
        // recipients whose preferred layer matches (or who have no preference = forward all)
        var isSimulcast = route.SimulcastLayerId.HasValue;

        foreach (var recipient in route.GetRecipientSnapshot())
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

        if (!_activeCalls.TryGetValue(config.CallId, out var callState) ||
            !IsCallMediaActive(callState) ||
            !IsCallParticipant(callState, sender))
        {
            _logger.LogWarning(
                "Rejected MediaConfig for inactive or unauthorized call. streamId={StreamId} call={CallId} sender={Sender}",
                config.StreamId,
                config.CallId,
                sender.ClientId);
            return;
        }

        // Register or update the media stream route.
        var route = _activeMediaStreams.GetOrAdd(config.StreamId, _ => new MediaStreamRoute
        {
            Sender = sender,
            CallId = config.CallId,
        });

        if (route.Sender.StreamId != sender.StreamId || route.CallId != config.CallId)
        {
            _logger.LogWarning(
                "Rejected MediaConfig that attempted to reuse a stream route. streamId={StreamId} owner={Owner} sender={Sender}",
                config.StreamId,
                route.Sender.ClientId,
                sender.ClientId);
            return;
        }

        AddMediaStream(callState, config.StreamId);

        // Add all call participants (except sender) as recipients
        route.AddRecipients(GetParticipantSnapshot(callState));

        // Forward config to all recipients
        var data = buffer.AsMemory(0, length);
        foreach (var recipient in route.GetRecipientSnapshot())
        {
            if (recipient.IsAlive && IsCallParticipant(callState, recipient))
            {
                await recipient.SendAsync(data, ct);
            }
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

        if (route.Sender.StreamId == sender.StreamId ||
            !route.ContainsRecipient(sender) ||
            !_activeCalls.TryGetValue(route.CallId, out var owningCall) ||
            !IsCallMediaActive(owningCall) ||
            !IsCallParticipant(owningCall, sender))
        {
            _logger.LogWarning(
                "Rejected media feedback from unauthorized sender. streamId={StreamId} call={CallId} sender={Sender}",
                streamId,
                route.CallId,
                sender.ClientId);
            return;
        }

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
            case SignalType.KeyExchange:
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

        if (callee.StreamId == caller.StreamId)
        {
            _logger.LogWarning("Rejected self-call initiate from {ClientId}", caller.ClientId);
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

        if (!_activeCalls.TryAdd(header.CallId, callState))
        {
            _logger.LogWarning(
                "Rejected CallSignal Initiate with duplicate call id. call={CallId} sender={ClientId}",
                header.CallId,
                caller.ClientId);
            return;
        }

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

        if (!IsSameConnection(callState.CalleeConnection, sender) || callState.Status != ServerCallStatus.Ringing)
        {
            _logger.LogWarning(
                "Rejected unauthorized CallSignal Answer. call={CallId} sender={ClientId}",
                header.CallId,
                sender.ClientId);
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

        if (!IsSameConnection(callState.CalleeConnection, sender) || callState.Status != ServerCallStatus.Ringing)
        {
            _logger.LogWarning(
                "Rejected unauthorized CallSignal Reject. call={CallId} sender={ClientId}",
                header.CallId,
                sender.ClientId);
            return;
        }

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

        if (!IsCallParticipant(callState, sender))
        {
            _logger.LogWarning(
                "Rejected unauthorized CallSignal End. call={CallId} sender={ClientId}",
                header.CallId,
                sender.ClientId);
            return;
        }

        callState.Status = ServerCallStatus.Ended;

        // Forward End to all other participants (supports group calls)
        var data = buffer.AsMemory(0, length);
        foreach (var participant in GetParticipantSnapshot(callState))
        {
            if (participant.StreamId != sender.StreamId && participant.IsAlive)
                _ = participant.SendAsync(data, ct);
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

        if (!IsCallParticipant(callState, sender) || callState.Status is ServerCallStatus.Ringing or ServerCallStatus.Ended or ServerCallStatus.Rejected)
        {
            _logger.LogWarning(
                "Rejected unauthorized CallSignal {SignalType}. call={CallId} sender={ClientId}",
                header.SignalType,
                header.CallId,
                sender.ClientId);
            return;
        }

        callState.Status = header.SignalType == SignalType.Hold ? ServerCallStatus.Held : ServerCallStatus.Active;

        var data = buffer.AsMemory(0, length);
        foreach (var participant in GetParticipantSnapshot(callState))
        {
            if (participant.StreamId != sender.StreamId && participant.IsAlive)
                _ = participant.SendAsync(data, ct);
        }
    }

    /// <summary>
    /// Pure relay for DirectOffer/DirectAnswer: forward to the other party without state changes.
    /// </summary>
    private async Task RelayCallSignalAsync(BoltHubConnection sender, byte[] buffer, int length, CallSignalHeader header, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(header.CallId, out var callState))
            return;

        if (!IsCallParticipant(callState, sender) || !IsCallMediaActive(callState))
        {
            _logger.LogWarning(
                "Rejected unauthorized CallSignal {SignalType}. call={CallId} sender={ClientId}",
                header.SignalType,
                header.CallId,
                sender.ClientId);
            return;
        }

        var data = buffer.AsMemory(0, length);
        foreach (var participant in GetParticipantSnapshot(callState))
        {
            if (participant.StreamId != sender.StreamId && participant.IsAlive)
                _ = participant.SendAsync(data, ct);
        }
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

        if (!IsCallOwner(callState, sender) || !IsCallMediaActive(callState))
        {
            _logger.LogWarning(
                "Rejected unauthorized CallSignal AddParticipant. call={CallId} sender={ClientId}",
                header.CallId,
                sender.ClientId);
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

        if (!TryAddCallParticipant(callState, newParticipant))
        {
            _logger.LogDebug(
                "Call {CallId} AddParticipant ignored existing participant {Participant}",
                header.CallId,
                newParticipant.ClientId);
            return;
        }

        // Add to all existing media stream routes as a recipient + request keyframes from senders
        foreach (var streamId in GetMediaStreamSnapshot(callState))
        {
            if (!_activeMediaStreams.TryGetValue(streamId, out var route))
                continue;

            // Add new participant as recipient (if not already present and not the sender)
            if (route.Sender.StreamId != newParticipant.StreamId)
                route.AddRecipient(newParticipant);

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
        foreach (var participant in GetParticipantSnapshot(callState))
        {
            if (participant.StreamId != sender.StreamId && participant.StreamId != newParticipant.StreamId && participant.IsAlive)
                _ = participant.SendAsync(data, ct);
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

        if (!IsCallOwner(callState, sender) || !IsCallMediaActive(callState))
        {
            _logger.LogWarning(
                "Rejected unauthorized CallSignal RemoveParticipant. call={CallId} sender={ClientId}",
                header.CallId,
                sender.ClientId);
            return;
        }

        if (header.PayloadLength < 4)
        {
            _logger.LogWarning("CallSignal RemoveParticipant from {ClientId} has no recipient hash in payload", sender.ClientId);
            return;
        }

        var recipientHash = BinaryPrimitives.ReadInt32LittleEndian(
            buffer.AsSpan(header.PayloadOffset, 4));

        if (!TryRemoveCallParticipantByServiceHash(callState, recipientHash, out var removedClientId))
            return;

        // Remove from all media stream recipient lists
        foreach (var streamId in GetMediaStreamSnapshot(callState))
        {
            if (_activeMediaStreams.TryGetValue(streamId, out var route))
            {
                if (route.Sender.ServiceHash == recipientHash)
                    _activeMediaStreams.TryRemove(streamId, out _);
                else
                    route.RemoveRecipientsWhere(r => r.ServiceHash == recipientHash);
            }
        }

        // Forward the RemoveParticipant signal to remaining participants
        var data = buffer.AsMemory(0, length);
        foreach (var participant in GetParticipantSnapshot(callState))
        {
            if (participant.StreamId != sender.StreamId && participant.IsAlive)
                _ = participant.SendAsync(data, ct);
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
            foreach (var streamId in GetMediaStreamSnapshot(callState))
                _activeMediaStreams.TryRemove(streamId, out _);
        }
    }

    // ── Pub/Sub handlers ──

    private async Task HandleSubscribeFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadSubscribe(buffer.AsSpan(0, length), out var topicHash, out var durable, out var subscriberId, out var topic, out var actorAccessToken, out _))
            return;

        if (!TryRegisterTopicName(topicHash, topic))
            return;

        if (!await AuthorizeTopicAsync(
                conn,
                BoltTopicOperation.Subscribe,
                topic,
                topicHash,
                durable,
                subscriberId,
                actorAccessToken,
                ct))
        {
            _logger.LogWarning(
                "Rejected unauthorized Bolt subscription. client={ClientId} topic={Topic} durable={Durable}",
                conn.ClientId,
                topic,
                durable);
            return;
        }

        if (!durable)
        {
            AddLiveSubscription(conn, topicHash);
            _logger.LogDebug("Transient subscribe: topic={Topic}", topic);
            return;
        }

        if (_durableStore is null)
        {
            _logger.LogWarning("Durable subscribe requested but no IDurableQueueStore configured. Falling back to transient.");
            AddLiveSubscription(conn, topicHash);
            return;
        }

        // Durable: register subscriber and mark this connection as the current
        // session immediately, but buffer live publishes until replay drains so
        // live frames cannot overtake older replay frames.
        await _durableStore.RegisterDurableSubscriberAsync(topicHash, subscriberId, ct);
        var durableKey = (topicHash, subscriberId);
        _liveDurableConnections[durableKey] = conn;
        _replayingDurableSubscriptions[durableKey] = new DurableReplayState();

        var fromSequence = await _durableStore.GetLastAckedSequenceAsync(topicHash, subscriberId, ct);
        var maxBatch = _durableOptions?.MaxReplayBatchSize ?? 1000;
        var replayCount = 0;
        var replayedBatchCount = 0;
        do
        {
            replayedBatchCount = 0;
            await foreach (var (seq, payload) in _durableStore.ReadFromAsync(topicHash, subscriberId, fromSequence, maxBatch, ct))
            {
                if (!_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection) ||
                    !ReferenceEquals(mappedConnection, conn))
                    return;

                var w = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteEvent(w, topicHash, subscriberId, seq, isReplay: true, payload);
                await conn.SendAsync(w.WrittenMemory, ct);
                w.Reset();
                fromSequence = seq;
                replayCount++;
                replayedBatchCount++;
            }
        }
        while (replayedBatchCount == maxBatch && !ct.IsCancellationRequested);

        await CompleteDurableReplayAsync(conn, topicHash, subscriberId, fromSequence, ct);

        _logger.LogDebug("Durable subscribe: topic={Topic} subscriber={Subscriber} replayed={Count}", topic, subscriberId, replayCount);
    }

    private void AddLiveSubscription(BoltHubConnection conn, int topicHash)
    {
        var topicSet = _liveSubscribersByTopic.GetOrAdd(topicHash, _ => new ConcurrentDictionary<BoltHubConnection, byte>());
        topicSet.TryAdd(conn, 0);

        var connSet = _liveSubscriptionsByConnection.GetOrAdd(conn, _ => new ConcurrentDictionary<int, byte>());
        connSet.TryAdd(topicHash, 0);
    }

    private async Task CompleteDurableReplayAsync(
        BoltHubConnection conn,
        int topicHash,
        string subscriberId,
        long lastReplayedSequence,
        CancellationToken ct)
    {
        var durableKey = (topicHash, subscriberId);
        while (!ct.IsCancellationRequested)
        {
            if (!_replayingDurableSubscriptions.TryGetValue(durableKey, out var state))
                return;

            var events = new List<(long Sequence, byte[] Payload)>();
            lock (state.SyncRoot)
            {
                while (state.DeferredEvents.TryDequeue(out var item))
                    events.Add(item);

                if (events.Count == 0)
                {
                    state.AcceptingDeferredEvents = false;
                    _replayingDurableSubscriptions.TryRemove(durableKey, out _);
                    return;
                }
            }

            if (!_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection) ||
                !ReferenceEquals(mappedConnection, conn))
                return;

            foreach (var (sequence, payload) in events
                         .OrderBy(item => item.Sequence == 0 ? long.MaxValue : item.Sequence))
            {
                if (sequence > 0 && sequence <= lastReplayedSequence)
                    continue;

                if (!_liveDurableConnections.TryGetValue(durableKey, out mappedConnection) ||
                    !ReferenceEquals(mappedConnection, conn))
                    return;

                var w = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteEvent(w, topicHash, subscriberId, sequence, isReplay: false, payload);
                await conn.SendAsync(w.WrittenMemory, ct);
                w.Reset();
                if (sequence > 0)
                    lastReplayedSequence = sequence;
            }
        }
    }

    private async Task HandleUnsubscribeFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadUnsubscribe(buffer.AsSpan(0, length), out var topicHash, out var topic, out var subscriberId, out var permanent, out var actorAccessToken, out _))
            return;

        if (!TryRegisterTopicName(topicHash, topic))
            return;

        if (!await AuthorizeTopicAsync(
                conn,
                BoltTopicOperation.Unsubscribe,
                topic,
                topicHash,
                durable: !string.Equals(subscriberId, conn.ClientId, StringComparison.Ordinal),
                subscriberId,
                actorAccessToken,
                ct))
        {
            _logger.LogWarning(
                "Rejected unauthorized Bolt unsubscribe. client={ClientId} topic={Topic} subscriber={Subscriber}",
                conn.ClientId,
                topic,
                subscriberId);
            return;
        }

        if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSet))
            topicSet.TryRemove(conn, out _);

        if (_liveSubscriptionsByConnection.TryGetValue(conn, out var connSet))
            connSet.TryRemove(topicHash, out _);

        var durableKey = (topicHash, subscriberId);
        if (_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection) &&
            ReferenceEquals(mappedConnection, conn))
        {
            _liveDurableConnections.TryRemove(durableKey, out _);
            _replayingDurableSubscriptions.TryRemove(durableKey, out _);
            if (permanent && _durableStore is not null)
                await _durableStore.UnregisterDurableSubscriberAsync(topicHash, subscriberId, ct);
        }

        _logger.LogDebug("Unsubscribe: topic={Topic} subscriber={Subscriber}", topic, subscriberId);
    }

    private async Task HandlePublishFrameAsync(BoltHubConnection publisher, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadPublish(buffer.AsSpan(0, length), out var topicHash, out var topic, out var durableEligible, out var payloadOffset, out var payloadLength, out _))
            return;

        if (!TryRegisterTopicName(topicHash, topic))
            return;

        if (!await AuthorizeTopicAsync(
                publisher,
                BoltTopicOperation.Publish,
                topic,
                topicHash,
                durableEligible,
                subscriberId: null,
                actorAccessToken: null,
                ct))
        {
            _logger.LogWarning(
                "Rejected unauthorized Bolt publish. client={ClientId} topicHash={TopicHash}",
                publisher.ClientId,
                topicHash);
            return;
        }

        var payload = new byte[payloadLength];
        buffer.AsSpan(payloadOffset, payloadLength).CopyTo(payload);

        var deliveredConnections = new HashSet<BoltHubConnection>();

        // Durable path: enqueue and deliver live if connected
        if (durableEligible && _durableStore is not null)
        {
            var durableSubs = await _durableStore.GetDurableSubscribersAsync(topicHash, ct);
            foreach (var subscriberId in durableSubs)
            {
                var durableKey = (topicHash, subscriberId);
                var publishLock = _durablePublishLocks.GetOrAdd(durableKey, _ => new SemaphoreSlim(1, 1));

                await publishLock.WaitAsync(ct);
                try
                {
                    long seq;
                    try
                    {
                        seq = await _durableStore.AppendAsync(topicHash, subscriberId, payload, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Durable append failed for topic={TopicHash} subscriber={Subscriber}", topicHash, subscriberId);
                        continue;
                    }

                    if (_liveDurableConnections.TryGetValue(durableKey, out var liveConn) && liveConn != publisher)
                    {
                        if (_replayingDurableSubscriptions.TryGetValue(durableKey, out var replayState))
                        {
                            var deferred = false;
                            lock (replayState.SyncRoot)
                            {
                                if (replayState.AcceptingDeferredEvents)
                                {
                                    replayState.DeferredEvents.Enqueue((seq, payload));
                                    deferred = true;
                                }
                            }

                            if (deferred)
                                continue;
                        }

                        var w = RentedBufferWriter.GetThreadLocal();
                        BoltCodec.WriteEvent(w, topicHash, subscriberId, seq, isReplay: false, payload);
                        try { await liveConn.SendAsync(w.WrittenMemory, ct); }
                        catch { }
                        w.Reset();
                        deliveredConnections.Add(liveConn);
                    }
                }
                finally
                {
                    publishLock.Release();
                }
            }
        }
        else
        {
            foreach (var (durableKey, liveConn) in _liveDurableConnections)
            {
                if (durableKey.TopicHash != topicHash || liveConn == publisher)
                    continue;

                if (_replayingDurableSubscriptions.TryGetValue(durableKey, out var replayState))
                {
                    var deferred = false;
                    lock (replayState.SyncRoot)
                    {
                        if (replayState.AcceptingDeferredEvents)
                        {
                            replayState.DeferredEvents.Enqueue((0, payload));
                            deferred = true;
                        }
                    }

                    if (deferred)
                        continue;
                }

                var w = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteEvent(w, topicHash, durableKey.SubscriberId, sequenceNumber: 0, isReplay: false, payload);
                try { await liveConn.SendAsync(w.WrittenMemory, ct); }
                catch { }
                w.Reset();
                deliveredConnections.Add(liveConn);
            }
        }

        // Live fan-out for transient subscribers (skip publisher and skip durable-already-delivered)
        if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSetForPublish))
        {
            foreach (var (subscriberConn, _) in topicSetForPublish)
            {
                if (subscriberConn == publisher) continue;
                if (deliveredConnections.Contains(subscriberConn)) continue;

                var w = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteEvent(w, topicHash, sequenceNumber: 0, isReplay: false, payload);
                try { await subscriberConn.SendAsync(w.WrittenMemory, ct); }
                catch { }
                w.Reset();
            }
        }
    }

    private async Task HandleAckFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadAck(buffer.AsSpan(0, length), out var topicHash, out var topic, out var subscriberId, out var upToSequence, out var actorAccessToken, out _))
            return;

        if (_durableStore is null) return;

        if (!TryRegisterTopicName(topicHash, topic))
            return;

        if (!await AuthorizeTopicAsync(
                conn,
                BoltTopicOperation.Ack,
                topic,
                topicHash,
                durable: true,
                subscriberId,
                actorAccessToken,
                ct))
        {
            _logger.LogWarning(
                "Rejected unauthorized Bolt ack. client={ClientId} topic={Topic} subscriber={Subscriber}",
                conn.ClientId,
                topic,
                subscriberId);
            return;
        }

        if (!_liveDurableConnections.TryGetValue((topicHash, subscriberId), out var mappedConnection) ||
            !ReferenceEquals(mappedConnection, conn))
        {
            _logger.LogWarning(
                "Rejected durable ack for non-current subscriber session. client={ClientId} topic={Topic} subscriber={Subscriber}",
                conn.ClientId,
                topic,
                subscriberId);
            return;
        }

        try
        {
            await _durableStore.AckAsync(topicHash, subscriberId, upToSequence, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Durable ack failed for topic={TopicHash} subscriber={Subscriber}", topicHash, subscriberId);
        }
    }

    private bool TryRegisterTopicName(int topicHash, string topic)
    {
        var existing = _topicNamesByHash.GetOrAdd(topicHash, topic);
        if (string.Equals(existing, topic, StringComparison.Ordinal))
            return true;

        _logger.LogWarning(
            "Rejected Bolt pub/sub frame because topic hash collision was detected. topicHash={TopicHash} existingTopic={ExistingTopic} rejectedTopic={RejectedTopic}",
            topicHash,
            existing,
            topic);
        return false;
    }

    private async ValueTask<bool> AuthorizeTopicAsync(
        BoltHubConnection connection,
        BoltTopicOperation operation,
        string? topic,
        int topicHash,
        bool durable,
        string? subscriberId,
        string? actorAccessToken,
        CancellationToken ct)
    {
        if (_topicAuthorizers.Count == 0)
            return true;

        var context = new BoltTopicAuthorizationContext(
            operation,
            topic,
            topicHash,
            durable,
            subscriberId,
            actorAccessToken,
            connection.StreamId,
            connection.ClientId,
            connection.ClientName,
            connection.User);

        foreach (var authorizer in _topicAuthorizers)
        {
            if (!await authorizer.AuthorizeAsync(context, ct))
                return false;
        }

        return true;
    }

    private bool ValidateRegisterIdentity(BoltHubConnection connection, string clientId, string clientName)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientName))
            return false;

        if (_registrationIdentityBindingMode == BoltRegistrationIdentityBindingMode.Off)
            return true;

        var user = connection.User;
        if (user?.Identity?.IsAuthenticated != true)
            return true;

        var hasServiceScope = HasRequiredServiceScope(user);
        var serviceClaim = ResolveServiceIdentityName(user);
        var isReservedServiceIdentity = IsReservedServiceIdentity(clientId, clientName);

        if (!hasServiceScope && !isReservedServiceIdentity)
            return true;

        string? rejectionReason = null;
        if (!hasServiceScope)
            rejectionReason = $"authenticated service registration requires scope '{_requiredServiceScope}'";
        else if (string.IsNullOrWhiteSpace(serviceClaim))
            rejectionReason = "authenticated service registration requires a service identity claim";
        else if (!string.Equals(serviceClaim, clientName, StringComparison.Ordinal))
            rejectionReason = "registered Bolt client name must match the authenticated service identity";
        else if (!string.Equals(clientId, Sha256Hex(serviceClaim), StringComparison.Ordinal))
            rejectionReason = "registered Bolt client id must be SHA256(authenticated service identity)";

        if (rejectionReason is null)
            return true;

        if (_registrationIdentityBindingMode == BoltRegistrationIdentityBindingMode.Audit)
        {
            _logger.LogWarning(
                "Bolt registration identity mismatch allowed in audit mode. reason={Reason} clientId={ClientId} clientName={ClientName} serviceClaim={ServiceClaim}",
                rejectionReason,
                clientId,
                clientName,
                serviceClaim);
            return true;
        }

        _logger.LogWarning(
            "Bolt registration identity mismatch rejected. reason={Reason} clientId={ClientId} clientName={ClientName} serviceClaim={ServiceClaim}",
            rejectionReason,
            clientId,
            clientName,
            serviceClaim);
        return false;
    }

    private bool IsReservedServiceIdentity(string clientId, string clientName) =>
        _reservedServiceNames.Contains(clientName) ||
        _reservedServiceClientIds.Contains(clientId) ||
        _reservedServiceNamePrefixes.Any(prefix => clientName.StartsWith(prefix, StringComparison.Ordinal));

    private bool HasRequiredServiceScope(ClaimsPrincipal user) =>
        user.Claims
            .Where(static claim =>
                string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(scope => string.Equals(scope, _requiredServiceScope, StringComparison.OrdinalIgnoreCase));

    private string? ResolveServiceIdentityName(ClaimsPrincipal user)
    {
        foreach (var claimType in _serviceIdentityClaimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static HashSet<string> NormalizeOptionSet(IEnumerable<string> values, StringComparer comparer) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToHashSet(comparer);

    private static string[] NormalizeOptionList(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

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

    private async Task RemoveConnectionAsync(BoltHubConnection connection)
    {
        if (connection.ClientId is not null)
        {
            _connectionsByStreamId.TryRemove(connection.StreamId, out _);

            if (_connectionsByServiceHash.TryGetValue(connection.ServiceHash, out var bag))
            {
                var updated = new ConcurrentBag<BoltHubConnection>(bag.Where(c => c.StreamId != connection.StreamId));
                if (updated.IsEmpty)
                {
                    _connectionsByServiceHash.TryRemove(connection.ServiceHash, out _);
                    _clientIdsByServiceHash.TryRemove(
                        new KeyValuePair<int, string>(connection.ServiceHash, connection.ClientId));
                }
                else
                {
                    _connectionsByServiceHash[connection.ServiceHash] = updated;
                }
            }

            _logger.LogInformation("Client disconnected: {ClientId} ({ClientName})", connection.ClientId, connection.ClientName);
            await NotifyClientDisconnectedAsync(connection, CancellationToken.None);
        }

        foreach (var (requestId, pending) in _pendingInvocations)
        {
            if (pending.Caller.StreamId == connection.StreamId || pending.ExpectedResponder.StreamId == connection.StreamId)
                _pendingInvocations.TryRemove(requestId, out _);
        }

        // End any active calls this connection is part of
        foreach (var (callId, callState) in _activeCalls)
        {
            if (!IsCallParticipant(callState, connection))
                continue;

            callState.Status = ServerCallStatus.Ended;

            // Notify the other participants.
            foreach (var participant in GetParticipantSnapshot(callState))
            {
                if (participant.StreamId == connection.StreamId || !participant.IsAlive)
                    continue;

                var writer = RentedBufferWriter.GetThreadLocal();
                try
                {
                    BoltCodec.WriteCallSignal(writer, callId, SignalType.End, ReadOnlySpan<byte>.Empty);
                    _ = participant.SendAsync(writer.WrittenMemory, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send End signal for call {CallId} during disconnect cleanup", callId);
                }
                finally { writer.Reset(); }
            }

            CleanupCall(callId);
        }

        // Remove connection from any media stream recipient lists
        foreach (var (streamId, route) in _activeMediaStreams)
        {
            route.RemoveRecipientsWhere(r => r.StreamId == connection.StreamId);

            // If sender disconnected, remove the whole route
            if (route.Sender.StreamId == connection.StreamId)
                _activeMediaStreams.TryRemove(streamId, out _);
        }

        // Clean up pub/sub subscriptions for this connection
        if (_liveSubscriptionsByConnection.TryRemove(connection, out var topics))
        {
            foreach (var (topicHash, _) in topics)
            {
                if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSet))
                    topicSet.TryRemove(connection, out _);
            }
        }

        // Remove this connection from any live durable bindings
        var keysToRemove = _liveDurableConnections.Where(kvp => kvp.Value == connection).Select(kvp => kvp.Key).ToList();
        foreach (var key in keysToRemove)
        {
            _liveDurableConnections.TryRemove(key, out _);
            _replayingDurableSubscriptions.TryRemove(key, out _);
        }
    }

    private async Task NotifyClientRegisteredAsync(BoltHubConnection connection, CancellationToken ct)
    {
        var handler = ClientRegistered;
        if (handler is null || connection.ClientId is null)
            return;

        var connectionEvent = BoltClientConnectionEvent.FromConnection(connection);
        foreach (Func<BoltClientConnectionEvent, CancellationToken, Task> subscriber in handler.GetInvocationList())
        {
            try
            {
                await subscriber(connectionEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client registered subscriber failed for {ClientId}", connection.ClientId);
            }
        }
    }

    private async Task NotifyClientDisconnectedAsync(BoltHubConnection connection, CancellationToken ct)
    {
        var handler = ClientDisconnected;
        if (handler is null || connection.ClientId is null)
            return;

        var connectionEvent = BoltClientConnectionEvent.FromConnection(connection);
        foreach (Func<BoltClientConnectionEvent, CancellationToken, Task> subscriber in handler.GetInvocationList())
        {
            try
            {
                await subscriber(connectionEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client disconnected subscriber failed for {ClientId}", connection.ClientId);
            }
        }
    }

    private void CleanupStaleInvocations(object? state)
    {
        var now = Environment.TickCount64;
        foreach (var (requestId, pending) in _pendingInvocations)
        {
            if (now - pending.Timestamp > _invocationTimeoutMs)
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

public sealed record BoltRequestContext(
    string ConnectionId,
    string? ClientId,
    string? ClientName,
    int ServiceHash,
    BoltTransport TransportType,
    ClaimsPrincipal? User)
{
    internal static BoltRequestContext FromConnection(BoltHubConnection connection) =>
        new(
            connection.StreamId,
            connection.ClientId,
            connection.ClientName,
            connection.ServiceHash,
            connection.TransportType,
            connection.User);
}

public sealed record BoltClientConnectionEvent(
    string ConnectionId,
    string ClientId,
    string? ClientName,
    int ServiceHash,
    BoltTransport TransportType,
    DateTime OccurredAt)
{
    internal static BoltClientConnectionEvent FromConnection(BoltHubConnection connection) =>
        new(
            connection.StreamId,
            connection.ClientId ?? string.Empty,
            connection.ClientName,
            connection.ServiceHash,
            connection.TransportType,
            DateTime.UtcNow);
}

internal sealed record PendingInvocation(
    BoltHubConnection Caller,
    BoltHubConnection ExpectedResponder,
    long Timestamp);

internal sealed class StreamRoute(BoltHubConnection sender, BoltHubConnection recipient, int commandHash)
{
    public BoltHubConnection Sender { get; } = sender;
    public BoltHubConnection Recipient { get; } = recipient;
    public int CommandHash { get; } = commandHash;
    public object SyncRoot { get; } = new();
    public bool LargeRpcRequestTracked { get; set; }
    public bool LargeRpcResponseValidated { get; set; }
}

/// <summary>
/// Server-side wrapper for a connected client's transport.
/// Uses a Channel-based send queue with a dedicated background send loop
/// so callers never block — writes go into the channel instantly.
/// The single-reader send loop drains the channel and writes to the transport
/// one at a time, eliminating lock contention for all transports.
/// </summary>
public sealed class BoltHubConnection
{
    private readonly IBoltConnection _transport;
    private readonly Channel<(byte[] Buffer, int Length)> _sendChannel;
    private readonly TimeSpan _sendEnqueueTimeout;
    public Task? SendLoop { get; private set; }

    public string StreamId { get; } = Guid.NewGuid().ToString("N");
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public ClaimsPrincipal? User { get; set; }
    public int ServiceHash { get; set; }
    public bool IsRegistered { get; set; }
    public bool IsAlive => _transport.IsConnected;
    public BoltTransport TransportType => _transport.TransportType;

    /// <summary>Pending bytes queued for this connection. Used for backpressure decisions.</summary>
    public long PendingBytes => Interlocked.Read(ref _pendingBytes);
    private long _pendingBytes;

    /// <summary>Backpressure threshold: drop media frames when pending exceeds this (1MB).</summary>
    public const long BackpressureDropThreshold = 1024 * 1024;

    /// <summary>Backpressure threshold: send feedback signal to reduce sender rate (2MB).</summary>
    public const long BackpressureFeedbackThreshold = 2 * 1024 * 1024;

    /// <summary>True if this connection is under backpressure (pending > drop threshold).</summary>
    public bool IsUnderPressure => PendingBytes > BackpressureDropThreshold;

    public BoltHubConnection(IBoltConnection transport, int sendQueueCapacity = 4096, int sendEnqueueTimeoutMs = 0)
    {
        _transport = transport;
        _sendEnqueueTimeout = sendEnqueueTimeoutMs > 0
            ? TimeSpan.FromMilliseconds(sendEnqueueTimeoutMs)
            : TimeSpan.Zero;
        _sendChannel = Channel.CreateBounded<(byte[], int)>(
            new BoundedChannelOptions(Math.Max(1, sendQueueCapacity))
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    /// <summary>Start the background send loop. Call once after construction.</summary>
    public void StartSendLoop(CancellationToken ct)
    {
        SendLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var (buf, len) in _sendChannel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        if (_transport.IsConnected)
                        {
                            using var sendTimeoutCts = _sendEnqueueTimeout > TimeSpan.Zero
                                ? new CancellationTokenSource(_sendEnqueueTimeout)
                                : null;
                            using var linkedSendCts = sendTimeoutCts is null
                                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                                : CancellationTokenSource.CreateLinkedTokenSource(ct, sendTimeoutCts.Token);
                            await _transport.SendAsync(buf.AsMemory(0, len), linkedSendCts.Token);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { /* Transport error — receive loop will detect disconnect */ }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buf);
                        Interlocked.Add(ref _pendingBytes, -len);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                while (_sendChannel.Reader.TryRead(out var pending))
                {
                    ArrayPool<byte>.Shared.Return(pending.Buffer);
                    Interlocked.Add(ref _pendingBytes, -pending.Length);
                }
            }
        }, ct);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        // Snapshot into a pooled buffer — the caller's buffer (thread-local RentedBufferWriter
        // or pooled receive buffer) may be reused before the async transport write completes.
        var len = data.Length;
        var buf = ArrayPool<byte>.Shared.Rent(len);
        data.Span.CopyTo(buf);
        Interlocked.Add(ref _pendingBytes, len);

        // All sends go through Channel (serialized single-writer)
        if (_sendChannel.Writer.TryWrite((buf, len)))
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

            await _sendChannel.Writer.WriteAsync((buf, len), enqueueToken);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buf);
            Interlocked.Add(ref _pendingBytes, -len);
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

    public ValueTask CloseAsync(CancellationToken ct = default) => _transport.CloseAsync(ct);
}

/// <summary>
/// Routing entry for an active media stream.
/// Sender produces frames; Recipients receive them (multicast).
/// </summary>
internal sealed class MediaStreamRoute
{
    private readonly object _sync = new();
    private readonly List<BoltHubConnection> _recipients = new();

    public BoltHubConnection Sender { get; init; } = null!;
    public Guid CallId { get; init; }

    /// <summary>
    /// For simulcast: maps this stream to a simulcast layer group.
    /// All streams in the same group (callId + sender) represent different quality layers.
    /// The hub forwards only the selected layer per recipient.
    /// </summary>
    public byte? SimulcastLayerId { get; set; }

    public bool ContainsRecipient(BoltHubConnection connection)
    {
        lock (_sync)
        {
            return _recipients.Any(r => r.StreamId == connection.StreamId);
        }
    }

    public bool AddRecipient(BoltHubConnection connection)
    {
        lock (_sync)
        {
            if (_recipients.Any(r => r.StreamId == connection.StreamId))
                return false;

            _recipients.Add(connection);
            return true;
        }
    }

    public void AddRecipients(IEnumerable<BoltHubConnection> connections)
    {
        lock (_sync)
        {
            foreach (var connection in connections)
            {
                if (connection.StreamId == Sender.StreamId)
                    continue;

                if (!_recipients.Any(r => r.StreamId == connection.StreamId))
                    _recipients.Add(connection);
            }
        }
    }

    public void RemoveRecipientsWhere(Predicate<BoltHubConnection> predicate)
    {
        lock (_sync)
        {
            _recipients.RemoveAll(predicate);
        }
    }

    public BoltHubConnection[] GetRecipientSnapshot()
    {
        lock (_sync)
        {
            return _recipients.ToArray();
        }
    }
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
