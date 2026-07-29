using Bolt.Client;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.WebSockets;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Microsoft.Extensions.Logging;

namespace Bolt.Media;

/// <summary>
/// Manages P2P direct connection upgrades for media calls.
///
/// Strategy:
/// 1. Call starts hub-routed (instant, always works)
/// 2. After call is active, both clients exchange endpoints via DirectOffer/DirectAnswer
/// 3. Both clients attempt direct WebSocket connection simultaneously
/// 4. If successful → migrate media frames to direct path (signaling stays on hub)
/// 5. If fails → hub continues routing seamlessly (user never notices)
/// 6. If direct connection drops mid-call → automatic fallback to hub
///
/// The direct connection uses the same Bolt binary protocol — media frames are
/// sent directly between peers without the hub in the path.
/// </summary>
public sealed class DirectConnectionManager : IAsyncDisposable
{
    private readonly Guid _callId;
    private readonly string _localClientId;
    private readonly BoltConnection _hubConnection;
    private readonly ILogger? _logger;
    private IPEndPoint? _remoteEndpoint;
    private BoltConnection? _directConnection;
    private volatile bool _isDirectActive;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _healthCheckTask;

    private const int ConnectTimeoutMs = 5000;
    private const int HealthCheckIntervalMs = 3000;
    private const int MaxConsecutiveFailures = 3;

    /// <summary>True if a direct P2P connection is active.</summary>
    public bool IsDirectActive => _isDirectActive;

    /// <summary>The active connection — direct if available, hub fallback otherwise.</summary>
    public BoltConnection ActiveConnection => _isDirectActive && _directConnection != null
        ? _directConnection
        : _hubConnection;

    /// <summary>Fired when direct connection is established or falls back to hub.</summary>
    public event Action<bool>? OnConnectionModeChanged;

    /// <summary>Fired when a media frame arrives on the direct connection (bypass hub receive loop).</summary>
    public event Action<byte[], int>? OnDirectFrameReceived;

    public DirectConnectionManager(Guid callId, string localClientId, BoltConnection hubConnection, ILogger? logger = null)
    {
        _callId = callId;
        _localClientId = localClientId;
        _hubConnection = hubConnection;
        _logger = logger;
    }

    /// <summary>
    /// Handle a DirectOffer signal containing the remote peer's WebSocket endpoint.
    /// Payload: [4:ipv4][2:port] or [16:ipv6][2:port]
    /// </summary>
    public void HandleDirectOffer(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6) return;

        IPAddress ip;
        int portOffset;
        if (payload.Length >= 18) // IPv6 + port
        {
            ip = new IPAddress(payload[..16]);
            portOffset = 16;
        }
        else // IPv4 + port
        {
            ip = new IPAddress(payload[..4]);
            portOffset = 4;
        }

        var port = BinaryPrimitives.ReadUInt16LittleEndian(payload[portOffset..]);
        _remoteEndpoint = new IPEndPoint(ip, port);

        _cts = new CancellationTokenSource();
        _ = AttemptDirectConnectionAsync(_cts.Token);
    }

    /// <summary>
    /// Create a DirectOffer payload with local endpoint information.
    /// </summary>
    public static byte[] CreateDirectOfferPayload(IPEndPoint localEndpoint)
    {
        var addrBytes = localEndpoint.Address.GetAddressBytes();
        var payload = new byte[addrBytes.Length + 2];
        addrBytes.CopyTo(payload, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(addrBytes.Length), (ushort)localEndpoint.Port);
        return payload;
    }

    private async Task AttemptDirectConnectionAsync(CancellationToken ct)
    {
        if (_remoteEndpoint == null) return;

        _logger?.LogInformation("Attempting direct P2P connection to {Endpoint} for call {CallId}",
            _remoteEndpoint, _callId);

        try
        {
            // Try direct WebSocket connection
            var ws = new ClientWebSocket();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(ConnectTimeoutMs);

            var wsUri = new Uri($"ws://{_remoteEndpoint}/bolt-direct");
            await ws.ConnectAsync(wsUri, connectCts.Token);

            // Handshake: send our client ID + call ID for verification
            var handshake = new byte[1 + 16 + 4 + _localClientId.Length * 2];
            handshake[0] = 0xFF; // Direct handshake marker
            _callId.TryWriteBytes(handshake.AsSpan(1));
            BinaryPrimitives.WriteInt32LittleEndian(handshake.AsSpan(17), _localClientId.Length);
            for (int i = 0; i < _localClientId.Length; i++)
                BinaryPrimitives.WriteUInt16LittleEndian(handshake.AsSpan(21 + i * 2), _localClientId[i]);

            await ws.SendAsync(handshake, WebSocketMessageType.Binary, true, ct);

            // Wait for handshake ack
            var ackBuffer = new byte[2];
            var result = await ws.ReceiveAsync(ackBuffer, connectCts.Token);
            if (result.Count < 1 || ackBuffer[0] != 0xFF)
            {
                ws.Dispose();
                _logger?.LogWarning("Direct handshake rejected for call {CallId}", _callId);
                return;
            }

            // Handshake successful — activate direct connection
            var directConn = new BoltConnection(ws);
            _directConnection = directConn;
            _isDirectActive = true;

            _logger?.LogInformation("Direct P2P connection established for call {CallId}", _callId);
            OnConnectionModeChanged?.Invoke(true);

            // Send DirectAnswer confirmation via hub so both sides know
            using var writer = new RentedBufferWriter(64);
            BoltCodec.WriteCallSignal(writer, _callId, SignalType.DirectAnswer, ReadOnlySpan<byte>.Empty);
            await _hubConnection.SendAsync(writer.WrittenMemory, ct);

            // Start receive loop on direct connection
            _receiveTask = Task.Run(() => DirectReceiveLoopAsync(directConn, ct), ct);

            // Start health check
            _healthCheckTask = Task.Run(() => HealthCheckLoopAsync(ct), ct);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Direct connection attempt cancelled for call {CallId}", _callId);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Direct connection failed for call {CallId}: {Error}. Continuing via hub.",
                _callId, ex.Message);
            // Silent fallback — hub routing continues, user doesn't notice
        }
    }

    private async Task DirectReceiveLoopAsync(BoltConnection conn, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
        try
        {
            while (!ct.IsCancellationRequested && conn.WebSocket.State == WebSocketState.Open)
            {
                var result = await conn.WebSocket.ReceiveAsync(buffer.AsMemory(), ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.Count == 0) continue;

                // Forward to the handler (BoltClient will process media frames)
                var copy = new byte[result.Count];
                Buffer.BlockCopy(buffer, 0, copy, 0, result.Count);
                OnDirectFrameReceived?.Invoke(copy, result.Count);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException)
        {
            // Direct connection dropped — fallback to hub
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (_isDirectActive)
            {
                FallbackToHub();
            }
        }
    }

    private async Task HealthCheckLoopAsync(CancellationToken ct)
    {
        var consecutiveFailures = 0;

        while (!ct.IsCancellationRequested && _isDirectActive)
        {
            try
            {
                await Task.Delay(HealthCheckIntervalMs, ct);

                if (_directConnection?.WebSocket.State != WebSocketState.Open)
                {
                    consecutiveFailures++;
                }
                else
                {
                    consecutiveFailures = 0;
                }

                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger?.LogWarning("Direct connection unhealthy ({Failures} failures), falling back to hub", consecutiveFailures);
                    FallbackToHub();
                    break;
                }
            }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Fall back to hub routing (e.g., if direct connection drops).
    /// Seamless — media frames switch back to hub path.
    /// </summary>
    public void FallbackToHub()
    {
        if (!_isDirectActive && _directConnection == null) return;

        var oldConn = _directConnection;
        _directConnection = null;
        _isDirectActive = false;

        if (oldConn?.WebSocket.State == WebSocketState.Open)
        {
            try { oldConn.WebSocket.Abort(); } catch { }
        }
        oldConn?.WebSocket.Dispose();

        _logger?.LogInformation("Fell back to hub routing for call {CallId}", _callId);
        OnConnectionModeChanged?.Invoke(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            if (_receiveTask != null) try { await _receiveTask; } catch { }
            if (_healthCheckTask != null) try { await _healthCheckTask; } catch { }
            _cts.Dispose();
        }

        if (_directConnection != null)
        {
            try
            {
                if (_directConnection.WebSocket.State == WebSocketState.Open)
                    await _directConnection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
            _directConnection.WebSocket.Dispose();
        }
    }
}
