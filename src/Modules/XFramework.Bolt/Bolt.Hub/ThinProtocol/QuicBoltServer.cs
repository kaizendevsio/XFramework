using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bolt.Domain.Shared.Protocol;

namespace Bolt.Hub.ThinProtocol;

/// <summary>
/// QUIC-based Bolt server. Each RPC uses its own bidirectional QUIC stream,
/// eliminating head-of-line blocking between concurrent requests.
///
/// Same wire protocol as the thin WebSocket server (BoltHubCodec), different transport.
/// QUIC provides: 0-RTT reconnection, per-stream congestion control, built-in TLS 1.3.
/// </summary>
public sealed class QuicBoltServer : IAsyncDisposable
{
    private readonly ILogger<QuicBoltServer> _logger;
    private readonly ConcurrentDictionary<string, QuicBoltConnection> _connectionsByStreamId = new();
    private readonly ConcurrentDictionary<int, ConcurrentBag<QuicBoltConnection>> _connectionsByServiceHash = new();
    private readonly ConcurrentDictionary<Guid, (QuicBoltConnection Caller, QuicStream CallerStream, long Timestamp)> _pendingInvocations = new();
    private readonly ConcurrentDictionary<int, int> _roundRobinIndex = new();
    private readonly Timer _cleanupTimer;

    private QuicListener? _listener;
    private CancellationTokenSource? _cts;

    public QuicBoltServer(ILogger<QuicBoltServer> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupStaleInvocations, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Start listening for QUIC connections.
    /// </summary>
    public async Task StartAsync(IPEndPoint endpoint, X509Certificate2? certificate = null, CancellationToken ct = default)
    {
        certificate ??= GenerateSelfSignedCert();

        _listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = endpoint,
            ApplicationProtocols = [new SslApplicationProtocol("bolt")],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                MaxInboundBidirectionalStreams = 256,
                MaxInboundUnidirectionalStreams = 0,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    ApplicationProtocols = [new SslApplicationProtocol("bolt")]
                }
            })
        }, ct);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _logger.LogInformation("QUIC Bolt server listening on {Endpoint}", endpoint);

        // Accept connections loop
        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var connection = await _listener.AcceptConnectionAsync(_cts.Token);
                    _ = HandleConnectionAsync(connection, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting QUIC connection");
                }
            }
        }, ct);
    }

    private async Task HandleConnectionAsync(QuicConnection quicConnection, CancellationToken ct)
    {
        var connection = new QuicBoltConnection(quicConnection);
        _logger.LogDebug("New QUIC connection from {RemoteEndPoint}", quicConnection.RemoteEndPoint);

        try
        {
            // First stream is the registration stream
            var regStream = await quicConnection.AcceptInboundStreamAsync(ct);
            await HandleRegistrationAsync(connection, regStream, ct);

            // Accept subsequent streams (each is an RPC)
            while (!ct.IsCancellationRequested)
            {
                var stream = await quicConnection.AcceptInboundStreamAsync(ct);
                _ = HandleStreamAsync(connection, stream, ct);
            }
        }
        catch (QuicException ex) when (ex.QuicError == QuicError.ConnectionAborted)
        {
            _logger.LogDebug("QUIC connection closed: {ClientId}", connection.ClientId);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling QUIC connection for {ClientId}", connection.ClientId);
        }
        finally
        {
            RemoveConnection(connection);
            await quicConnection.DisposeAsync();
        }
    }

    private async Task HandleRegistrationAsync(QuicBoltConnection connection, QuicStream stream, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read > 0 && (FrameType)buffer[0] == FrameType.Register)
            {
                if (BoltHubCodec.TryReadRegister(buffer.AsSpan(0, read), out var clientId, out var clientName, out _))
                {
                    connection.ClientId = clientId;
                    connection.ClientName = clientName;
                    connection.ServiceHash = BoltHubCodec.Fnv1aHash(clientId);

                    _connectionsByStreamId[connection.Id] = connection;
                    _connectionsByServiceHash.AddOrUpdate(
                        connection.ServiceHash,
                        _ => new ConcurrentBag<QuicBoltConnection> { connection },
                        (_, bag) => { bag.Add(connection); return bag; });

                    _logger.LogInformation("QUIC client registered: {ClientId} ({ClientName})", clientId, clientName);

                    // Send ack
                    var writer = new ArrayBufferWriter<byte>(2);
                    BoltHubCodec.WriteRegisterAck(writer, true);
                    await stream.WriteAsync(writer.WrittenMemory, ct);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await stream.DisposeAsync();
        }
    }

    private async Task HandleStreamAsync(QuicBoltConnection connection, QuicStream stream, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            // Persistent stream — read multiple frames (same pattern as WebSocket)
            while (!ct.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0) break;

                var frameType = (FrameType)buffer[0];

                switch (frameType)
                {
                    case FrameType.Request:
                        await HandleRequestAsync(connection, stream, buffer, read, ct);
                        break;
                    case FrameType.Response:
                        HandleResponseLocally(buffer, read);
                        break;
                }
            }
        }
        catch (QuicException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling QUIC stream for {ClientId}", connection.ClientId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task HandleRequestAsync(QuicBoltConnection caller, QuicStream callerStream,
        byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltHubCodec.TryReadRequest(buffer.AsSpan(0, length), out var frame, out var consumed))
            return;

        // Track: we need to write the response back on the caller's persistent stream
        _pendingInvocations[frame.RequestId] = (caller, callerStream, Environment.TickCount64);

        var recipient = GetRecipient(frame.RecipientHash);
        if (recipient is null)
        {
            var errWriter = new ArrayBufferWriter<byte>(BoltHubCodec.ResponseHeaderSize);
            BoltHubCodec.WriteResponse(errWriter, frame.RequestId, HttpStatusCode.NotFound, ReadOnlySpan<byte>.Empty);
            await callerStream.WriteAsync(errWriter.WrittenMemory, ct);
            _pendingInvocations.TryRemove(frame.RequestId, out _);
            return;
        }

        // Forward to recipient's persistent RPC stream
        var recipientStream = GetOrCreateRecipientStream(recipient);
        if (recipientStream is not null)
        {
            await recipientStream.WriteAsync(buffer.AsMemory(0, consumed), ct);
        }
    }

    /// <summary>
    /// Response came back from a recipient — route to original caller's stream.
    /// </summary>
    private void HandleResponseLocally(byte[] buffer, int length)
    {
        if (!BoltHubCodec.TryReadResponse(buffer.AsSpan(0, length), out var frame, out var consumed))
            return;

        if (_pendingInvocations.TryRemove(frame.RequestId, out var pending))
        {
            // Write response back on caller's stream (fire and forget, async)
            _ = pending.CallerStream.WriteAsync(buffer.AsMemory(0, consumed));
        }
    }

    private readonly ConcurrentDictionary<string, QuicStream> _recipientRpcStreams = new();

    private QuicStream? GetOrCreateRecipientStream(QuicBoltConnection recipient)
    {
        return _recipientRpcStreams.GetOrAdd(recipient.Id, _ =>
        {
            try
            {
                return recipient.QuicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional).AsTask().Result;
            }
            catch
            {
                return null!;
            }
        });
    }

    private QuicBoltConnection? GetRecipient(int serviceHash)
    {
        if (!_connectionsByServiceHash.TryGetValue(serviceHash, out var bag))
            return null;

        var clients = bag.Where(c => c.IsAlive).ToList();
        if (clients.Count == 0) return null;
        if (clients.Count == 1) return clients[0];

        var idx = _roundRobinIndex.AddOrUpdate(serviceHash, 0, (_, prev) => prev + 1);
        return clients[(int)((uint)idx % clients.Count)];
    }

    private void RemoveConnection(QuicBoltConnection connection)
    {
        if (connection.ClientId is not null)
        {
            _connectionsByStreamId.TryRemove(connection.Id, out _);
            if (_connectionsByServiceHash.TryGetValue(connection.ServiceHash, out var bag))
            {
                var updated = new ConcurrentBag<QuicBoltConnection>(bag.Where(c => c.Id != connection.Id));
                if (updated.IsEmpty) _connectionsByServiceHash.TryRemove(connection.ServiceHash, out _);
                else _connectionsByServiceHash[connection.ServiceHash] = updated;
            }
            _logger.LogInformation("QUIC client disconnected: {ClientId}", connection.ClientId);
        }
    }

    private void CleanupStaleInvocations(object? state)
    {
        var now = Environment.TickCount64;
        foreach (var (requestId, pending) in _pendingInvocations)
        {
            if (now - pending.Timestamp > 30_000)
                _pendingInvocations.TryRemove(requestId, out _);
        }
    }

    public static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        req.CertificateExtensions.Add(sanBuilder.Build());
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        // Export and re-import to ensure private key is available in memory
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }

    public int ConnectedClients => _connectionsByStreamId.Count;

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_listener is not null) await _listener.DisposeAsync();
        _cleanupTimer.Dispose();
    }
}

public sealed class QuicBoltConnection
{
    public QuicConnection QuicConnection { get; }
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int ServiceHash { get; set; }
    public bool IsAlive => true; // QUIC doesn't expose connection state directly

    public QuicBoltConnection(QuicConnection connection) => QuicConnection = connection;
}
