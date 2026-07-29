using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Sources;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Bolt.Protocol.Transport;
using Bolt.Server.Media;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Bolt.Tests")]

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
    private static readonly TimeSpan MaxMigrationAllowanceLifetime = TimeSpan.FromDays(7);
    private static readonly string[] DefaultServiceIdentityClaimTypes = ["client_id", "service", "azp", "sub"];

    private enum PendingInvocationAddResult
    {
        Added,
        Duplicate,
        CapacityExceeded
    }

    private readonly record struct StreamDataAuthorization(
        bool Allowed,
        HttpStatusCode FailureStatus,
        Guid RequestId,
        bool TerminatePeer)
    {
        public static StreamDataAuthorization Success => new(true, default, default, false);
        public static StreamDataAuthorization Reject(
            HttpStatusCode status,
            Guid requestId = default,
            bool terminatePeer = true) =>
            new(false, status, requestId, terminatePeer);
    }

    private sealed class DurableReplayState(BoltHubConnection owner)
    {
        public BoltHubConnection Owner { get; } = owner;
        public object SyncRoot { get; } = new();
        public Queue<(long Sequence, byte[] Payload)> DeferredEvents { get; } = new();
        public long DeferredBytes { get; set; }
        public bool AcceptingDeferredEvents { get; set; } = true;
    }

    private sealed class ServiceRoute(string clientId)
    {
        public string ClientId { get; } = clientId;
        public object SyncRoot { get; } = new();
        public BoltHubConnection[] Connections = [];
        public bool Retired { get; set; }
    }

    private sealed record DurableSubscriptionAuthorization(
        BoltHubConnection Connection,
        DateTimeOffset? ExpiresAtUtc);

    private readonly ILogger<BoltServer> _logger;
    private readonly ConcurrentDictionary<string, BoltHubConnection> _activeTransportConnections = new();
    private readonly ConcurrentDictionary<string, BoltHubConnection> _connectionsByStreamId = new();
    private readonly ConcurrentDictionary<int, ServiceRoute> _connectionsByServiceHash = new();
    private readonly ConcurrentDictionary<Guid, PendingInvocation> _pendingInvocations = new();
    private readonly ConcurrentDictionary<int, int> _roundRobinIndex = new();
    private readonly ConcurrentDictionary<string, int> _connectionCountsByPrincipal = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _pendingInvocationsByPrincipal = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _activeStreamsByPrincipal = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _activeMediaStreamsByPrincipal = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _subscriptionsByPrincipal = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, BoltPrincipalRateLimiter> _rateLimitersByPrincipal = new(StringComparer.Ordinal);
    private long _requestRateRejectionTotal;
    private long _byteRateRejectionTotal;
    private long _pushRateRejectionTotal;
    private readonly SemaphoreSlim _pendingInvocationSlots;

    // Stream routing: streamId → (sender connection, recipient connection)
    private readonly ConcurrentDictionary<Guid, StreamRoute> _activeStreams = new();

    // Media routing: streamId → route (sender + recipients for multicast)
    private readonly ConcurrentDictionary<Guid, MediaStreamRoute> _activeMediaStreams = new();

    // Call state management: callId → state
    private readonly ConcurrentDictionary<Guid, ServerCallState> _activeCalls = new();

    // Direct handlers — when registered, server handles requests locally instead of routing
    private readonly ConcurrentDictionary<int, Func<BoltRequestContext, ReadOnlyMemory<byte>, Guid, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _localHandlers = new();
    private readonly ConcurrentDictionary<Guid, LocalInvocation> _localInvocations = new();
    private readonly ConcurrentDictionary<int, string> _commandNamesByHash = new();

    // Media processor tap: registered processors receive copies of media frames on a background thread
    private readonly List<IMediaProcessor> _mediaProcessors = new();
    private readonly Channel<(Guid CallId, Guid StreamId, byte[] Data, uint Timestamp, uint Seq)> _mediaTapChannel;
    private readonly CancellationTokenSource _mediaTapCts = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private int _disposed;

    private readonly Timer _cleanupTimer;
    private readonly int _invocationTimeoutMs;
    private readonly int _maxFrameBytes;
    private readonly int _receiveBufferBytes;
    private readonly int _maxLargeRpcPayloadBytes;
    private readonly int _sendQueueCapacity;
    private readonly long _sendQueueByteCapacity;
    private readonly int _sendEnqueueTimeoutMs;
    private readonly TimeSpan _transportCloseTimeout;
    private readonly int _maxPendingRpcCalls;
    private readonly int _maxPendingRpcCallsPerPrincipal;
    private readonly int _maxConnectionsPerPrincipal;
    private readonly int _maxActiveStreamsPerPrincipal;
    private readonly int _maxMediaStreamsPerPrincipal;
    private readonly int _maxSubscriptionsPerPrincipal;
    private readonly int _maxDurableSubscribersPerTopic;
    private readonly int _rpcRequestsPerSecond;
    private readonly int _rpcRequestBurst;
    private readonly int _rpcInboundBytesPerSecond;
    private readonly int _rpcInboundByteBurst;
    private readonly bool _rateLimitingEnabled;
    private readonly bool _requireTopicAuthorization;
    private readonly TimeSpan _maxConnectionLifetime;
    private readonly bool _mediaEnabled;
    private readonly BoltRegistrationIdentityBindingMode _registrationIdentityBindingMode;
    private readonly string _requiredServiceScope;
    private readonly string[] _serviceIdentityClaimTypes;
    private readonly HashSet<string> _reservedServiceNames;
    private readonly string[] _reservedServiceNamePrefixes;
    private readonly HashSet<string> _reservedServiceClientIds;
    private readonly BoltRegistrationMigrationAllowance[] _registrationMigrationAllowances;
    private static readonly int LargeRpcCommandHash = BoltCodec.Fnv1aHash("__bolt_large_rpc__");
    private static readonly int LargeRpcResponseHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response__");
    private static readonly int LargeRpcResponseStreamHash = BoltCodec.Fnv1aHash("__bolt_large_rpc_response_stream__");

    // Pub/sub state — transient (live fan-out only)
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<BoltHubConnection, byte>> _liveSubscribersByTopic = new();
    private readonly ConcurrentDictionary<BoltHubConnection, ConcurrentDictionary<int, byte>> _liveSubscriptionsByConnection = new();

    // Pub/sub state — durable (persistent identity)
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), BoltHubConnection> _liveDurableConnections = new();
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), DurableReplayState> _replayingDurableSubscriptions = new();
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), DurableSubscriptionAuthorization>
        _durableSubscriptionAuthorizations = new();
    private readonly SemaphoreSlim[] _durableSubscriptionGates =
        Enumerable.Range(0, 64).Select(static _ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly ConcurrentDictionary<int, string> _topicNamesByHash = new();

    // Durable queue backend (injected)
    private readonly Bolt.Server.Durable.IDurableQueueStore? _durableStore;
    private readonly Bolt.Server.Durable.DurableQueueOptions? _durableOptions;
    private readonly IReadOnlyList<IBoltTopicAuthorizer> _topicAuthorizers;
    private readonly ArrayPool<byte> _receiveBufferPool;

    public BoltServer(ILogger<BoltServer> logger)
        : this(logger, new BoltServerOptions())
    {
    }

    public BoltServer(
        ILogger<BoltServer> logger,
        BoltServerOptions options,
        IEnumerable<IBoltTopicAuthorizer>? topicAuthorizers = null)
        : this(logger, options, topicAuthorizers, ArrayPool<byte>.Shared)
    {
    }

    internal BoltServer(
        ILogger<BoltServer> logger,
        BoltServerOptions options,
        IEnumerable<IBoltTopicAuthorizer>? topicAuthorizers,
        ArrayPool<byte> receiveBufferPool)
    {
        _logger = logger;
        _topicAuthorizers = topicAuthorizers?.ToList() ?? [];
        _receiveBufferPool = receiveBufferPool ?? throw new ArgumentNullException(nameof(receiveBufferPool));
        _invocationTimeoutMs = Math.Max(1, options.InvocationTimeoutMs);
        _maxFrameBytes = Math.Max(1024, options.MaxFrameBytes);
        _receiveBufferBytes = Math.Clamp(options.ReceiveBufferBytes, 1024, _maxFrameBytes);
        _maxLargeRpcPayloadBytes = Math.Max(1024, options.MaxLargeRpcPayloadBytes);
        _sendQueueCapacity = Math.Max(1, options.SendQueueCapacity);
        _sendQueueByteCapacity = Math.Max(1, options.SendQueueByteCapacity);
        _sendEnqueueTimeoutMs = options.SendEnqueueTimeoutMs > 0
            ? options.SendEnqueueTimeoutMs
            : _invocationTimeoutMs;
        _transportCloseTimeout = TimeSpan.FromMilliseconds(Math.Max(1, options.TransportCloseTimeoutMs));
        _maxPendingRpcCalls = Math.Max(1, options.MaxPendingRpcCalls);
        _maxPendingRpcCallsPerPrincipal = Math.Min(
            _maxPendingRpcCalls,
            Math.Max(1, options.MaxPendingRpcCallsPerPrincipal));
        _pendingInvocationSlots = new SemaphoreSlim(_maxPendingRpcCalls, _maxPendingRpcCalls);
        _maxConnectionsPerPrincipal = Math.Max(1, options.MaxConnectionsPerPrincipal);
        _maxActiveStreamsPerPrincipal = Math.Max(1, options.MaxActiveStreamsPerPrincipal);
        _maxMediaStreamsPerPrincipal = Math.Max(1, options.MaxMediaStreamsPerPrincipal);
        _maxSubscriptionsPerPrincipal = Math.Max(1, options.MaxSubscriptionsPerPrincipal);
        _maxDurableSubscribersPerTopic = Math.Max(1, options.MaxDurableSubscribersPerTopic);
        ValidateRateLimitOptions(options.RpcRequestsPerSecond, options.RpcRequestBurst, "request");
        ValidateRateLimitOptions(options.RpcInboundBytesPerSecond, options.RpcInboundByteBurst, "inbound byte");
        _rpcRequestsPerSecond = options.RpcRequestsPerSecond;
        _rpcRequestBurst = options.RpcRequestBurst;
        _rpcInboundBytesPerSecond = options.RpcInboundBytesPerSecond;
        _rpcInboundByteBurst = options.RpcInboundByteBurst;
        _rateLimitingEnabled = _rpcRequestsPerSecond > 0 || _rpcInboundBytesPerSecond > 0;
        _requireTopicAuthorization = options.RequireTopicAuthorization;
        if (_requireTopicAuthorization && _topicAuthorizers.Count == 0)
        {
            throw new InvalidOperationException(
                "Bolt topic authorization is required, but no IBoltTopicAuthorizer is registered.");
        }
        _maxConnectionLifetime = options.MaxConnectionLifetimeSeconds > 0
            ? TimeSpan.FromSeconds(options.MaxConnectionLifetimeSeconds)
            : Timeout.InfiniteTimeSpan;
        _mediaEnabled = options.MediaEnabled;
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
        _registrationMigrationAllowances = options.RegistrationMigrationAllowances.ToArray();
        ValidateMigrationAllowances(_registrationMigrationAllowances);

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
        _localHandlers[hash] = (_, payload, requestId, _) => handler(payload, requestId);
    }

    public void RegisterHandler(
        string commandName,
        Func<ReadOnlyMemory<byte>, Guid, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = GetCommandHash(commandName);
        _localHandlers[hash] = (_, payload, requestId, ct) => handler(payload, requestId, ct);
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
        _localHandlers[hash] = (context, payload, requestId, _) => handler(context, payload, requestId);
    }

    public void RegisterHandler(
        string commandName,
        Func<BoltRequestContext, ReadOnlyMemory<byte>, Guid, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = GetCommandHash(commandName);
        _localHandlers[hash] = handler;
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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var connectionCts = CreateConnectionCancellation(user, ct);
        var connectionCt = connectionCts.Token;
        var connection = new BoltHubConnection(
            transport,
            _sendQueueCapacity,
            _sendEnqueueTimeoutMs,
            _sendQueueByteCapacity)
        {
            User = user
        };
        connection.StartSendLoop(connectionCt, ex =>
        {
            _logger.LogWarning(
                ex,
                "Retiring Bolt connection after transport send failure. client={ClientId} stream={StreamId}",
                connection.ClientId ?? "unregistered",
                connection.StreamId);
            connectionCts.Cancel();
        });
        _activeTransportConnections[connection.StreamId] = connection;
        var receiveBuffer = _receiveBufferPool.Rent(_receiveBufferBytes);
        byte[]? largeBuffer = null;

        try
        {
            while (transport.IsConnected && !connectionCt.IsCancellationRequested)
            {
                var (bytesRead, endOfMessage) = await transport.ReceiveAsync(receiveBuffer.AsMemory(), connectionCt);

                // (0, true) signals connection closed
                if (bytesRead == 0 && endOfMessage)
                    break;

                if (bytesRead == 0)
                    continue;

                if (bytesRead > _maxFrameBytes)
                {
                    BoltServerMetrics.RecordOversizedFrameRejection("fragment");
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
                    if (largeBuffer != null) _receiveBufferPool.Return(largeBuffer);
                    largeBuffer = _receiveBufferPool.Rent(capacity);
                    receiveBuffer.AsSpan(0, bytesRead).CopyTo(largeBuffer);

                    while (!endOfMessage)
                    {
                        (bytesRead, endOfMessage) = await transport.ReceiveAsync(receiveBuffer.AsMemory(), connectionCt);
                        if (bytesRead == 0 && endOfMessage)
                            return;

                        if (bytesRead > _maxFrameBytes || assembled > _maxFrameBytes - bytesRead)
                        {
                            BoltServerMetrics.RecordOversizedFrameRejection("assembled");
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
                            var newBuf = _receiveBufferPool.Rent(newCapacity);
                            largeBuffer.AsSpan(0, assembled).CopyTo(newBuf);
                            _receiveBufferPool.Return(largeBuffer);
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
                    BoltServerMetrics.RecordOversizedFrameRejection("complete");
                    _logger.LogWarning("Closing Bolt connection because frame size was invalid. size={Size} max={Max}", totalLength, _maxFrameBytes);
                    break;
                }

                if (ReferenceEquals(frameBytes, largeBuffer))
                {
                    var assembledFrame = largeBuffer!;
                    largeBuffer = null;
                    try
                    {
                        await ProcessFrameAsync(connection, assembledFrame, totalLength, connectionCt);
                    }
                    finally
                    {
                        _receiveBufferPool.Return(assembledFrame);
                    }
                }
                else
                {
                    await ProcessFrameAsync(connection, frameBytes, totalLength, connectionCt);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection for client {ClientId}", connection.ClientId ?? "unregistered");
        }
        finally
        {
            connection.BeginClose();
            connectionCts.Cancel();
            connection.CompleteSendChannel();
            if (connection.SendLoop is not null)
            {
                try { await connection.SendLoop.WaitAsync(_transportCloseTimeout); }
                catch (TimeoutException)
                {
                    _logger.LogWarning(
                        "Bolt send loop did not stop within the transport close deadline. client={ClientId} stream={StreamId}",
                        connection.ClientId ?? "unregistered",
                        connection.StreamId);
                }
                catch { }
            }
            _receiveBufferPool.Return(receiveBuffer);
            if (largeBuffer != null) _receiveBufferPool.Return(largeBuffer);
            await CloseTransportAsync(transport);
            try
            {
                await RemoveConnectionAsync(connection);
            }
            finally
            {
                _activeTransportConnections.TryRemove(connection.StreamId, out _);
            }
        }
    }

    private async ValueTask CloseTransportAsync(IBoltConnection transport)
    {
        using var closeCts = new CancellationTokenSource(_transportCloseTimeout);
        try
        {
            await transport.CloseAsync(closeCts.Token);
        }
        catch (OperationCanceledException) when (closeCts.IsCancellationRequested)
        {
        }
        catch
        {
        }

        try { await transport.DisposeAsync().AsTask().WaitAsync(_transportCloseTimeout); }
        catch (TimeoutException) { }
        catch { }
    }

    private CancellationTokenSource CreateConnectionCancellation(ClaimsPrincipal? user, CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdownCts.Token);
        var lifetime = _maxConnectionLifetime;

        var expirationValue = user?.FindFirstValue("exp");
        if (long.TryParse(expirationValue, NumberStyles.None, CultureInfo.InvariantCulture, out var expirationUnixSeconds))
        {
            try
            {
                var untilExpiration = DateTimeOffset.FromUnixTimeSeconds(expirationUnixSeconds) - DateTimeOffset.UtcNow;
                if (untilExpiration < TimeSpan.Zero)
                    untilExpiration = TimeSpan.Zero;

                if (lifetime == Timeout.InfiniteTimeSpan || untilExpiration < lifetime)
                    lifetime = untilExpiration;
            }
            catch (ArgumentOutOfRangeException)
            {
                lifetime = TimeSpan.Zero;
            }
        }

        if (lifetime != Timeout.InfiniteTimeSpan)
            cts.CancelAfter(lifetime);

        return cts;
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

        if (!_mediaEnabled && IsMediaFrame(frameType))
        {
            BoltServerMetrics.RecordDisabledMediaRejection(frameType);
            _logger.LogWarning(
                "Rejected disabled Bolt Media frame {FrameType} from {ClientId}",
                frameType,
                connection.ClientId);
            return;
        }

        switch (frameType)
        {
            case FrameType.Register:
                await HandleRegisterAsync(connection, buffer, length, ct);
                break;
            case FrameType.Batch:
            {
                if (!BoltCodec.TryReadBatch(buffer.AsSpan(0, length), out var batch))
                {
                    _logger.LogWarning("Closing {ClientId} after malformed Bolt batch", connection.ClientId);
                    await connection.CloseAsync(ct);
                    break;
                }

                var count = batch.Count;
                var offset = BoltCodec.BatchHeaderSize;
                for (var i = 0; i < count; i++)
                {
                    var frameLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset));
                    offset += 4;
                    buffer.AsSpan(offset, frameLength).CopyTo(buffer);
                    offset += frameLength;
                    await ProcessFrameAsync(connection, buffer, frameLength, ct);
                }
                break;
            }
            case FrameType.Request:
                // Process inline — hub work is just header parse + queue to writer channel (non-blocking)
                if (BoltCodec.TryReadRequest(buffer.AsSpan(0, length), out var request, out _) &&
                    _localHandlers.ContainsKey(request.CommandHash))
                {
                    if (!TryAdmitRpc(connection, request.PayloadLength, "rpc", isPush: false))
                    {
                        await SendRateLimitedResponseAsync(connection, request.RequestId, ct);
                        break;
                    }

                    var requestCopy = ArrayPool<byte>.Shared.Rent(length);
                    buffer.AsSpan(0, length).CopyTo(requestCopy);
                    _ = DispatchLocalRequestAsync(
                        connection,
                        requestCopy,
                        length,
                        admissionGranted: true,
                        ct);
                }
                else
                {
                    await HandleRequestAsync(connection, buffer, length, ct);
                }
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
            case FrameType.RequestCancel:
                await HandleRequestCancelAsync(connection, buffer, length, ct);
                break;
            case FrameType.StreamOpen:
                if (await HandleStreamOpenAsync(connection, buffer, length, ct))
                    await RouteStreamFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.StreamData:
                await RouteStreamFrameAsync(connection, buffer, length, ct);
                break;
            case FrameType.StreamClose:
                await RouteStreamFrameAsync(connection, buffer, length, ct);
                await CleanupStreamAsync(connection, buffer, length, ct);
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

    private async Task DispatchLocalRequestAsync(
        BoltHubConnection connection,
        byte[] buffer,
        int length,
        bool admissionGranted,
        CancellationToken ct)
    {
        try
        {
            await HandleRequestAsync(connection, buffer, length, ct, admissionGranted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Unhandled Bolt local request dispatch failure");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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

        if (!BoltCodec.TryReadRegister(
                buffer.AsSpan(0, length),
                out var wireVersion,
                out var clientId,
                out var clientName,
                out _))
        {
            BoltServerMetrics.RecordRegistrationRejection("invalid_register_frame");
            _logger.LogWarning("Invalid register frame");
            var rejectWriter = new ArrayBufferWriter<byte>(BoltCodec.RegisterAckSize);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAndCloseAsync(rejectWriter.WrittenMemory, ct);
            return;
        }

        if (wireVersion != BoltCodec.WireVersion)
        {
            BoltServerMetrics.RecordRegistrationRejection("wire_version_mismatch");
            _logger.LogWarning(
                "Rejected Bolt wire version {ClientWireVersion}; server requires {ServerWireVersion}",
                wireVersion,
                BoltCodec.WireVersion);
            var rejectWriter = new ArrayBufferWriter<byte>(BoltCodec.RegisterAckSize);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAndCloseAsync(rejectWriter.WrittenMemory, ct);
            return;
        }

        if (!ValidateRegisterIdentity(connection, clientId, clientName))
        {
            BoltServerMetrics.RecordRegistrationRejection("identity_mismatch");
            _logger.LogWarning(
                "Rejected Bolt register identity. stream={StreamId} clientId={ClientId} clientName={ClientName}",
                connection.StreamId,
                clientId,
                clientName);

            var rejectWriter = new ArrayBufferWriter<byte>(BoltCodec.RegisterAckSize);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAndCloseAsync(rejectWriter.WrittenMemory, ct);
            return;
        }

        var quotaKey = ResolvePrincipalQuotaKey(connection.User, clientId);
        if (quotaKey is null)
        {
            BoltServerMetrics.RecordRegistrationRejection("missing_principal_identifier");
            _logger.LogWarning(
                "Rejected Bolt registration because the authenticated principal has no stable quota identity. clientId={ClientId}",
                clientId);

            var rejectWriter = new ArrayBufferWriter<byte>(2);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAndCloseAsync(rejectWriter.WrittenMemory, ct);
            return;
        }

        if (!TryReserveQuota(_connectionCountsByPrincipal, quotaKey, _maxConnectionsPerPrincipal))
        {
            BoltServerMetrics.RecordQuotaRejection("connections");
            _logger.LogWarning(
                "Rejected Bolt register identity because client connection limit was reached. clientId={ClientId} maxConnections={MaxConnections}",
                clientId,
                _maxConnectionsPerPrincipal);

            var rejectWriter = new ArrayBufferWriter<byte>(2);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAndCloseAsync(rejectWriter.WrittenMemory, ct);
            return;
        }

        var serviceHash = BoltCodec.Fnv1aHash(clientId);
        connection.ClientId = clientId;
        connection.ClientName = clientName;
        connection.QuotaKey = quotaKey;
        connection.ServiceHash = serviceHash;
        if (!TryAddServiceConnection(connection, clientId, out var existingClientId))
        {
            ReleaseQuota(_connectionCountsByPrincipal, quotaKey);
            connection.ClientId = null;
            connection.ClientName = null;
            connection.QuotaKey = connection.StreamId;
            connection.ServiceHash = 0;
            BoltServerMetrics.RecordRegistrationRejection("service_hash_collision");
            _logger.LogWarning(
                "Rejected Bolt register identity because service hash collision was detected. hash={ServiceHash} existingClientId={ExistingClientId} rejectedClientId={RejectedClientId}",
                serviceHash,
                existingClientId,
                clientId);

            var rejectWriter = new ArrayBufferWriter<byte>(2);
            BoltCodec.WriteRegisterAck(rejectWriter, false);
            await connection.SendAndCloseAsync(rejectWriter.WrittenMemory, ct);
            return;
        }

        if (_rateLimitingEnabled)
            connection.RateLimiter = AttachPrincipalRateLimiter(quotaKey);

        connection.IsRegistered = true;

        _connectionsByStreamId[connection.StreamId] = connection;

        _logger.LogInformation("Client registered: {ClientId} ({ClientName}) [hash={ServiceHash}]",
            clientId, clientName, connection.ServiceHash);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteRegisterAck(writer, true);
        await connection.SendAsync(writer, ct);

        await NotifyClientRegisteredAsync(connection, ct);
    }

    private async Task HandleRequestAsync(
        BoltHubConnection caller,
        byte[] buffer,
        int length,
        CancellationToken ct,
        bool admissionGranted = false)
    {
        var span = buffer.AsSpan(0, length);

        if (!BoltCodec.TryReadRequest(span, out var frame, out var consumed))
        {
            if (!admissionGranted)
                TryAdmitRpc(caller, length, "rpc", isPush: false);
            _logger.LogWarning("Invalid request frame from {ClientId}", caller.ClientId);
            return;
        }

        if (!admissionGranted && !TryAdmitRpc(caller, frame.PayloadLength, "rpc", isPush: false))
        {
            await SendRateLimitedResponseAsync(caller, frame.RequestId, ct);
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
        if (_localHandlers.TryGetValue(frame.CommandHash, out var localHandler))
        {
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdownCts.Token);
            requestCts.CancelAfter(_invocationTimeoutMs);
            var invocation = new LocalInvocation(caller, requestCts);
            if (!_localInvocations.TryAdd(frame.RequestId, invocation))
            {
                var duplicateWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteResponse(duplicateWriter, frame.RequestId, HttpStatusCode.Conflict, ReadOnlySpan<byte>.Empty);
                await caller.SendAsync(duplicateWriter, ct);
                return;
            }

            try
            {
                var payload = frame.GetPayload(buffer.AsMemory(0, length));
                var context = BoltRequestContext.FromConnection(caller);
                var (statusCode, responsePayload) = await localHandler(context, payload, frame.RequestId, requestCts.Token);
                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteResponse(writer, frame.RequestId, statusCode, responsePayload.Span);
                await caller.SendAsync(writer, ct);
            }
            catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
            {
                if (!invocation.CanceledByCaller && caller.IsAlive)
                    await SendInvocationTerminalResponseAsync(frame.RequestId, caller, HttpStatusCode.GatewayTimeout, "local-handler-timeout", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Local handler error for command hash {Hash}", frame.CommandHash);
                var errWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.InternalServerError, ReadOnlySpan<byte>.Empty);
                await caller.SendAsync(errWriter, ct);
            }
            finally
            {
                _localInvocations.TryRemove(new KeyValuePair<Guid, LocalInvocation>(frame.RequestId, invocation));
            }
            return;
        }

        var recipient = GetRecipient(frame.RecipientHash);
        if (recipient is null)
        {
            BoltServerMetrics.RecordRouteMiss("request");
            var errWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.NotFound, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter, ct);
            return;
        }

        var pending = new PendingInvocation(caller, recipient, Environment.TickCount64);
        var addResult = TryAddPendingInvocation(frame.RequestId, pending);
        if (addResult != PendingInvocationAddResult.Added)
        {
            if (addResult == PendingInvocationAddResult.CapacityExceeded)
                BoltServerMetrics.RecordQuotaRejection("pending_rpc");
            var errWriter = RentedBufferWriter.GetThreadLocal();
            var statusCode = addResult == PendingInvocationAddResult.CapacityExceeded
                ? HttpStatusCode.TooManyRequests
                : HttpStatusCode.Conflict;
            BoltCodec.WriteResponse(errWriter, frame.RequestId, statusCode, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter, ct);
            return;
        }

        try
        {
            await recipient.SendAsync(buffer.AsMemory(0, consumed), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            TryRemovePendingInvocation(frame.RequestId, pending, out _);
            _logger.LogWarning(
                ex,
                "Failed to enqueue routed request {RequestId} to recipient {Recipient}",
                frame.RequestId,
                recipient.ClientId);

            var errWriter = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.ServiceUnavailable, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(errWriter, ct);
        }
    }

    private async Task HandleRequestCancelAsync(
        BoltHubConnection caller,
        byte[] buffer,
        int length,
        CancellationToken ct)
    {
        if (!BoltCodec.TryReadRequestCancel(buffer.AsSpan(0, length), out var requestId))
            return;

        if (_localInvocations.TryGetValue(requestId, out var localInvocation) &&
            ReferenceEquals(localInvocation.Caller, caller))
        {
            localInvocation.CancelByCaller();
            return;
        }

        if (!_pendingInvocations.TryGetValue(requestId, out var pending) ||
            !ReferenceEquals(pending.Caller, caller) ||
            !TryRemovePendingInvocation(requestId, pending, out var removed))
        {
            return;
        }

        BoltServerMetrics.RecordRequestCancellation();
        BoltServerMetrics.RecordRpcDuration(Environment.TickCount64 - removed.Timestamp, "canceled");

        if (!removed.ExpectedResponder.IsAlive)
            return;

        try
        {
            await removed.ExpectedResponder.SendAsync(buffer.AsMemory(0, length), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Failed to forward Bolt request cancellation {RequestId}", requestId);
        }
    }

    private static bool IsMediaFrame(FrameType frameType) =>
        frameType is
            FrameType.MediaFrame or
            FrameType.FecFrame or
            FrameType.MediaConfig or
            FrameType.MediaFeedback or
            FrameType.MediaKeyRequest or
            FrameType.NackRequest or
            FrameType.CallSignal;

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
                    "Rejected response from unexpected Bolt responder. requestId={RequestId} expectedClient={ExpectedClient} expectedStream={ExpectedStream} actualClient={ActualClient} actualStream={ActualStream}",
                    requestId,
                    pending.ExpectedResponder.ClientId,
                    pending.ExpectedResponder.StreamId,
                    responder.ClientId,
                    responder.StreamId);
                return;
            }

            if (TryRemovePendingInvocation(requestId, pending, out var removed))
            {
                BoltServerMetrics.RecordRpcDuration(Environment.TickCount64 - removed.Timestamp, "completed");
                await removed.Caller.SendAsync(buffer.AsMemory(0, totalSize), ct);
            }
        }
    }

    private async Task HandlePushAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadRequest(buffer.AsSpan(0, length), out var frame, out var totalSize))
        {
            TryAdmitRpc(sender, length, "push", isPush: true);
            _logger.LogWarning("Invalid push frame from {ClientId}", sender.ClientId);
            return;
        }

        if (frame.CommandHash == LargeRpcResponseHash &&
            frame.SenderHash == sender.ServiceHash &&
            IsExpectedLargeRpcResponsePush(sender, frame, buffer, totalSize))
        {
            await RouteLargeRpcResponsePushAsync(sender, frame, buffer, totalSize, ct);
            return;
        }

        if (!TryAdmitRpc(sender, frame.PayloadLength, "push", isPush: true))
            return;

        if (frame.SenderHash != sender.ServiceHash)
        {
            _logger.LogWarning(
                "Rejected push with spoofed sender hash. client={ClientId} expected={ExpectedHash} actual={ActualHash}",
                sender.ClientId,
                sender.ServiceHash,
                frame.SenderHash);
            return;
        }

        if (frame.CommandHash == LargeRpcResponseHash)
        {
            await RouteLargeRpcResponsePushAsync(sender, frame, buffer, totalSize, ct);
            return;
        }

        var recipient = GetRecipient(frame.RecipientHash);
        if (recipient is null)
        {
            BoltServerMetrics.RecordRouteMiss("push");
            return;
        }

        if (!recipient.IsUnderPressure)
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

        if (TryRemovePendingInvocation(requestId, pending, out var removed))
            await removed.Caller.SendAsync(buffer.AsMemory(0, totalSize), ct);
    }

    /// <summary>Get the count of currently connected clients.</summary>
    public int ConnectedClientCount => _connectionsByStreamId.Count;

    /// <summary>
    /// Captures nonsecret runtime transport facts for readiness and rollout observation.
    /// The snapshot is aggregate-only and does not expose client or principal identities.
    /// </summary>
    public BoltServerHealthSnapshot GetHealthSnapshot()
    {
        var connections = _activeTransportConnections.Values.ToArray();
        var registeredConnections = _connectionsByStreamId.Count;
        var liveConnections = 0;
        var closingConnections = 0;
        var unregisteredTrackedConnections = 0;
        var runningSendLoops = 0;
        var completedSendLoops = 0;
        var faultedSendLoops = 0;
        var liveConnectionsWithInactiveSendLoops = 0;
        var connectionsUnderSendPressure = 0;
        var negativeRuntimeCounters = 0;
        long aggregateQueuedSendBytes = 0;
        long maximumQueuedSendBytes = 0;

        foreach (var connection in connections)
        {
            var isLive = connection.IsAlive;
            var isClosing = connection.IsClosing;
            if (isLive)
                liveConnections++;
            if (isClosing)
                closingConnections++;

            var pendingBytes = connection.PendingBytes;
            if (pendingBytes < 0)
            {
                negativeRuntimeCounters++;
            }
            else
            {
                aggregateQueuedSendBytes = SaturatingAdd(aggregateQueuedSendBytes, pendingBytes);
                maximumQueuedSendBytes = Math.Max(maximumQueuedSendBytes, pendingBytes);
                if (pendingBytes > BoltHubConnection.BackpressureDropThreshold)
                    connectionsUnderSendPressure++;
            }

            var sendLoopStatus = connection.SendLoop?.Status;
            if (sendLoopStatus is null)
            {
                if (isLive && !isClosing)
                    liveConnectionsWithInactiveSendLoops++;
                continue;
            }

            if (sendLoopStatus == TaskStatus.Faulted)
                faultedSendLoops++;

            if (sendLoopStatus is TaskStatus.RanToCompletion or TaskStatus.Canceled or TaskStatus.Faulted)
            {
                completedSendLoops++;
                if (isLive && !isClosing)
                    liveConnectionsWithInactiveSendLoops++;
            }
            else
            {
                runningSendLoops++;
            }
        }

        var connectionCounts = SummarizeRuntimeCounts(_connectionCountsByPrincipal.Values);
        var pendingRpcCounts = SummarizeRuntimeCounts(_pendingInvocationsByPrincipal.Values);
        var logicalStreamCounts = SummarizeRuntimeCounts(_activeStreamsByPrincipal.Values);
        var mediaStreamCounts = SummarizeRuntimeCounts(_activeMediaStreamsByPrincipal.Values);
        var subscriptionCounts = SummarizeRuntimeCounts(_subscriptionsByPrincipal.Values);
        var liveTransientSubscriptions = SummarizeRuntimeCounts(
            _liveSubscriptionsByConnection.Values.Select(static topics => topics.Count));
        foreach (var connection in _connectionsByStreamId.Values)
        {
            if (!connection.IsRegistered)
                unregisteredTrackedConnections++;
        }

        negativeRuntimeCounters += connectionCounts.NegativeCount +
                                   pendingRpcCounts.NegativeCount +
                                   logicalStreamCounts.NegativeCount +
                                   mediaStreamCounts.NegativeCount +
                                   subscriptionCounts.NegativeCount +
                                   liveTransientSubscriptions.NegativeCount;
        var rateLimitRejections = new BoltRateLimitRejectionTotals(
            Interlocked.Read(ref _requestRateRejectionTotal),
            Interlocked.Read(ref _byteRateRejectionTotal),
            Interlocked.Read(ref _pushRateRejectionTotal));

        return new BoltServerHealthSnapshot(
            connections.Length,
            registeredConnections,
            Math.Max(0, connections.Length - registeredConnections),
            liveConnections,
            closingConnections,
            unregisteredTrackedConnections,
            _pendingInvocations.Count,
            _activeStreams.Count,
            _activeMediaStreams.Count,
            _activeCalls.Count,
            subscriptionCounts.Total,
            liveTransientSubscriptions.Total,
            _liveDurableConnections.Count,
            aggregateQueuedSendBytes,
            maximumQueuedSendBytes,
            connectionsUnderSendPressure,
            runningSendLoops,
            completedSendLoops,
            faultedSendLoops,
            liveConnectionsWithInactiveSendLoops,
            negativeRuntimeCounters,
            connectionCounts.Maximum,
            pendingRpcCounts.Maximum,
            logicalStreamCounts.Maximum,
            mediaStreamCounts.Maximum,
            subscriptionCounts.Maximum,
            _rateLimitersByPrincipal.Count,
            rateLimitRejections.RequestRate,
            rateLimitRejections.ByteRate,
            rateLimitRejections.PushRate,
            Volatile.Read(ref _disposed) != 0,
            new BoltServerHealthBounds(
                _maxFrameBytes,
                _sendQueueCapacity,
                _sendEnqueueTimeoutMs,
                BoltHubConnection.BackpressureDropThreshold,
                BoltHubConnection.BackpressureFeedbackThreshold,
                _maxPendingRpcCalls,
                _maxPendingRpcCallsPerPrincipal,
                _maxConnectionsPerPrincipal,
                _maxActiveStreamsPerPrincipal,
                _maxMediaStreamsPerPrincipal,
                _maxSubscriptionsPerPrincipal,
                _maxDurableSubscribersPerTopic,
                _rpcRequestsPerSecond,
                _rpcRequestBurst,
                _rpcInboundBytesPerSecond,
                _rpcInboundByteBurst,
                _requireTopicAuthorization,
                _mediaEnabled));
    }

    private static RuntimeCountSummary SummarizeRuntimeCounts(IEnumerable<int> counts)
    {
        long total = 0;
        var maximum = 0;
        var negativeCount = 0;
        foreach (var count in counts)
        {
            if (count < 0)
            {
                negativeCount++;
                continue;
            }

            total = SaturatingAdd(total, count);
            maximum = Math.Max(maximum, count);
        }

        return new RuntimeCountSummary(total, maximum, negativeCount);
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private readonly record struct RuntimeCountSummary(long Total, int Maximum, int NegativeCount);

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

    private async Task<bool> HandleStreamOpenAsync(
        BoltHubConnection sender,
        byte[] buffer,
        int length,
        CancellationToken ct)
    {
        if (!BoltCodec.TryReadStreamOpen(buffer.AsSpan(0, length), out var streamId, out var recipientHash, out var commandHash))
            return false;

        var recipient = GetRecipient(recipientHash);
        if (recipient is null)
        {
            BoltServerMetrics.RecordRouteMiss("stream");
            await SendUnavailableStreamCloseAsync(sender, streamId, ct);
            return false;
        }

        if (!TryReserveQuota(_activeStreamsByPrincipal, sender.QuotaKey, _maxActiveStreamsPerPrincipal))
        {
            BoltServerMetrics.RecordQuotaRejection("logical_streams");
            _logger.LogWarning(
                "Rejected stream open because connection stream limit was reached. client={ClientId} maxStreams={MaxStreams}",
                sender.ClientId,
                _maxActiveStreamsPerPrincipal);
            await SendStreamCloseAsync(sender, streamId, HttpStatusCode.TooManyRequests, ct);
            return false;
        }

        if (!_activeStreams.TryAdd(streamId, new StreamRoute(sender, recipient, commandHash)))
        {
            ReleaseQuota(_activeStreamsByPrincipal, sender.QuotaKey);
            _logger.LogWarning(
                "Rejected duplicate stream id {StreamId} from {ClientId}",
                streamId,
                sender.ClientId);
            await SendStreamCloseAsync(sender, streamId, HttpStatusCode.Conflict, ct);
            return false;
        }

        if (!recipient.IsAlive || !_connectionsByStreamId.ContainsKey(recipient.StreamId))
        {
            if (_activeStreams.TryGetValue(streamId, out var route))
            {
                RemoveStreamRoute(
                    streamId,
                    route,
                    releaseLargeRpcPendingInvocation: false,
                    out _);
            }
            await SendUnavailableStreamCloseAsync(sender, streamId, ct);
            return false;
        }

        _logger.LogDebug("Stream opened: {StreamId} from {Sender} to {Recipient}",
            streamId, sender.ClientId, recipient.ClientId);
        return true;
    }

    private bool IsExpectedLargeRpcResponsePush(
        BoltHubConnection sender,
        RequestFrame frame,
        byte[] buffer,
        int totalSize)
    {
        var payload = frame.GetPayload(buffer.AsMemory(0, totalSize));
        if (payload.Length < 18)
            return false;

        var requestId = new Guid(payload.Span[..16]);
        return _pendingInvocations.TryGetValue(requestId, out var pending) &&
               pending.ExpectedResponder.StreamId == sender.StreamId &&
               pending.Caller.ServiceHash == frame.RecipientHash;
    }

    /// <summary>
    /// Route a stream frame (Data or Close) to the correct peer.
    /// If the sender is the stream's Sender, forward to Recipient and vice versa.
    /// </summary>
    private async Task RouteStreamFrameAsync(BoltHubConnection sender, byte[] buffer, int length, CancellationToken ct)
    {
        if (length < 17)
        {
            if (length > 0 && (FrameType)buffer[0] == FrameType.StreamData)
                TryAdmitRpc(sender, length, "malformed_stream_data", isPush: false);
            return;
        }

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
        if ((FrameType)buffer[0] == FrameType.StreamData)
        {
            var authorization = AuthorizeStreamData(sender, peers, buffer, length);
            if (!authorization.Allowed)
            {
                await RejectStreamDataAsync(
                    sender,
                    peers,
                    streamId,
                    authorization.FailureStatus,
                    authorization.RequestId,
                    authorization.TerminatePeer,
                    ct);
                return;
            }
        }

        BoltHubConnection recipient;
        if (peers.Sender.StreamId == sender.StreamId)
            recipient = peers.Recipient;
        else if (peers.Recipient.StreamId == sender.StreamId)
            recipient = peers.Sender;
        else
        {
            _logger.LogWarning(
                "Rejected stream frame from nonparticipant. streamId={StreamId} client={ClientId}",
                streamId,
                sender.ClientId);
            return;
        }

        if (!recipient.IsAlive)
        {
            if (RemoveStreamRoute(
                    streamId,
                    peers,
                    releaseLargeRpcPendingInvocation: true,
                    out var releasedOwnership) &&
                releasedOwnership is not null)
            {
                await SendUnavailableResponseAsync(
                    releasedOwnership.PendingInvocation.Caller,
                    releasedOwnership.RequestId,
                    ct);
            }
            return;
        }

        try
        {
            await recipient.SendAsync(buffer.AsMemory(0, length), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to route stream {StreamId} to {Recipient}; the sender remains connected",
                streamId,
                recipient.ClientId);

            recipient.BeginClose();
            recipient.CompleteSendChannel();
            using (var closeCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                closeCts.CancelAfter(_transportCloseTimeout);
                try { await recipient.CloseAsync(closeCts.Token); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception closeException)
                {
                    _logger.LogDebug(
                        closeException,
                        "Failed to close unresponsive stream recipient {Recipient}",
                        recipient.ClientId);
                }
            }

            if (RemoveStreamRoute(
                    streamId,
                    peers,
                    releaseLargeRpcPendingInvocation: true,
                    out var releasedOwnership) &&
                releasedOwnership is not null)
            {
                await SendUnavailableResponseAsync(
                    releasedOwnership.PendingInvocation.Caller,
                    releasedOwnership.RequestId,
                    ct);
            }
            else
            {
                await SendUnavailableStreamCloseAsync(sender, streamId, ct);
            }
        }
    }

    private StreamDataAuthorization AuthorizeStreamData(
        BoltHubConnection sender,
        StreamRoute route,
        byte[] buffer,
        int length)
    {
        if (route.CommandHash == LargeRpcCommandHash && route.Sender.StreamId == sender.StreamId)
            return TryTrackLargeRpcRequest(route, buffer, length);

        if (route.CommandHash == LargeRpcResponseStreamHash)
        {
            if (route.Sender.StreamId != sender.StreamId)
            {
                _logger.LogWarning("Rejected large RPC response stream data from non-owner {ClientId}", sender.ClientId);
                return StreamDataAuthorization.Reject(HttpStatusCode.Forbidden, terminatePeer: false);
            }

            return TryValidateLargeRpcResponseStream(route, buffer, length);
        }

        return StreamDataAuthorization.Success;
    }

    private StreamDataAuthorization TryTrackLargeRpcRequest(StreamRoute route, byte[] buffer, int length)
    {
        lock (route.SyncRoot)
        {
            if (!BoltCodec.TryReadStreamData(buffer.AsSpan(0, length), out _, out var payloadOffset, out var payloadLength, out _))
            {
                var status = TryAdmitRpc(route.Sender, length, "large_rpc", isPush: false)
                    ? HttpStatusCode.BadRequest
                    : HttpStatusCode.TooManyRequests;
                return StreamDataAuthorization.Reject(status);
            }

            if (route.LargeRpcRequestTracked)
            {
                var received = route.LargeRpcRequestBytesReceived + payloadLength;
                if (received > route.LargeRpcExpectedPayloadBytes)
                {
                    _logger.LogWarning(
                        "Rejected large RPC request exceeding its declared size. client={ClientId} expected={Expected} received={Received}",
                        route.Sender.ClientId,
                        route.LargeRpcExpectedPayloadBytes,
                        received);
                    return StreamDataAuthorization.Reject(
                        HttpStatusCode.BadRequest,
                        route.LargeRpcRequestOwnership?.RequestId ?? default);
                }

                route.LargeRpcRequestBytesReceived = received;
                return StreamDataAuthorization.Success;
            }

            if (payloadLength < 28)
            {
                var status = TryAdmitRpc(route.Sender, payloadLength, "large_rpc", isPush: false)
                    ? HttpStatusCode.BadRequest
                    : HttpStatusCode.TooManyRequests;
                _logger.LogWarning("Rejected malformed large RPC request header from {ClientId}", route.Sender.ClientId);
                return StreamDataAuthorization.Reject(status);
            }

            var payload = buffer.AsSpan(payloadOffset, payloadLength);
            var requestId = new Guid(payload[..16]);
            var expectedPayloadBytes = BinaryPrimitives.ReadInt32LittleEndian(payload[20..]);
            var metadataSenderHash = BinaryPrimitives.ReadInt32LittleEndian(payload[24..]);
            if (expectedPayloadBytes < 0 || expectedPayloadBytes > _maxLargeRpcPayloadBytes)
            {
                if (!TryAdmitRpc(
                        route.Sender,
                        expectedPayloadBytes > 0 ? expectedPayloadBytes : payloadLength,
                        "large_rpc",
                        isPush: false))
                {
                    return StreamDataAuthorization.Reject(HttpStatusCode.TooManyRequests, requestId);
                }

                _logger.LogWarning(
                    "Rejected large RPC request outside the configured logical payload limit. client={ClientId} declared={Declared} max={Max}",
                    route.Sender.ClientId,
                    expectedPayloadBytes,
                    _maxLargeRpcPayloadBytes);
                return StreamDataAuthorization.Reject(HttpStatusCode.RequestEntityTooLarge, requestId);
            }

            if (!TryAdmitRpc(route.Sender, expectedPayloadBytes, "large_rpc", isPush: false))
                return StreamDataAuthorization.Reject(HttpStatusCode.TooManyRequests, requestId);

            if (metadataSenderHash != route.Sender.ServiceHash)
            {
                _logger.LogWarning(
                    "Rejected large RPC request with spoofed metadata sender hash. requestId={RequestId} client={ClientId}",
                    requestId,
                    route.Sender.ClientId);
                return StreamDataAuthorization.Reject(HttpStatusCode.Forbidden, requestId);
            }

            var pending = new PendingInvocation(route.Sender, route.Recipient, Environment.TickCount64);
            var addResult = TryAddPendingInvocation(requestId, pending);
            if (addResult != PendingInvocationAddResult.Added)
            {
                _logger.LogWarning(
                    "Rejected large RPC request. reason={Reason} requestId={RequestId} client={ClientId}",
                    addResult,
                    requestId,
                    route.Sender.ClientId);
                return StreamDataAuthorization.Reject(
                    addResult == PendingInvocationAddResult.CapacityExceeded
                        ? HttpStatusCode.TooManyRequests
                        : HttpStatusCode.Conflict,
                    requestId);
            }

            route.LargeRpcRequestTracked = true;
            route.LargeRpcRequestOwnership = new LargeRpcPendingInvocationOwnership(requestId, pending);
            route.LargeRpcExpectedPayloadBytes = expectedPayloadBytes;
            return StreamDataAuthorization.Success;
        }
    }

    private StreamDataAuthorization TryValidateLargeRpcResponseStream(StreamRoute route, byte[] buffer, int length)
    {
        lock (route.SyncRoot)
        {
            if (route.LargeRpcResponseValidated)
            {
                if (!BoltCodec.TryReadStreamData(buffer.AsSpan(0, length), out _, out _, out var nextPayloadLength, out _))
                    return StreamDataAuthorization.Reject(
                        HttpStatusCode.BadRequest,
                        route.LargeRpcResponseRequestId,
                        terminatePeer: true);

                var received = route.LargeRpcResponseBytesReceived + nextPayloadLength;
                if (received > route.LargeRpcResponseExpectedPayloadBytes)
                {
                    _logger.LogWarning(
                        "Rejected large RPC response exceeding its declared size. client={ClientId} expected={Expected} received={Received}",
                        route.Sender.ClientId,
                        route.LargeRpcResponseExpectedPayloadBytes,
                        received);
                    return StreamDataAuthorization.Reject(
                        HttpStatusCode.BadRequest,
                        route.LargeRpcResponseRequestId,
                        terminatePeer: true);
                }

                route.LargeRpcResponseBytesReceived = received;
                return StreamDataAuthorization.Success;
            }

            if (!BoltCodec.TryReadStreamData(buffer.AsSpan(0, length), out _, out var payloadOffset, out var payloadLength, out _))
                return StreamDataAuthorization.Reject(HttpStatusCode.BadRequest, terminatePeer: false);

            if (payloadLength < 22)
            {
                _logger.LogWarning("Rejected malformed large RPC response stream header from {ClientId}", route.Sender.ClientId);
                return StreamDataAuthorization.Reject(HttpStatusCode.BadRequest, terminatePeer: false);
            }

            var payload = buffer.AsSpan(payloadOffset, payloadLength);
            var requestId = new Guid(payload[..16]);
            var expectedPayloadBytes = BinaryPrimitives.ReadInt32LittleEndian(payload[18..]);
            if (!_pendingInvocations.TryGetValue(requestId, out var pending))
            {
                _logger.LogWarning(
                    "Rejected large RPC response stream without pending invocation. requestId={RequestId} sender={ClientId}",
                    requestId,
                    route.Sender.ClientId);
                return StreamDataAuthorization.Reject(HttpStatusCode.NotFound, requestId, terminatePeer: false);
            }

            if (pending.ExpectedResponder.StreamId != route.Sender.StreamId ||
                pending.Caller.ServiceHash != route.Recipient.ServiceHash)
            {
                _logger.LogWarning(
                    "Rejected large RPC response stream from unexpected responder or recipient. requestId={RequestId} sender={Sender}",
                    requestId,
                    route.Sender.ClientId);
                return StreamDataAuthorization.Reject(HttpStatusCode.Forbidden, requestId, terminatePeer: false);
            }

            if (expectedPayloadBytes < 0 || expectedPayloadBytes > _maxLargeRpcPayloadBytes)
            {
                _logger.LogWarning(
                    "Rejected large RPC response outside the configured logical payload limit. client={ClientId} declared={Declared} max={Max}",
                    route.Sender.ClientId,
                    expectedPayloadBytes,
                    _maxLargeRpcPayloadBytes);
                return StreamDataAuthorization.Reject(
                    HttpStatusCode.RequestEntityTooLarge,
                    requestId,
                    terminatePeer: true);
            }

            if (!TryRemovePendingInvocation(requestId, pending, out _))
                return StreamDataAuthorization.Reject(HttpStatusCode.Conflict, terminatePeer: false);

            route.LargeRpcResponseValidated = true;
            route.LargeRpcResponseRequestId = requestId;
            route.LargeRpcResponseExpectedPayloadBytes = expectedPayloadBytes;
            return StreamDataAuthorization.Success;
        }
    }

    private async Task RejectStreamDataAsync(
        BoltHubConnection sender,
        StreamRoute route,
        Guid streamId,
        HttpStatusCode statusCode,
        Guid requestId,
        bool terminatePeer,
        CancellationToken ct)
    {
        RemoveStreamRoute(
            streamId,
            route,
            releaseLargeRpcPendingInvocation: true,
            out var releasedOwnership);

        await SendStreamCloseAsync(sender, streamId, statusCode, ct);
        if (!terminatePeer)
            return;

        var peer = route.Sender.StreamId == sender.StreamId ? route.Recipient : route.Sender;
        if (peer.IsAlive)
            await SendStreamCloseAsync(peer, streamId, statusCode, ct);

        if (releasedOwnership is not null)
        {
            await SendLargeRpcTerminalResponseAsync(releasedOwnership, statusCode, ct);
            return;
        }

        if (requestId == Guid.Empty)
            return;

        if (route.CommandHash == LargeRpcCommandHash)
            await SendLargeRpcTerminalResponseAsync(route.Sender, route.Recipient.ServiceHash, requestId, statusCode, ct);
        else if (route.CommandHash == LargeRpcResponseStreamHash)
            await SendLargeRpcTerminalResponseAsync(route.Recipient, route.Sender.ServiceHash, requestId, statusCode, ct);
    }

    private async Task CleanupStreamAsync(
        BoltHubConnection sender,
        byte[] buffer,
        int length,
        CancellationToken ct)
    {
        if (!BoltCodec.TryReadStreamClose(buffer.AsSpan(0, length), out var streamId, out var statusCode))
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

        var releasePendingInvocation = IsAbortedLargeRpcRequest(peers, sender, statusCode);
        if (!RemoveStreamRoute(streamId, peers, releasePendingInvocation, out var releasedOwnership) ||
            releasedOwnership is null)
        {
            return;
        }

        var terminalStatus = statusCode != HttpStatusCode.OK
            ? statusCode
            : sender.StreamId == peers.Sender.StreamId
                ? HttpStatusCode.BadRequest
                : HttpStatusCode.ServiceUnavailable;
        await SendLargeRpcTerminalResponseAsync(releasedOwnership, terminalStatus, ct);
    }

    private static bool IsAbortedLargeRpcRequest(
        StreamRoute route,
        BoltHubConnection closingConnection,
        HttpStatusCode statusCode)
    {
        if (route.CommandHash != LargeRpcCommandHash)
            return false;

        lock (route.SyncRoot)
        {
            return route.LargeRpcRequestOwnership is not null &&
                   (closingConnection.StreamId != route.Sender.StreamId ||
                    statusCode != HttpStatusCode.OK ||
                    route.LargeRpcRequestBytesReceived != route.LargeRpcExpectedPayloadBytes);
        }
    }

    private bool RemoveStreamRoute(
        Guid streamId,
        StreamRoute route,
        bool releaseLargeRpcPendingInvocation,
        out LargeRpcPendingInvocationOwnership? releasedOwnership)
    {
        releasedOwnership = null;
        if (!_activeStreams.TryRemove(new KeyValuePair<Guid, StreamRoute>(streamId, route)))
            return false;

        ReleaseQuota(_activeStreamsByPrincipal, route.Sender.QuotaKey);
        if (releaseLargeRpcPendingInvocation)
            releasedOwnership = ReleaseLargeRpcPendingInvocation(route);
        return true;
    }

    private LargeRpcPendingInvocationOwnership? ReleaseLargeRpcPendingInvocation(StreamRoute route)
    {
        LargeRpcPendingInvocationOwnership? ownership;
        lock (route.SyncRoot)
        {
            ownership = route.LargeRpcRequestOwnership;
            route.LargeRpcRequestOwnership = null;
        }

        return ownership is not null &&
               TryRemovePendingInvocation(ownership.RequestId, ownership.PendingInvocation, out _)
            ? ownership
            : null;
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
        => callState.Status == ServerCallStatus.Active;

    private bool RejectMediaWhileHeld(
        ServerCallState callState,
        FrameType frameType,
        Guid callId,
        BoltHubConnection sender)
    {
        if (callState.Status != ServerCallStatus.Held)
            return false;

        _logger.LogDebug(
            "Rejected media traffic while call is held. frameType={FrameType} call={CallId} sender={Sender}",
            frameType,
            callId,
            sender.ClientId);
        return true;
    }

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

    private static void RemoveMediaStream(ServerCallState callState, Guid streamId)
    {
        lock (callState.MediaStreamIds)
            callState.MediaStreamIds.Remove(streamId);
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
            RejectMediaWhileHeld(owningCall, (FrameType)buffer[0], route.CallId, sender) ||
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

        if (!_activeCalls.TryGetValue(config.CallId, out var callState))
        {
            _logger.LogWarning(
                "Rejected MediaConfig for inactive or unauthorized call. streamId={StreamId} call={CallId} sender={Sender}",
                config.StreamId,
                config.CallId,
                sender.ClientId);
            return;
        }

        if (RejectMediaWhileHeld(callState, FrameType.MediaConfig, config.CallId, sender))
            return;

        MediaStreamRoute? route;
        IReadOnlyList<BoltHubConnection> recipients;
        lock (callState.SyncRoot)
        {
            if (!_activeCalls.TryGetValue(config.CallId, out var currentCallState) ||
                !ReferenceEquals(currentCallState, callState) ||
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

            var reservedMediaSlot = false;
            if (!_activeMediaStreams.TryGetValue(config.StreamId, out route))
            {
                if (!TryReserveQuota(_activeMediaStreamsByPrincipal, sender.QuotaKey, _maxMediaStreamsPerPrincipal))
                {
                    BoltServerMetrics.RecordQuotaRejection("media_streams");
                    _logger.LogWarning(
                        "Rejected MediaConfig because principal media stream limit was reached. client={ClientId} maxStreams={MaxStreams}",
                        sender.ClientId,
                        _maxMediaStreamsPerPrincipal);
                    return;
                }

                reservedMediaSlot = true;
                var candidate = new MediaStreamRoute
                {
                    Sender = sender,
                    CallId = config.CallId,
                };

                if (!_activeMediaStreams.TryAdd(config.StreamId, candidate))
                {
                    ReleaseQuota(_activeMediaStreamsByPrincipal, sender.QuotaKey);
                    reservedMediaSlot = false;
                    if (!_activeMediaStreams.TryGetValue(config.StreamId, out route))
                        return;
                }
                else
                {
                    route = candidate;
                }
            }

            if (route is null)
                return;

            if (route.Sender.StreamId != sender.StreamId || route.CallId != config.CallId)
            {
                if (reservedMediaSlot && _activeMediaStreams.TryRemove(config.StreamId, out _))
                    ReleaseQuota(_activeMediaStreamsByPrincipal, sender.QuotaKey);

                _logger.LogWarning(
                    "Rejected MediaConfig that attempted to reuse a stream route. streamId={StreamId} owner={Owner} sender={Sender}",
                    config.StreamId,
                    route.Sender.ClientId,
                    sender.ClientId);
                return;
            }

            AddMediaStream(callState, config.StreamId);
            route.AddRecipients(GetParticipantSnapshot(callState));
            recipients = route.GetRecipientSnapshot();
        }

        // Forward config to all recipients
        var data = buffer.AsMemory(0, length);
        foreach (var recipient in recipients)
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
            RejectMediaWhileHeld(owningCall, (FrameType)buffer[0], route.CallId, sender) ||
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
            await caller.SendAsync(writer, ct);
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
        await caller.SendAsync(ringWriter, ct);

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
                await route.Sender.SendAsync(keyReqWriter, ct);
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
        lock (callState.SyncRoot)
        {
            foreach (var streamId in GetMediaStreamSnapshot(callState))
            {
                if (_activeMediaStreams.TryGetValue(streamId, out var route))
                {
                    if (route.Sender.ServiceHash == recipientHash)
                    {
                        if (_activeMediaStreams.TryRemove(streamId, out var removedRoute))
                        {
                            ReleaseQuota(_activeMediaStreamsByPrincipal, removedRoute.Sender.QuotaKey);
                            RemoveMediaStream(callState, streamId);
                        }
                    }
                    else
                    {
                        route.RemoveRecipientsWhere(r => r.ServiceHash == recipientHash);
                    }
                }
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
            lock (callState.SyncRoot)
            {
                foreach (var streamId in GetMediaStreamSnapshot(callState))
                {
                    if (_activeMediaStreams.TryRemove(streamId, out var removedRoute))
                        ReleaseQuota(_activeMediaStreamsByPrincipal, removedRoute.Sender.QuotaKey);
                }
            }
        }
    }

    // ── Pub/Sub handlers ──

    private async Task HandleSubscribeFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadSubscribe(buffer.AsSpan(0, length), out var topicHash, out var durable, out var subscriberId, out var topic, out var actorAccessToken, out _))
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

        if (durable && _durableStore is not null)
        {
            DateTimeOffset? authorizationExpiresAtUtc = null;
            if (!string.IsNullOrWhiteSpace(actorAccessToken))
            {
                if (!TryReadJwtExpiration(actorAccessToken, out var expiration) ||
                    expiration <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning(
                        "Rejected durable Bolt subscription because the validated actor token has no usable expiry. client={ClientId} topic={Topic}",
                        conn.ClientId,
                        topic);
                    return;
                }

                authorizationExpiresAtUtc = expiration;
            }

            await HandleDurableSubscribeAsync(
                conn,
                topicHash,
                subscriberId,
                topic,
                authorizationExpiresAtUtc,
                ct);
            return;
        }

        var subscriptionAlreadyActive = IsLiveSubscriptionActive(conn, topicHash);
        if (!subscriptionAlreadyActive &&
            !TryReserveQuota(_subscriptionsByPrincipal, conn.QuotaKey, _maxSubscriptionsPerPrincipal))
        {
            BoltServerMetrics.RecordQuotaRejection("subscriptions");
            _logger.LogWarning(
                "Rejected Bolt subscription because principal subscription limit was reached. client={ClientId} maxSubscriptions={MaxSubscriptions}",
                conn.ClientId,
                _maxSubscriptionsPerPrincipal);
            return;
        }

        if (!TryRegisterTopicName(topicHash, topic))
        {
            if (!subscriptionAlreadyActive)
                ReleaseQuota(_subscriptionsByPrincipal, conn.QuotaKey);
            return;
        }

        if (!durable)
        {
            if (!AddLiveSubscription(conn, topicHash) && !subscriptionAlreadyActive)
                ReleaseQuota(_subscriptionsByPrincipal, conn.QuotaKey);
            _logger.LogDebug("Transient subscribe: topic={Topic}", topic);
            return;
        }

        if (_durableStore is null)
        {
            _logger.LogWarning("Durable subscribe requested but no IDurableQueueStore configured. Falling back to transient.");
            if (!AddLiveSubscription(conn, topicHash) && !subscriptionAlreadyActive)
                ReleaseQuota(_subscriptionsByPrincipal, conn.QuotaKey);
            return;
        }

    }

    private bool AddLiveSubscription(BoltHubConnection conn, int topicHash)
    {
        var topicSet = _liveSubscribersByTopic.GetOrAdd(topicHash, _ => new ConcurrentDictionary<BoltHubConnection, byte>());
        topicSet.TryAdd(conn, 0);

        var connSet = _liveSubscriptionsByConnection.GetOrAdd(conn, _ => new ConcurrentDictionary<int, byte>());
        return connSet.TryAdd(topicHash, 0);
    }

    private bool IsLiveSubscriptionActive(BoltHubConnection connection, int topicHash) =>
        _liveSubscriptionsByConnection.TryGetValue(connection, out var topics) &&
        topics.ContainsKey(topicHash);

    private async Task HandleDurableSubscribeAsync(
        BoltHubConnection connection,
        int topicHash,
        string subscriberId,
        string topic,
        DateTimeOffset? authorizationExpiresAtUtc,
        CancellationToken ct)
    {
        var durableStore = _durableStore!;
        var durableKey = (topicHash, subscriberId);
        var gate = GetDurableSubscriptionGate(durableKey);
        await gate.WaitAsync(ct);
        try
        {
            _liveDurableConnections.TryGetValue(durableKey, out var previousConnection);
            var alreadyActive = ReferenceEquals(previousConnection, connection);
            if (!alreadyActive &&
                !TryReserveQuota(
                    _subscriptionsByPrincipal,
                    connection.QuotaKey,
                    _maxSubscriptionsPerPrincipal))
            {
                BoltServerMetrics.RecordQuotaRejection("subscriptions");
                _logger.LogWarning(
                    "Rejected durable Bolt subscription because principal subscription limit was reached. client={ClientId} maxSubscriptions={MaxSubscriptions}",
                    connection.ClientId,
                    _maxSubscriptionsPerPrincipal);
                return;
            }

            var reservationHeld = !alreadyActive;
            try
            {
                if (!TryRegisterTopicName(topicHash, topic))
                    return;

                if (!await durableStore.TryRegisterDurableSubscriberAsync(
                        topicHash,
                        subscriberId,
                        _maxDurableSubscribersPerTopic,
                        ct))
                {
                    BoltServerMetrics.RecordQuotaRejection("durable_subscribers");
                    _logger.LogWarning(
                        "Rejected durable Bolt subscription because topic subscriber cardinality was reached. topic={Topic} maxSubscribers={MaxSubscribers}",
                        topic,
                        _maxDurableSubscribersPerTopic);
                    return;
                }

                if (!connection.IsAlive)
                    return;

                _liveDurableConnections[durableKey] = connection;
                _durableSubscriptionAuthorizations[durableKey] =
                    new DurableSubscriptionAuthorization(connection, authorizationExpiresAtUtc);
                _replayingDurableSubscriptions[durableKey] = new DurableReplayState(connection);
                if (previousConnection is not null && !alreadyActive)
                    ReleaseQuota(_subscriptionsByPrincipal, previousConnection.QuotaKey);
                reservationHeld = false;
            }
            finally
            {
                if (reservationHeld)
                    ReleaseQuota(_subscriptionsByPrincipal, connection.QuotaKey);
            }
        }
        finally
        {
            gate.Release();
        }

        if (!_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection) ||
            !ReferenceEquals(mappedConnection, connection))
        {
            return;
        }

        var fromSequence = await durableStore.GetLastAckedSequenceAsync(topicHash, subscriberId, ct);
        var maxBatch = _durableOptions?.MaxReplayBatchSize ?? 1000;
        var replayCount = 0;
        int replayedBatchCount;
        do
        {
            replayedBatchCount = 0;
            await foreach (var (seq, payload) in durableStore.ReadFromAsync(
                               topicHash,
                               subscriberId,
                               fromSequence,
                               maxBatch,
                               ct))
            {
                if (!_liveDurableConnections.TryGetValue(durableKey, out mappedConnection) ||
                    !ReferenceEquals(mappedConnection, connection))
                {
                    return;
                }

                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteEvent(writer, topicHash, subscriberId, seq, isReplay: true, payload);
                await connection.SendAsync(writer, ct);
                fromSequence = seq;
                replayCount++;
                replayedBatchCount++;
            }
        }
        while (replayedBatchCount == maxBatch && !ct.IsCancellationRequested);

        await CompleteDurableReplayAsync(connection, topicHash, subscriberId, fromSequence, ct);
        _logger.LogDebug(
            "Durable subscribe: topic={Topic} subscriber={Subscriber} replayed={Count}",
            topic,
            subscriberId,
            replayCount);
    }

    private SemaphoreSlim GetDurableSubscriptionGate((int TopicHash, string SubscriberId) key)
    {
        var index = (int)((uint)HashCode.Combine(key.TopicHash, key.SubscriberId) %
                          (uint)_durableSubscriptionGates.Length);
        return _durableSubscriptionGates[index];
    }

    private async Task CompleteDurableReplayAsync(
        BoltHubConnection conn,
        int topicHash,
        string subscriberId,
        long lastReplayedSequence,
        CancellationToken ct)
    {
        var durableKey = (topicHash, subscriberId);
        var gate = GetDurableSubscriptionGate(durableKey);
        while (!ct.IsCancellationRequested)
        {
            await gate.WaitAsync(ct);
            try
            {
                if (!_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection) ||
                    !ReferenceEquals(mappedConnection, conn) ||
                    !_replayingDurableSubscriptions.TryGetValue(durableKey, out var state) ||
                    !ReferenceEquals(state.Owner, conn))
                {
                    return;
                }

                var events = new List<(long Sequence, byte[] Payload)>();
                lock (state.SyncRoot)
                {
                    while (state.DeferredEvents.Count > 0)
                    {
                        var item = state.DeferredEvents.Dequeue();
                        state.DeferredBytes -= item.Payload.Length;
                        events.Add(item);
                    }

                    if (events.Count == 0)
                    {
                        state.AcceptingDeferredEvents = false;
                        _replayingDurableSubscriptions.TryRemove(
                            new KeyValuePair<(int TopicHash, string SubscriberId), DurableReplayState>(
                                durableKey,
                                state));
                        return;
                    }
                }

                foreach (var (sequence, payload) in events
                             .OrderBy(item => item.Sequence == 0 ? long.MaxValue : item.Sequence))
                {
                    if (sequence > 0 && sequence <= lastReplayedSequence)
                        continue;

                    var w = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteEvent(w, topicHash, subscriberId, sequence, isReplay: false, payload);
                    await conn.SendAsync(w, ct);
                    if (sequence > 0)
                        lastReplayedSequence = sequence;
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private void RemoveDurableReplayState(
        (int TopicHash, string SubscriberId) durableKey,
        BoltHubConnection owner)
    {
        if (_replayingDurableSubscriptions.TryGetValue(durableKey, out var replayState) &&
            ReferenceEquals(replayState.Owner, owner))
        {
            _replayingDurableSubscriptions.TryRemove(
                new KeyValuePair<(int TopicHash, string SubscriberId), DurableReplayState>(
                    durableKey,
                    replayState));
        }
    }

    private async Task HandleUnsubscribeFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadUnsubscribe(buffer.AsSpan(0, length), out var topicHash, out var topic, out var subscriberId, out var permanent, out var actorAccessToken, out _))
            return;

        var durable = !string.Equals(subscriberId, conn.ClientId, StringComparison.Ordinal);
        if (!await AuthorizeTopicAsync(
                conn,
                BoltTopicOperation.Unsubscribe,
                topic,
                topicHash,
                durable,
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

        if (!TryRegisterTopicName(topicHash, topic))
            return;

        if (durable)
        {
            var durableKey = (topicHash, subscriberId);
            var gate = GetDurableSubscriptionGate(durableKey);
            await gate.WaitAsync(ct);
            try
            {
                if (!_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection))
                {
                    if (permanent && _durableStore is not null)
                        await _durableStore.UnregisterDurableSubscriberAsync(topicHash, subscriberId, ct);
                }
                else if (ReferenceEquals(mappedConnection, conn))
                {
                    if (permanent && _durableStore is not null)
                        await _durableStore.UnregisterDurableSubscriberAsync(topicHash, subscriberId, ct);

                    if (_liveDurableConnections.TryRemove(
                            new KeyValuePair<(int TopicHash, string SubscriberId), BoltHubConnection>(
                                durableKey,
                                conn)))
                    {
                        _durableSubscriptionAuthorizations.TryRemove(durableKey, out _);
                        ReleaseQuota(_subscriptionsByPrincipal, conn.QuotaKey);
                    }

                    RemoveDurableReplayState(durableKey, conn);
                }
                else if (permanent)
                {
                    _logger.LogWarning(
                        "Rejected permanent durable unsubscribe from non-current subscriber session. client={ClientId} topic={Topic} subscriber={Subscriber}",
                        conn.ClientId,
                        topic,
                        subscriberId);
                }
            }
            finally
            {
                gate.Release();
            }

            _logger.LogDebug("Unsubscribe: topic={Topic} subscriber={Subscriber}", topic, subscriberId);
            return;
        }

        if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSet))
            topicSet.TryRemove(conn, out _);

        if (_liveSubscriptionsByConnection.TryGetValue(conn, out var connSet) &&
            connSet.TryRemove(topicHash, out _))
        {
            ReleaseQuota(_subscriptionsByPrincipal, conn.QuotaKey);
        }

        _logger.LogDebug("Unsubscribe: topic={Topic} subscriber={Subscriber}", topic, subscriberId);
    }

    private async Task HandlePublishFrameAsync(BoltHubConnection publisher, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadPublish(buffer.AsSpan(0, length), out var topicHash, out var topic, out var durableEligible, out var payloadOffset, out var payloadLength, out _))
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

        if (!TryRegisterTopicName(topicHash, topic))
            return;

        var payload = buffer.AsMemory(payloadOffset, payloadLength);

        var deliveredConnections = new HashSet<BoltHubConnection>();

        // Durable path: enqueue and deliver live if connected
        if (durableEligible && _durableStore is not null)
        {
            var durableSubs = await _durableStore.GetDurableSubscribersAsync(topicHash, ct);
            foreach (var subscriberId in durableSubs)
            {
                var durableKey = (topicHash, subscriberId);
                var publishLock = GetDurableSubscriptionGate(durableKey);

                await publishLock.WaitAsync(ct);
                try
                {
                    if (!await _durableStore.IsDurableSubscriberRegisteredAsync(
                            topicHash,
                            subscriberId,
                            ct))
                    {
                        continue;
                    }

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
                        if (_replayingDurableSubscriptions.TryGetValue(durableKey, out var replayState) &&
                            ReferenceEquals(replayState.Owner, liveConn))
                        {
                            if (HandleEventDuringReplay(replayState, seq, payload))
                                continue;
                        }

                        var w = RentedBufferWriter.GetThreadLocal();
                        BoltCodec.WriteEvent(w, topicHash, subscriberId, seq, isReplay: false, payload.Span);
                        try { await liveConn.SendAsync(w, ct); }
                        catch { }
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
            foreach (var (durableKey, snapshotConnection) in _liveDurableConnections)
            {
                if (durableKey.TopicHash != topicHash || snapshotConnection == publisher)
                    continue;

                var gate = GetDurableSubscriptionGate(durableKey);
                await gate.WaitAsync(ct);
                try
                {
                    if (!_liveDurableConnections.TryGetValue(durableKey, out var liveConn) ||
                        !ReferenceEquals(liveConn, snapshotConnection) ||
                        liveConn == publisher)
                    {
                        continue;
                    }

                    if (_replayingDurableSubscriptions.TryGetValue(durableKey, out var replayState) &&
                        ReferenceEquals(replayState.Owner, liveConn))
                    {
                        if (HandleEventDuringReplay(replayState, 0, payload))
                            continue;
                    }

                    var w = RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteEvent(w, topicHash, durableKey.SubscriberId, sequenceNumber: 0, isReplay: false, payload.Span);
                    try { await liveConn.SendAsync(w, ct); }
                    catch { }
                    deliveredConnections.Add(liveConn);
                }
                finally
                {
                    gate.Release();
                }
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
                BoltCodec.WriteEvent(w, topicHash, sequenceNumber: 0, isReplay: false, payload.Span);
                try { await subscriberConn.SendAsync(w, ct); }
                catch { }
            }
        }
    }

    private bool HandleEventDuringReplay(
        DurableReplayState state,
        long sequence,
        ReadOnlyMemory<byte> payload)
    {
        lock (state.SyncRoot)
        {
            if (!state.AcceptingDeferredEvents)
                return false;

            var byteCapacity = Math.Max(
                1,
                _durableOptions?.MaxReplayDeferredBytesPerSubscription ?? 8L * 1024 * 1024);
            if (payload.Length > byteCapacity - state.DeferredBytes)
            {
                return true;
            }

            var copy = payload.ToArray();
            state.DeferredEvents.Enqueue((sequence, copy));
            state.DeferredBytes += copy.Length;
            BoltServerMetrics.RecordReplayDeferredBytes(state.DeferredBytes);
            return true;
        }
    }

    private async Task HandleAckFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadAck(buffer.AsSpan(0, length), out var topicHash, out var topic, out var subscriberId, out var upToSequence, out var actorAccessToken, out _))
            return;

        if (_durableStore is null) return;

        var durableKey = (topicHash, subscriberId);
        var gate = GetDurableSubscriptionGate(durableKey);
        await gate.WaitAsync(ct);
        try
        {
            if (!_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection) ||
                !ReferenceEquals(mappedConnection, conn) ||
                !_durableSubscriptionAuthorizations.TryGetValue(durableKey, out var authorization) ||
                !ReferenceEquals(authorization.Connection, conn))
            {
                _logger.LogWarning(
                    "Rejected durable ack for non-current subscriber session. client={ClientId} topic={Topic} subscriber={Subscriber}",
                    conn.ClientId,
                    topic,
                    subscriberId);
                return;
            }

            if (authorization.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= DateTimeOffset.UtcNow)
            {
                RemoveDurableBindingUnderGate(durableKey, conn);
                _logger.LogWarning(
                    "Rejected durable ack because subscription authorization expired. client={ClientId} topic={Topic} subscriber={Subscriber}",
                    conn.ClientId,
                    topic,
                    subscriberId);
                return;
            }
        }
        finally
        {
            gate.Release();
        }

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
            return;
        }

        await gate.WaitAsync(ct);
        try
        {
            if (!_liveDurableConnections.TryGetValue(durableKey, out var mappedConnection) ||
                !ReferenceEquals(mappedConnection, conn) ||
                !_durableSubscriptionAuthorizations.TryGetValue(durableKey, out var authorization) ||
                !ReferenceEquals(authorization.Connection, conn))
            {
                _logger.LogWarning(
                    "Rejected durable ack because subscriber ownership changed during authorization. client={ClientId} topic={Topic} subscriber={Subscriber}",
                    conn.ClientId,
                    topic,
                    subscriberId);
                return;
            }

            if (authorization.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= DateTimeOffset.UtcNow)
            {
                RemoveDurableBindingUnderGate(durableKey, conn);
                return;
            }

            if (!TryRegisterTopicName(topicHash, topic))
                return;

            await _durableStore.AckAsync(topicHash, subscriberId, upToSequence, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Durable ack failed for topic={TopicHash} subscriber={Subscriber}", topicHash, subscriberId);
        }
        finally
        {
            gate.Release();
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
            try
            {
                if (!await authorizer.AuthorizeAsync(context, ct))
                    return false;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Rejected Bolt topic operation because authorizer {Authorizer} failed. client={ClientId} topic={Topic} operation={Operation}",
                    authorizer.GetType().Name,
                    connection.ClientId,
                    topic,
                    operation);
                return false;
            }
        }

        return true;
    }

    private static bool TryReadJwtExpiration(string token, out DateTimeOffset expiration)
    {
        expiration = default;
        var segments = token.Split('.');
        if (segments.Length != 3 || segments[1].Length == 0)
            return false;

        try
        {
            var payloadSegment = segments[1].Replace('-', '+').Replace('_', '/');
            payloadSegment = payloadSegment.PadRight(
                payloadSegment.Length + ((4 - payloadSegment.Length % 4) % 4),
                '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payloadSegment));
            if (!document.RootElement.TryGetProperty("exp", out var exp) ||
                !exp.TryGetInt64(out var unixSeconds))
            {
                return false;
            }

            expiration = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            expiration = default;
            return false;
        }
    }

    private static void ValidateRateLimitOptions(int rate, int burst, string category)
    {
        if (rate < 0 || burst < 0 || (rate == 0) != (burst == 0))
        {
            throw new InvalidOperationException(
                $"Bolt {category} rate and burst must either both be zero or both be positive.");
        }
    }

    private BoltPrincipalRateLimiter AttachPrincipalRateLimiter(string quotaKey)
    {
        while (true)
        {
            var limiter = _rateLimitersByPrincipal.GetOrAdd(
                quotaKey,
                _ => new BoltPrincipalRateLimiter(
                    _rpcRequestsPerSecond,
                    _rpcRequestBurst,
                    _rpcInboundBytesPerSecond,
                    _rpcInboundByteBurst));
            if (limiter.TryAttach())
                return limiter;

            _rateLimitersByPrincipal.TryRemove(
                new KeyValuePair<string, BoltPrincipalRateLimiter>(quotaKey, limiter));
        }
    }

    private void DetachPrincipalRateLimiter(BoltHubConnection connection)
    {
        var limiter = connection.RateLimiter;
        connection.RateLimiter = null;
        if (limiter is null || !limiter.DetachAndRetire())
            return;

        _rateLimitersByPrincipal.TryRemove(
            new KeyValuePair<string, BoltPrincipalRateLimiter>(connection.QuotaKey, limiter));
        limiter.Dispose();
    }

    private static async Task SendRateLimitedResponseAsync(
        BoltHubConnection caller,
        Guid requestId,
        CancellationToken ct)
    {
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteResponse(writer, requestId, HttpStatusCode.TooManyRequests, ReadOnlySpan<byte>.Empty);
        await caller.SendAsync(writer, ct);
    }

    private bool TryAdmitRpc(
        BoltHubConnection connection,
        int payloadBytes,
        string frameCategory,
        bool isPush)
    {
        var limiter = connection.RateLimiter;
        if (limiter is null)
            return true;

        if (!limiter.TryAcquire(payloadBytes, out var rejectionReason))
        {
            if (rejectionReason == "request_rate")
                Interlocked.Increment(ref _requestRateRejectionTotal);
            else
                Interlocked.Increment(ref _byteRateRejectionTotal);
            if (isPush)
                Interlocked.Increment(ref _pushRateRejectionTotal);
            BoltServerMetrics.RecordRateLimitRejection(frameCategory, rejectionReason, isPush);
            return false;
        }

        return true;
    }

    private PendingInvocationAddResult TryAddPendingInvocation(Guid requestId, PendingInvocation pending)
    {
        if (!_pendingInvocationSlots.Wait(0))
            return PendingInvocationAddResult.CapacityExceeded;

        if (!TryReserveQuota(
                _pendingInvocationsByPrincipal,
                pending.Caller.QuotaKey,
                _maxPendingRpcCallsPerPrincipal))
        {
            _pendingInvocationSlots.Release();
            return PendingInvocationAddResult.CapacityExceeded;
        }

        if (_pendingInvocations.TryAdd(requestId, pending))
            return PendingInvocationAddResult.Added;

        ReleaseQuota(_pendingInvocationsByPrincipal, pending.Caller.QuotaKey);
        _pendingInvocationSlots.Release();
        return PendingInvocationAddResult.Duplicate;
    }

    private bool TryRemovePendingInvocation(
        Guid requestId,
        PendingInvocation expected,
        out PendingInvocation pending)
    {
        if (!_pendingInvocations.TryRemove(new KeyValuePair<Guid, PendingInvocation>(requestId, expected)))
        {
            pending = null!;
            return false;
        }

        pending = expected;
        ReleasePendingInvocationCapacity(pending);
        return true;
    }

    private void ReleasePendingInvocationCapacity(PendingInvocation pending)
    {
        ReleaseQuota(_pendingInvocationsByPrincipal, pending.Caller.QuotaKey);
        _pendingInvocationSlots.Release();
    }

    private string? ResolvePrincipalQuotaKey(ClaimsPrincipal? user, string clientId)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return $"anonymous-client:{clientId}";

        var identityValue = user is not null && HasRequiredServiceScope(user)
            ? ResolveServiceIdentityName(user)
            : user?.FindFirst("sub")?.Value ??
              user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
              user?.Identity?.Name;

        return string.IsNullOrWhiteSpace(identityValue)
            ? null
            : $"principal:{identityValue}";
    }

    private static bool TryReserveQuota(
        ConcurrentDictionary<string, int> counts,
        string quotaKey,
        int maximum)
    {
        while (true)
        {
            if (!counts.TryGetValue(quotaKey, out var current))
            {
                if (counts.TryAdd(quotaKey, 1))
                    return true;

                continue;
            }

            if (current >= maximum)
                return false;

            if (counts.TryUpdate(quotaKey, current + 1, current))
                return true;
        }
    }

    private static void ReleaseQuota(ConcurrentDictionary<string, int> counts, string quotaKey)
    {
        while (counts.TryGetValue(quotaKey, out var current))
        {
            if (current <= 1)
            {
                if (counts.TryRemove(new KeyValuePair<string, int>(quotaKey, current)))
                    return;
            }
            else if (counts.TryUpdate(quotaKey, current - 1, current))
            {
                return;
            }
        }
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

        if (hasServiceScope &&
            !string.IsNullOrWhiteSpace(serviceClaim) &&
            IsRegistrationMigrationAllowed(serviceClaim, clientId, clientName))
        {
            _logger.LogWarning(
                "Allowed Bolt registration through an expiring migration mapping. authenticatedService={AuthenticatedService} clientId={ClientId} clientName={ClientName}",
                serviceClaim,
                clientId,
                clientName);
            return true;
        }

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

    private static void ValidateMigrationAllowances(
        IReadOnlyCollection<BoltRegistrationMigrationAllowance> allowances)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var allowance in allowances)
        {
            if (string.IsNullOrWhiteSpace(allowance.AuthenticatedServiceName) ||
                string.IsNullOrWhiteSpace(allowance.ClientId) ||
                string.IsNullOrWhiteSpace(allowance.ClientName))
            {
                throw new InvalidOperationException(
                    "Bolt registration migration allowances require AuthenticatedServiceName, ClientId, and ClientName.");
            }

            if (allowance.ExpiresAtUtc <= now)
            {
                throw new InvalidOperationException(
                    $"Bolt registration migration allowance for '{allowance.ClientName}' is expired.");
            }

            if (allowance.ExpiresAtUtc > now.Add(MaxMigrationAllowanceLifetime))
            {
                throw new InvalidOperationException(
                    $"Bolt registration migration allowance for '{allowance.ClientName}' exceeds the seven-day maximum lifetime.");
            }
        }
    }

    private bool IsRegistrationMigrationAllowed(
        string authenticatedServiceName,
        string clientId,
        string clientName)
    {
        var now = DateTimeOffset.UtcNow;
        return _registrationMigrationAllowances.Any(allowance =>
            allowance.ExpiresAtUtc > now &&
            string.Equals(
                allowance.AuthenticatedServiceName,
                authenticatedServiceName,
                StringComparison.Ordinal) &&
            string.Equals(allowance.ClientId, clientId, StringComparison.Ordinal) &&
            string.Equals(allowance.ClientName, clientName, StringComparison.Ordinal));
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

    private bool TryAddServiceConnection(
        BoltHubConnection connection,
        string clientId,
        out string existingClientId)
    {
        while (true)
        {
            var route = _connectionsByServiceHash.GetOrAdd(
                connection.ServiceHash,
                _ => new ServiceRoute(clientId));

            lock (route.SyncRoot)
            {
                if (route.Retired)
                    continue;

                existingClientId = route.ClientId;
                if (!string.Equals(route.ClientId, clientId, StringComparison.Ordinal))
                    return false;

                var connections = Volatile.Read(ref route.Connections);
                if (!connections.Any(candidate => candidate.StreamId == connection.StreamId))
                    Volatile.Write(ref route.Connections, [.. connections, connection]);
                return true;
            }
        }
    }

    private void RemoveServiceConnection(BoltHubConnection connection)
    {
        if (!_connectionsByServiceHash.TryGetValue(connection.ServiceHash, out var route))
            return;

        lock (route.SyncRoot)
        {
            if (route.Retired)
                return;

            var updated = Volatile.Read(ref route.Connections)
                .Where(candidate => candidate.StreamId != connection.StreamId)
                .ToArray();
            Volatile.Write(ref route.Connections, updated);
            if (updated.Length > 0)
                return;

            route.Retired = true;
            _connectionsByServiceHash.TryRemove(
                new KeyValuePair<int, ServiceRoute>(connection.ServiceHash, route));
            _roundRobinIndex.TryRemove(connection.ServiceHash, out _);
        }
    }

    private BoltHubConnection? GetRecipient(int serviceHash)
    {
        if (!_connectionsByServiceHash.TryGetValue(serviceHash, out var route))
            return null;

        var connections = Volatile.Read(ref route.Connections);

        // Direct iteration — no LINQ, no List allocation
        BoltHubConnection? firstAlive = null;
        int aliveCount = 0;

        foreach (var client in connections)
        {
            if (client.IsRegistered && client.IsAlive)
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
        foreach (var client in connections)
        {
            if (client.IsRegistered && client.IsAlive)
            {
                if (current == targetIdx) return client;
                current++;
            }
        }

        return firstAlive;
    }

    private async Task SendInvocationTerminalResponseAsync(
        Guid requestId,
        BoltHubConnection caller,
        HttpStatusCode statusCode,
        string reason,
        CancellationToken ct)
    {
        if (!caller.IsAlive)
        {
            _logger.LogDebug(
                "Skipped terminal Bolt invocation response because caller is disconnected. requestId={RequestId} caller={CallerClientId} callerStream={CallerStreamId} statusCode={StatusCode} reason={Reason}",
                requestId,
                caller.ClientId,
                caller.StreamId,
                (int)statusCode,
                reason);
            return;
        }

        try
        {
            var writer = new ArrayBufferWriter<byte>(BoltCodec.ResponseHeaderSize);
            BoltCodec.WriteResponse(writer, requestId, statusCode, ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(writer.WrittenMemory, ct);

            _logger.LogDebug(
                "Sent terminal Bolt invocation response. requestId={RequestId} caller={CallerClientId} callerStream={CallerStreamId} statusCode={StatusCode} reason={Reason}",
                requestId,
                caller.ClientId,
                caller.StreamId,
                (int)statusCode,
                reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Failed to send terminal Bolt invocation response. requestId={RequestId} caller={CallerClientId} callerStream={CallerStreamId} statusCode={StatusCode} reason={Reason}",
                requestId,
                caller.ClientId,
                caller.StreamId,
                (int)statusCode,
                reason);
        }
    }

    private async Task RemoveConnectionAsync(BoltHubConnection connection)
    {
        using var notificationCts = new CancellationTokenSource(_transportCloseTimeout);
        var peerNotifications = new List<Task>();

        if (connection.ClientId is not null)
        {
            ReleaseQuota(_connectionCountsByPrincipal, connection.QuotaKey);
            DetachPrincipalRateLimiter(connection);
            _connectionsByStreamId.TryRemove(connection.StreamId, out _);

            RemoveServiceConnection(connection);

            _logger.LogInformation("Client disconnected: {ClientId} ({ClientName})", connection.ClientId, connection.ClientName);
        }

        foreach (var (requestId, pending) in _pendingInvocations)
        {
            if (pending.Caller.StreamId != connection.StreamId &&
                pending.ExpectedResponder.StreamId != connection.StreamId)
            {
                continue;
            }

            if (!TryRemovePendingInvocation(requestId, pending, out var removedPending))
                continue;

            if (removedPending.ExpectedResponder.StreamId == connection.StreamId &&
                removedPending.Caller.StreamId != connection.StreamId &&
                removedPending.Caller.IsAlive)
            {
                peerNotifications.Add(SendUnavailableResponseAsync(
                    removedPending.Caller,
                    requestId,
                    notificationCts.Token));
            }
            else if (removedPending.Caller.StreamId == connection.StreamId &&
                     removedPending.ExpectedResponder.StreamId != connection.StreamId &&
                     removedPending.ExpectedResponder.IsAlive)
            {
                BoltServerMetrics.RecordRequestCancellation();
                peerNotifications.Add(SendRequestCancelAsync(
                    removedPending.ExpectedResponder,
                    requestId,
                    notificationCts.Token));
            }
        }

        foreach (var (streamId, route) in _activeStreams)
        {
            if (route.Sender.StreamId != connection.StreamId && route.Recipient.StreamId != connection.StreamId)
                continue;

            if (RemoveStreamRoute(
                    streamId,
                    route,
                    releaseLargeRpcPendingInvocation: true,
                    out var releasedOwnership))
            {
                if (releasedOwnership is not null &&
                    releasedOwnership.PendingInvocation.ExpectedResponder.StreamId == connection.StreamId &&
                    releasedOwnership.PendingInvocation.Caller.IsAlive)
                {
                    peerNotifications.Add(SendUnavailableResponseAsync(
                        releasedOwnership.PendingInvocation.Caller,
                        releasedOwnership.RequestId,
                        notificationCts.Token));
                }

                var survivingPeer = route.Sender.StreamId == connection.StreamId
                    ? route.Recipient
                    : route.Sender;
                if (survivingPeer.StreamId != connection.StreamId && survivingPeer.IsAlive)
                {
                    peerNotifications.Add(SendUnavailableStreamCloseAsync(
                        survivingPeer,
                        streamId,
                        notificationCts.Token));
                }
            }
        }

        if (peerNotifications.Count > 0)
        {
            try { await Task.WhenAll(peerNotifications).WaitAsync(notificationCts.Token); }
            catch (OperationCanceledException) when (notificationCts.IsCancellationRequested) { }
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
                    _ = participant.SendAsync(writer, CancellationToken.None);
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
            if (route.Sender.StreamId == connection.StreamId &&
                _activeMediaStreams.TryRemove(streamId, out var removedRoute))
            {
                ReleaseQuota(_activeMediaStreamsByPrincipal, removedRoute.Sender.QuotaKey);
            }
        }

        // Clean up pub/sub subscriptions for this connection
        if (_liveSubscriptionsByConnection.TryRemove(connection, out var topics))
        {
            foreach (var (topicHash, _) in topics)
            {
                if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSet))
                    topicSet.TryRemove(connection, out _);
                ReleaseQuota(_subscriptionsByPrincipal, connection.QuotaKey);
            }
        }

        // Remove this connection from any live durable bindings
        var keysToRemove = _liveDurableConnections.Where(kvp => kvp.Value == connection).Select(kvp => kvp.Key).ToList();
        foreach (var key in keysToRemove)
        {
            var gate = GetDurableSubscriptionGate(key);
            using var gateCts = new CancellationTokenSource(_transportCloseTimeout);
            var lockTaken = false;
            try
            {
                await gate.WaitAsync(gateCts.Token);
                lockTaken = true;
            }
            catch (OperationCanceledException) when (gateCts.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Durable subscription cleanup gate timed out; cleanup will retry. topicHash={TopicHash} subscriber={Subscriber}",
                    key.TopicHash,
                    key.SubscriberId);
                _ = CleanupDurableBindingWhenAvailableAsync(key, connection);
                continue;
            }

            try
            {
                RemoveDurableBindingUnderGate(key, connection);
            }
            finally
            {
                if (lockTaken)
                    gate.Release();
            }
        }

        await NotifyClientDisconnectedAsync(connection, CancellationToken.None);
    }

    private async Task SendUnavailableResponseAsync(
        BoltHubConnection caller,
        Guid requestId,
        CancellationToken ct)
    {
        try
        {
            var writer = new ArrayBufferWriter<byte>(BoltCodec.ResponseHeaderSize);
            BoltCodec.WriteResponse(
                writer,
                requestId,
                HttpStatusCode.ServiceUnavailable,
                ReadOnlySpan<byte>.Empty);
            await caller.SendAsync(writer.WrittenMemory, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Failed to notify caller that RPC {RequestId} lost its responder", requestId);
        }
    }

    private async Task SendLargeRpcTerminalResponseAsync(
        LargeRpcPendingInvocationOwnership ownership,
        HttpStatusCode statusCode,
        CancellationToken ct)
    {
        var pending = ownership.PendingInvocation;
        await SendLargeRpcTerminalResponseAsync(
            pending.Caller,
            pending.ExpectedResponder.ServiceHash,
            ownership.RequestId,
            statusCode,
            ct);
    }

    private async Task SendLargeRpcTerminalResponseAsync(
        BoltHubConnection caller,
        int responderHash,
        Guid requestId,
        HttpStatusCode statusCode,
        CancellationToken ct)
    {
        if (!caller.IsAlive)
            return;

        try
        {
            var payload = new byte[18];
            requestId.TryWriteBytes(payload);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16), (short)statusCode);

            var writer = new ArrayBufferWriter<byte>(64);
            BoltCodec.WritePush(
                writer,
                Guid.NewGuid(),
                caller.ServiceHash,
                responderHash,
                LargeRpcResponseHash,
                payload);
            await caller.SendAsync(writer.WrittenMemory, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Failed to complete aborted large RPC {RequestId} with status {StatusCode}",
                requestId,
                statusCode);
        }
    }

    private async Task SendUnavailableStreamCloseAsync(
        BoltHubConnection peer,
        Guid streamId,
        CancellationToken ct) =>
        await SendStreamCloseAsync(peer, streamId, HttpStatusCode.ServiceUnavailable, ct);

    private async Task SendStreamCloseAsync(
        BoltHubConnection peer,
        Guid streamId,
        HttpStatusCode statusCode,
        CancellationToken ct)
    {
        try
        {
            var writer = new ArrayBufferWriter<byte>(BoltCodec.StreamCloseSize);
            BoltCodec.WriteStreamClose(writer, streamId, statusCode);
            await peer.SendAsync(writer.WrittenMemory, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Failed to close stream {StreamId} after its peer disconnected", streamId);
        }
    }

    private async Task CleanupDurableBindingWhenAvailableAsync(
        (int TopicHash, string SubscriberId) key,
        BoltHubConnection connection)
    {
        var gate = GetDurableSubscriptionGate(key);
        try
        {
            await gate.WaitAsync(_shutdownCts.Token);
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            return;
        }

        try
        {
            RemoveDurableBindingUnderGate(key, connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Deferred durable subscription cleanup failed. topicHash={TopicHash} subscriber={Subscriber}",
                key.TopicHash,
                key.SubscriberId);
        }
        finally
        {
            gate.Release();
        }
    }

    private void RemoveDurableBindingUnderGate(
        (int TopicHash, string SubscriberId) key,
        BoltHubConnection connection)
    {
        if (_liveDurableConnections.TryRemove(
                new KeyValuePair<(int TopicHash, string SubscriberId), BoltHubConnection>(key, connection)))
        {
            _durableSubscriptionAuthorizations.TryRemove(key, out _);
            ReleaseQuota(_subscriptionsByPrincipal, connection.QuotaKey);
        }

        RemoveDurableReplayState(key, connection);
    }

    private async Task NotifyClientRegisteredAsync(BoltHubConnection connection, CancellationToken ct)
    {
        var handler = ClientRegistered;
        if (handler is null || connection.ClientId is null)
            return;

        var connectionEvent = BoltClientConnectionEvent.FromConnection(connection);
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadlineCts.CancelAfter(_transportCloseTimeout);
        foreach (Func<BoltClientConnectionEvent, CancellationToken, Task> subscriber in handler.GetInvocationList())
        {
            try
            {
                await subscriber(connectionEvent, deadlineCts.Token).WaitAsync(deadlineCts.Token);
            }
            catch (OperationCanceledException) when (deadlineCts.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Client registered subscriber deadline elapsed for {ClientId}",
                    connection.ClientId);
                break;
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
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadlineCts.CancelAfter(_transportCloseTimeout);
        foreach (Func<BoltClientConnectionEvent, CancellationToken, Task> subscriber in handler.GetInvocationList())
        {
            try
            {
                await subscriber(connectionEvent, deadlineCts.Token).WaitAsync(deadlineCts.Token);
            }
            catch (OperationCanceledException) when (deadlineCts.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Client disconnected subscriber deadline elapsed for {ClientId}",
                    connection.ClientId);
                break;
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
                if (TryRemovePendingInvocation(requestId, pending, out var removed))
                {
                    var ageMs = now - removed.Timestamp;
                    BoltServerMetrics.RecordRpcDuration(ageMs, "timeout");
                    _logger.LogWarning(
                        "Completing stale Bolt invocation at hub timeout. requestId={RequestId} caller={CallerClientId} callerStream={CallerStreamId} responder={ResponderClientId} responderStream={ResponderStreamId} ageMs={AgeMs} timeoutMs={TimeoutMs}",
                        requestId,
                        removed.Caller.ClientId,
                        removed.Caller.StreamId,
                        removed.ExpectedResponder.ClientId,
                        removed.ExpectedResponder.StreamId,
                        ageMs,
                        _invocationTimeoutMs);

                    _ = SendInvocationTerminalResponseAsync(
                        requestId,
                        removed.Caller,
                        HttpStatusCode.GatewayTimeout,
                        "hub-invocation-timeout",
                        CancellationToken.None);
                }
            }
        }

        // Cleanup stale Ringing calls (unanswered for > 30 seconds)
        var utcNow = DateTime.UtcNow;
        foreach (var (key, authorization) in _durableSubscriptionAuthorizations)
        {
            if (authorization.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= DateTimeOffset.UtcNow)
                _ = ExpireDurableAuthorizationAsync(key, authorization);
        }

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
                    _ = callState.CallerConnection.SendAsync(writer, CancellationToken.None);
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
                    _ = callState.CalleeConnection.SendAsync(writer, CancellationToken.None);
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

    private async Task SendRequestCancelAsync(
        BoltHubConnection responder,
        Guid requestId,
        CancellationToken ct)
    {
        try
        {
            var writer = new ArrayBufferWriter<byte>(BoltCodec.RequestCancelSize);
            BoltCodec.WriteRequestCancel(writer, requestId);
            await responder.SendAsync(writer.WrittenMemory, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Failed to cancel RPC {RequestId} after its caller disconnected", requestId);
        }
    }

    private async Task ExpireDurableAuthorizationAsync(
        (int TopicHash, string SubscriberId) key,
        DurableSubscriptionAuthorization authorization)
    {
        var gate = GetDurableSubscriptionGate(key);
        try
        {
            await gate.WaitAsync(_shutdownCts.Token);
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            return;
        }

        try
        {
            if (_durableSubscriptionAuthorizations.TryGetValue(key, out var current) &&
                ReferenceEquals(current, authorization) &&
                current.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                RemoveDurableBindingUnderGate(key, authorization.Connection);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public int ConnectedClients => _connectionsByStreamId.Count;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _shutdownCts.Cancel();
        _mediaTapCts.Cancel();
        _mediaTapChannel.Writer.TryComplete();
        _cleanupTimer.Dispose();
        foreach (var (requestId, pending) in _pendingInvocations)
            TryRemovePendingInvocation(requestId, pending, out _);
        foreach (var invocation in _localInvocations.Values)
            invocation.Cancellation.Cancel();
        foreach (var (quotaKey, limiter) in _rateLimitersByPrincipal)
        {
            if (_rateLimitersByPrincipal.TryRemove(
                    new KeyValuePair<string, BoltPrincipalRateLimiter>(quotaKey, limiter)))
            {
                limiter.Dispose();
            }
        }
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

internal sealed class PendingInvocation(
    BoltHubConnection caller,
    BoltHubConnection expectedResponder,
    long timestamp)
{
    public BoltHubConnection Caller { get; } = caller;
    public BoltHubConnection ExpectedResponder { get; } = expectedResponder;
    public long Timestamp { get; } = timestamp;
}

internal sealed class LocalInvocation(
    BoltHubConnection caller,
    CancellationTokenSource cancellation)
{
    private int _canceledByCaller;

    public BoltHubConnection Caller { get; } = caller;
    public CancellationTokenSource Cancellation { get; } = cancellation;
    public bool CanceledByCaller => Volatile.Read(ref _canceledByCaller) != 0;

    public void CancelByCaller()
    {
        Interlocked.Exchange(ref _canceledByCaller, 1);
        Cancellation.Cancel();
    }
}

internal sealed record LargeRpcPendingInvocationOwnership(
    Guid RequestId,
    PendingInvocation PendingInvocation);

internal sealed class StreamRoute(BoltHubConnection sender, BoltHubConnection recipient, int commandHash)
{
    public BoltHubConnection Sender { get; } = sender;
    public BoltHubConnection Recipient { get; } = recipient;
    public int CommandHash { get; } = commandHash;
    public object SyncRoot { get; } = new();
    public bool LargeRpcRequestTracked { get; set; }
    public LargeRpcPendingInvocationOwnership? LargeRpcRequestOwnership { get; set; }
    public long LargeRpcExpectedPayloadBytes { get; set; }
    public long LargeRpcRequestBytesReceived { get; set; }
    public bool LargeRpcResponseValidated { get; set; }
    public Guid LargeRpcResponseRequestId { get; set; }
    public long LargeRpcResponseExpectedPayloadBytes { get; set; }
    public long LargeRpcResponseBytesReceived { get; set; }
}

public sealed class BoltSendEnqueueTimeoutException(string message, Exception innerException)
    : TimeoutException(message, innerException);

public sealed class BoltSendQueueByteCapacityExceededException(string message)
    : InvalidOperationException(message);

public sealed class BoltTransportSendTimeoutException(string message, Exception innerException)
    : TimeoutException(message, innerException);

public sealed class BoltTransportSendException(string message, Exception? innerException = null)
    : IOException(message, innerException);

internal sealed class BoltPrincipalRateLimiter : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly int _requestsPerSecond;
    private readonly int _requestBurst;
    private readonly int _inboundBytesPerSecond;
    private readonly int _inboundByteBurst;
    private double _requestTokens;
    private double _byteTokens;
    private long _lastRefillTimestamp;
    private int _connectionCount;
    private bool _retired;

    public BoltPrincipalRateLimiter(
        int requestsPerSecond,
        int requestBurst,
        int inboundBytesPerSecond,
        int inboundByteBurst)
    {
        _requestsPerSecond = requestsPerSecond;
        _requestBurst = requestBurst;
        _inboundBytesPerSecond = inboundBytesPerSecond;
        _inboundByteBurst = inboundByteBurst;
        _requestTokens = requestBurst;
        _byteTokens = inboundByteBurst;
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    public bool TryAttach()
    {
        lock (_syncRoot)
        {
            if (_retired)
                return false;

            _connectionCount++;
            return true;
        }
    }

    public bool DetachAndRetire()
    {
        lock (_syncRoot)
        {
            if (_connectionCount > 0)
                _connectionCount--;
            if (_connectionCount != 0 || _retired)
                return false;

            _retired = true;
            return true;
        }
    }

    public bool TryAcquire(int payloadBytes, out string rejectionReason)
    {
        lock (_syncRoot)
        {
            RefillTokens();

            if (_requestsPerSecond > 0)
            {
                if (_requestTokens < 1d)
                {
                    rejectionReason = "request_rate";
                    return false;
                }

                _requestTokens -= 1d;
            }

            if (_inboundBytesPerSecond > 0 && payloadBytes > 0)
            {
                if (payloadBytes > _inboundByteBurst || _byteTokens < payloadBytes)
                {
                    rejectionReason = "byte_rate";
                    return false;
                }

                _byteTokens -= payloadBytes;
            }

            rejectionReason = string.Empty;
            return true;
        }
    }

    private void RefillTokens()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedSeconds = (now - _lastRefillTimestamp) / (double)Stopwatch.Frequency;
        if (elapsedSeconds <= 0d)
            return;

        _lastRefillTimestamp = now;
        if (_requestsPerSecond > 0)
        {
            _requestTokens = Math.Min(
                _requestBurst,
                _requestTokens + elapsedSeconds * _requestsPerSecond);
        }

        if (_inboundBytesPerSecond > 0)
        {
            _byteTokens = Math.Min(
                _inboundByteBurst,
                _byteTokens + elapsedSeconds * _inboundBytesPerSecond);
        }
    }

    public void Dispose() { }
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
    private sealed class PendingSend
    {
        public PendingSend(byte[] buffer, int length, PooledSendCompletion? transportCompletion)
        {
            Buffer = buffer;
            Length = length;
            TransportCompletion = transportCompletion;
        }

        public byte[] Buffer { get; }
        public int Length { get; }
        public PooledSendCompletion? TransportCompletion { get; }
        private int _released;

        public bool TryMarkReleased() => Interlocked.Exchange(ref _released, 1) == 0;
    }

    private sealed class PooledSendCompletion : IValueTaskSource
    {
        private static readonly ConcurrentBag<PooledSendCompletion> Pool = new();

        private ManualResetValueTaskSourceCore<bool> _core;
        private CancellationTokenRegistration _cancellationRegistration;
        private CancellationToken _callerToken;
        private int _completionSignaled;
        private int _transportCompleted;
        private int _waiterConsumed;
        private int _returned;

        public static PooledSendCompletion Rent()
        {
            if (!Pool.TryTake(out var completion))
                completion = new PooledSendCompletion();

            completion._core.Reset();
            completion._core.RunContinuationsAsynchronously = true;
            completion._cancellationRegistration = default;
            completion._callerToken = default;
            completion._completionSignaled = 0;
            completion._transportCompleted = 0;
            completion._waiterConsumed = 0;
            completion._returned = 0;
            return completion;
        }

        public ValueTask WaitAsync(CancellationToken cancellationToken)
        {
            _callerToken = cancellationToken;
            if (cancellationToken.CanBeCanceled)
            {
                _cancellationRegistration = cancellationToken.UnsafeRegister(
                    static state => ((PooledSendCompletion)state!).CancelWaiter(),
                    this);
            }

            return new ValueTask(this, _core.Version);
        }

        public void SetResult()
        {
            if (Interlocked.CompareExchange(ref _completionSignaled, 1, 0) == 0)
                _core.SetResult(true);

            Volatile.Write(ref _transportCompleted, 1);
            TryReturn();
        }

        public void SetException(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completionSignaled, 1, 0) == 0)
                _core.SetException(exception);

            Volatile.Write(ref _transportCompleted, 1);
            TryReturn();
        }

        public void SetCanceled(CancellationToken cancellationToken) =>
            SetException(new OperationCanceledException(cancellationToken));

        public void ReturnUnused()
        {
            _cancellationRegistration.Dispose();
            _cancellationRegistration = default;
            Volatile.Write(ref _transportCompleted, 1);
            Volatile.Write(ref _waiterConsumed, 1);
            TryReturn();
        }

        void IValueTaskSource.GetResult(short token)
        {
            try
            {
                _core.GetResult(token);
            }
            finally
            {
                _cancellationRegistration.Dispose();
                _cancellationRegistration = default;
                Volatile.Write(ref _waiterConsumed, 1);
                TryReturn();
            }
        }

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _core.GetStatus(token);

        void IValueTaskSource.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);

        private void CancelWaiter()
        {
            if (Interlocked.CompareExchange(ref _completionSignaled, 1, 0) == 0)
                _core.SetException(new OperationCanceledException(_callerToken));
        }

        private void TryReturn()
        {
            if (Volatile.Read(ref _transportCompleted) == 0 ||
                Volatile.Read(ref _waiterConsumed) == 0 ||
                Interlocked.Exchange(ref _returned, 1) != 0)
            {
                return;
            }

            _callerToken = default;
            Pool.Add(this);
        }
    }

    private readonly IBoltConnection _transport;
    private readonly Channel<PendingSend> _sendChannel;
    private readonly TimeSpan _sendEnqueueTimeout;
    private readonly long _sendQueueByteCapacity;
    private readonly object _pendingByteCapacitySync = new();
    private TaskCompletionSource? _pendingByteCapacityChanged;
    private int _isClosing;
    private Exception? _sendFailure;
    public Task? SendLoop { get; private set; }

    public string StreamId { get; } = Guid.NewGuid().ToString("N");
    internal string QuotaKey { get; set; }
    internal BoltPrincipalRateLimiter? RateLimiter { get; set; }
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public ClaimsPrincipal? User { get; set; }
    public int ServiceHash { get; set; }
    public bool IsRegistered { get; set; }
    public bool IsAlive => !IsClosing && _transport.IsConnected;
    internal bool IsClosing => Volatile.Read(ref _isClosing) != 0;
    public BoltTransport TransportType => _transport.TransportType;
    public Exception? SendFailure => Volatile.Read(ref _sendFailure);

    public long SendEnqueueTimeoutCount => Interlocked.Read(ref _sendEnqueueTimeoutCount);
    private long _sendEnqueueTimeoutCount;

    public long TransportSendTimeoutCount => Interlocked.Read(ref _transportSendTimeoutCount);
    private long _transportSendTimeoutCount;

    public long TransportSendFailureCount => Interlocked.Read(ref _transportSendFailureCount);
    private long _transportSendFailureCount;

    /// <summary>Pending bytes queued for this connection. Used for backpressure decisions.</summary>
    public long PendingBytes
    {
        get
        {
            lock (_pendingByteCapacitySync)
                return _pendingBytes;
        }
    }
    private long _pendingBytes;

    /// <summary>Backpressure threshold: drop media frames when pending exceeds this (1MB).</summary>
    public const long BackpressureDropThreshold = 1024 * 1024;

    /// <summary>Backpressure threshold: send feedback signal to reduce sender rate (2MB).</summary>
    public const long BackpressureFeedbackThreshold = 2 * 1024 * 1024;

    /// <summary>True if this connection is under backpressure (pending > drop threshold).</summary>
    public bool IsUnderPressure => PendingBytes > BackpressureDropThreshold;

    public BoltHubConnection(
        IBoltConnection transport,
        int sendQueueCapacity = 4096,
        int sendEnqueueTimeoutMs = 0,
        long sendQueueByteCapacity = 16L * 1024 * 1024)
    {
        _transport = transport;
        QuotaKey = StreamId;
        _sendQueueByteCapacity = Math.Max(1, sendQueueByteCapacity);
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

    /// <summary>Start the background send loop. Call once after construction.</summary>
    public void StartSendLoop(CancellationToken ct, Action<Exception>? onFailure = null)
    {
        if (SendLoop is not null)
            throw new InvalidOperationException("The Bolt send loop has already been started.");

        SendLoop = Task.Run(async () =>
        {
            Exception? terminalFailure = null;
            var sendDeadlineCts = _sendEnqueueTimeout > TimeSpan.Zero
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;
            try
            {
                await foreach (var pending in _sendChannel.Reader.ReadAllAsync(ct))
                {
                    Task? transportSend = null;
                    PendingSend[]? batchItems = null;
                    var batchCount = 1;
                    byte[]? batchBuffer = null;
                    var sendBuffer = pending.Buffer;
                    var sendLength = pending.Length;
                    try
                    {
                        if (!_transport.IsConnected)
                            throw CreateTransportFailure("Bolt transport disconnected before a queued send completed.");

                        var encodedBatchSize = BoltCodec.BatchHeaderSize + 4 + pending.Length;
                        if (BoltCodec.IsValidBatchInnerFrame(pending.Buffer.AsSpan(0, pending.Length)) &&
                            _sendChannel.Reader.TryPeek(out var next) &&
                            BoltCodec.IsValidBatchInnerFrame(next.Buffer.AsSpan(0, next.Length)) &&
                            encodedBatchSize <= BoltCodec.MaxBatchBytes - 4 - next.Length)
                        {
                            batchItems = ArrayPool<PendingSend>.Shared.Rent(BoltCodec.MaxBatchFrames);
                            batchItems[0] = pending;
                            while (batchCount < BoltCodec.MaxBatchFrames &&
                                   _sendChannel.Reader.TryPeek(out next) &&
                                   BoltCodec.IsValidBatchInnerFrame(next.Buffer.AsSpan(0, next.Length)) &&
                                   encodedBatchSize <= BoltCodec.MaxBatchBytes - 4 - next.Length &&
                                   _sendChannel.Reader.TryRead(out next))
                            {
                                batchItems[batchCount++] = next;
                                encodedBatchSize += 4 + next.Length;
                            }

                            var batchWriter = RentedBufferWriter.GetThreadLocal();
                            BoltCodec.WriteBatch(
                                batchWriter,
                                batchItems.AsSpan(0, batchCount),
                                static item => item.Buffer.AsMemory(0, item.Length));
                            (batchBuffer, sendLength) = batchWriter.Detach();
                            sendBuffer = batchBuffer;
                        }

                        var sendToken = ct;
                        if (sendDeadlineCts is not null)
                        {
                            sendDeadlineCts.CancelAfter(_sendEnqueueTimeout);
                            sendToken = sendDeadlineCts.Token;
                        }

                        var sendOperation = _transport.SendAsync(sendBuffer.AsMemory(0, sendLength), sendToken);
                        if (sendOperation.IsCompletedSuccessfully)
                        {
                            sendOperation.GetAwaiter().GetResult();
                        }
                        else
                        {
                            transportSend = sendOperation.AsTask();
                            await transportSend.WaitAsync(sendToken);
                        }
                        CompleteBatch(batchItems, batchCount, pending, static completion => completion.SetResult());
                    }
                    catch (OperationCanceledException ex) when (
                        sendDeadlineCts?.IsCancellationRequested == true &&
                        !ct.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref _transportSendTimeoutCount);
                        var timeout = new BoltTransportSendTimeoutException(
                            $"Bolt transport send timed out after {_sendEnqueueTimeout.TotalMilliseconds:0} ms.",
                            ex);
                        CompleteBatch(batchItems, batchCount, pending, completion => completion.SetException(timeout));
                        throw timeout;
                    }
                    catch (OperationCanceledException)
                    {
                        CompleteBatch(batchItems, batchCount, pending, completion => completion.SetCanceled(ct));
                        throw;
                    }
                    catch (BoltTransportSendException ex)
                    {
                        CompleteBatch(batchItems, batchCount, pending, completion => completion.SetException(ex));
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var failure = CreateTransportFailure("Bolt transport send failed.", ex);
                        CompleteBatch(batchItems, batchCount, pending, completion => completion.SetException(failure));
                        throw failure;
                    }
                    finally
                    {
                        if (sendDeadlineCts is not null &&
                            !sendDeadlineCts.TryReset() &&
                            !ct.IsCancellationRequested)
                        {
                            sendDeadlineCts.Dispose();
                            sendDeadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        }
                        if (batchItems is not null)
                        {
                            for (var i = 0; i < batchCount; i++)
                                ReleasePendingSend(batchItems[i]);
                            ArrayPool<PendingSend>.Shared.Return(batchItems, clearArray: true);

                            if (batchBuffer is not null)
                            {
                                if (transportSend is { IsCompleted: false })
                                    _ = ReleaseBatchBufferWhenTransportCompletesAsync(transportSend, batchBuffer);
                                else
                                    ArrayPool<byte>.Shared.Return(batchBuffer);
                            }
                        }
                        else if (transportSend is { IsCompleted: false })
                        {
                            _ = ReleaseWhenTransportCompletesAsync(transportSend, pending);
                        }
                        else
                        {
                            ReleasePendingSend(pending);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                terminalFailure = ex;
                BoltServerMetrics.RecordTransportSendFailure(
                    ex is BoltTransportSendTimeoutException ? "timeout" : "failure");
                Volatile.Write(ref _sendFailure, ex);
                BeginClose();
                _sendChannel.Writer.TryComplete(ex);
                try { onFailure?.Invoke(ex); }
                catch { }
                throw;
            }
            finally
            {
                sendDeadlineCts?.Dispose();
                while (_sendChannel.Reader.TryRead(out var pending))
                {
                    if (terminalFailure is not null)
                        pending.TransportCompletion?.SetException(terminalFailure);
                    else
                        pending.TransportCompletion?.SetCanceled(ct);
                    ReleasePendingSend(pending);
                }
            }
        });
    }

    private static void CompleteBatch(
        PendingSend[]? batchItems,
        int batchCount,
        PendingSend single,
        Action<PooledSendCompletion> complete)
    {
        if (batchItems is null)
        {
            if (single.TransportCompletion is { } completion)
                complete(completion);
            return;
        }

        for (var i = 0; i < batchCount; i++)
        {
            if (batchItems[i].TransportCompletion is { } completion)
                complete(completion);
        }
    }

    private static async Task ReleaseBatchBufferWhenTransportCompletesAsync(Task transportSend, byte[] buffer)
    {
        try { await transportSend; }
        catch { }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        ThrowIfUnavailable();
        return EnqueueAsync(data, transportCompletion: null, ct);
    }

    private async ValueTask SendReliableAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        ThrowIfUnavailable();
        var completion = PooledSendCompletion.Rent();
        var enqueued = false;
        try
        {
            await EnqueueAsync(data, completion, ct);
            enqueued = true;
            await completion.WaitAsync(ct);
        }
        finally
        {
            if (!enqueued)
                completion.ReturnUnused();
        }
    }

    /// <summary>
    /// Detaches a writer's pooled buffer into the send queue without copying it.
    /// The connection owns the detached buffer on every completion and failure path.
    /// </summary>
    public ValueTask SendAsync(RentedBufferWriter writer, CancellationToken ct)
    {
        ThrowIfUnavailable();
        ct.ThrowIfCancellationRequested();
        var (buffer, length) = writer.Detach();
        try
        {
            return EnqueueOwnedAsync(buffer, length, transportCompletion: null, ct);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    private ValueTask EnqueueAsync(
        ReadOnlyMemory<byte> data,
        PooledSendCompletion? transportCompletion,
        CancellationToken ct)
    {
        // Snapshot into a pooled buffer — the caller's buffer (thread-local RentedBufferWriter
        // or pooled receive buffer) may be reused before the async transport write completes.
        ct.ThrowIfCancellationRequested();
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

        try
        {
            return EnqueueOwnedAsync(buf, len, transportCompletion, ct);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buf);
            throw;
        }
    }

    private ValueTask EnqueueOwnedAsync(
        byte[] buffer,
        int length,
        PooledSendCompletion? transportCompletion,
        CancellationToken ct)
    {
        if ((uint)length > (uint)buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        ct.ThrowIfCancellationRequested();
        if (length > _sendQueueByteCapacity)
        {
            throw new BoltSendQueueByteCapacityExceededException(
                $"Bolt send queue byte capacity {_sendQueueByteCapacity} is smaller than the {length}-byte frame.");
        }

        if (!TryReservePendingBytes(length))
            return WaitForByteCapacityAndEnqueueAsync(buffer, length, transportCompletion, ct);

        return EnqueueReservedAsync(buffer, length, transportCompletion, ct);
    }

    private ValueTask EnqueueReservedAsync(
        byte[] buffer,
        int length,
        PooledSendCompletion? transportCompletion,
        CancellationToken ct)
    {
        var pending = new PendingSend(buffer, length, transportCompletion);

        // All sends go through Channel (serialized single-writer)
        if (_sendChannel.Writer.TryWrite(pending))
            return ValueTask.CompletedTask;
        return SendSlowAsync(pending, ct);
    }

    private async ValueTask WaitForByteCapacityAndEnqueueAsync(
        byte[] buffer,
        int length,
        PooledSendCompletion? transportCompletion,
        CancellationToken ct)
    {
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;
        var enqueueToken = ct;
        PendingSend? pending = null;
        var reserved = false;
        try
        {
            if (_sendEnqueueTimeout > TimeSpan.Zero)
            {
                timeoutCts = new CancellationTokenSource(_sendEnqueueTimeout);
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                enqueueToken = linkedCts.Token;
            }

            await ReservePendingBytesAsync(length, enqueueToken);
            reserved = true;

            pending = new PendingSend(buffer, length, transportCompletion);
            if (!_sendChannel.Writer.TryWrite(pending))
                await _sendChannel.Writer.WriteAsync(pending, enqueueToken);
        }
        catch (OperationCanceledException ex) when (timeoutCts?.IsCancellationRequested == true && !ct.IsCancellationRequested)
        {
            Interlocked.Increment(ref _sendEnqueueTimeoutCount);
            ReleaseUnqueuedBuffer(buffer, length, reserved, pending);
            throw new BoltSendEnqueueTimeoutException(
                $"Bolt send queue enqueue timed out after {_sendEnqueueTimeout.TotalMilliseconds:0} ms.",
                ex);
        }
        catch
        {
            ReleaseUnqueuedBuffer(buffer, length, reserved, pending);
            throw;
        }
        finally
        {
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
        }
    }

    private void ReleaseUnqueuedBuffer(byte[] buffer, int length, bool reserved, PendingSend? pending)
    {
        if (pending is not null)
        {
            ReleasePendingSend(pending);
            return;
        }

        ArrayPool<byte>.Shared.Return(buffer);
        if (reserved)
            ReleasePendingBytes(length);
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
        catch (OperationCanceledException ex) when (timeoutCts?.IsCancellationRequested == true && !ct.IsCancellationRequested)
        {
            Interlocked.Increment(ref _sendEnqueueTimeoutCount);
            ReleasePendingSend(pending);
            throw new BoltSendEnqueueTimeoutException(
                $"Bolt send queue enqueue timed out after {_sendEnqueueTimeout.TotalMilliseconds:0} ms.",
                ex);
        }
        catch
        {
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
    public void CompleteSendChannel() => _sendChannel.Writer.TryComplete();

    internal void BeginClose() => Interlocked.Exchange(ref _isClosing, 1);

    internal async ValueTask SendAndCloseAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await SendReliableAsync(data, ct);
        BeginClose();
        CompleteSendChannel();
        if (SendLoop is not null)
            await SendLoop.WaitAsync(ct);
        await _transport.CloseAsync(ct);
    }

    public ValueTask CloseAsync(CancellationToken ct = default) => _transport.CloseAsync(ct);

    private BoltTransportSendException CreateTransportFailure(string message, Exception? innerException = null)
    {
        Interlocked.Increment(ref _transportSendFailureCount);
        return new BoltTransportSendException(message, innerException);
    }

    private void ThrowIfUnavailable()
    {
        if (SendFailure is { } failure)
            throw new BoltTransportSendException("The Bolt connection was retired after a transport send failure.", failure);
        if (IsClosing)
            throw new InvalidOperationException("The Bolt connection is closing and cannot accept sends.");
    }

    private void ReleasePendingSend(PendingSend pending)
    {
        if (!pending.TryMarkReleased())
            return;

        ArrayPool<byte>.Shared.Return(pending.Buffer);
        ReleasePendingBytes(pending.Length);
    }

    private bool TryReservePendingBytes(int length)
    {
        if (length > _sendQueueByteCapacity)
            return false;

        lock (_pendingByteCapacitySync)
        {
            if (_pendingBytes > _sendQueueByteCapacity - length)
                return false;

            _pendingBytes += length;
            return true;
        }
    }

    private async ValueTask ReservePendingBytesAsync(int length, CancellationToken ct)
    {
        while (true)
        {
            Task capacityChanged;
            lock (_pendingByteCapacitySync)
            {
                if (_pendingBytes <= _sendQueueByteCapacity - length)
                {
                    _pendingBytes += length;
                    return;
                }

                capacityChanged = (_pendingByteCapacityChanged ??= CreateCapacitySignal()).Task;
            }

            await capacityChanged.WaitAsync(ct);
        }
    }

    private void ReleasePendingBytes(int length)
    {
        TaskCompletionSource? capacityChanged;
        lock (_pendingByteCapacitySync)
        {
            _pendingBytes -= length;
            capacityChanged = _pendingByteCapacityChanged;
            _pendingByteCapacityChanged = null;
        }

        capacityChanged?.TrySetResult();
    }

    private static TaskCompletionSource CreateCapacitySignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task ReleaseWhenTransportCompletesAsync(Task transportSend, PendingSend pending)
    {
        try { await transportSend; }
        catch { }
        finally { ReleasePendingSend(pending); }
    }
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
    private int _status;

    public object SyncRoot { get; } = new();
    public Guid CallId { get; init; }
    public ServerCallStatus Status
    {
        get => (ServerCallStatus)Volatile.Read(ref _status);
        set => Volatile.Write(ref _status, (int)value);
    }
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
