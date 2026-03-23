using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Shared.Buffers;
using StreamFlow.Domain.Shared.Protocol;

namespace XFramework.Integration.ThinProtocol;

/// <summary>
/// Direct QUIC RPC — client connects directly to a service (no hub).
/// Each RPC uses a new bidirectional QUIC stream (native multiplexing).
/// Best for server-to-server where both endpoints are known.
/// </summary>
public sealed class QuicDirectServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _handlers = new();
    private readonly ILogger _logger;
    private QuicListener? _listener;
    private CancellationTokenSource? _cts;

    public QuicDirectServer(ILogger logger) => _logger = logger;

    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        _handlers[StreamFlowCodec.Fnv1aHash(commandName)] = handler;
    }

    public async Task StartAsync(IPEndPoint endpoint, CancellationToken ct = default)
    {
        var cert = GenerateCert();
        _listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = endpoint,
            ApplicationProtocols = [new SslApplicationProtocol("sf-direct")],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                MaxInboundBidirectionalStreams = 256,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = cert,
                    ApplicationProtocols = [new SslApplicationProtocol("sf-direct")]
                }
            })
        }, ct);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _logger.LogInformation("QUIC direct server listening on {Endpoint}", endpoint);

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var conn = await _listener.AcceptConnectionAsync(_cts.Token);
                    _ = HandleConnectionAsync(conn, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Accept error"); }
            }
        }, ct);
    }

    private async Task HandleConnectionAsync(QuicConnection conn, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var stream = await conn.AcceptInboundStreamAsync(ct);
                _ = HandleStreamAsync(stream, ct);
            }
        }
        catch (QuicException) { }
        catch (OperationCanceledException) { }
        finally
        {
            await conn.DisposeAsync();
        }
    }

    private async Task HandleStreamAsync(QuicStream stream, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read > 0 && StreamFlowCodec.TryReadRequest(buffer.AsSpan(0, read), out var frame, out _))
            {
                if (_handlers.TryGetValue(frame.CommandHash, out var handler))
                {
                    var requestPayload = frame.GetPayload(buffer.AsMemory(0, read));
                    var (status, respData) = await handler(requestPayload, frame.RequestId);
                    var writer = RentedBufferWriter.GetThreadLocal();
                    StreamFlowCodec.WriteResponse(writer, frame.RequestId, status, respData.Span);
                    await stream.WriteAsync(writer.WrittenMemory, ct);
                }
                else
                {
                    var writer = new ArrayBufferWriter<byte>(StreamFlowCodec.ResponseHeaderSize);
                    StreamFlowCodec.WriteResponse(writer, frame.RequestId, HttpStatusCode.NotImplemented, []);
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

    private static X509Certificate2 GenerateCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_listener is not null) await _listener.DisposeAsync();
    }
}

/// <summary>
/// Direct QUIC RPC client — connects directly to a service endpoint.
/// Each RPC opens a new bidirectional stream (QUIC native multiplexing).
/// </summary>
public sealed class QuicDirectClient : IAsyncDisposable
{
    private readonly IPEndPoint _endpoint;
    private QuicConnection? _connection;

    public QuicDirectClient(IPEndPoint endpoint) => _endpoint = endpoint;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _connection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
        {
            RemoteEndPoint = _endpoint,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            MaxInboundBidirectionalStreams = 256,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = [new SslApplicationProtocol("sf-direct")],
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        }, ct);
    }

    /// <summary>
    /// Invoke an RPC. Opens a new QUIC stream per call (lightweight, native multiplexing).
    /// </summary>
    public async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Data)> InvokeAsync(
        string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var requestId = Guid.NewGuid();
        var commandHash = StreamFlowCodec.Fnv1aHash(commandName);

        var stream = await _connection!.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
        try
        {
            // Write request
            var writer = RentedBufferWriter.GetThreadLocal();
            StreamFlowCodec.WriteRequest(writer, requestId, 0, commandHash, payload.Span);
            await stream.WriteAsync(writer.WrittenMemory, ct);
            stream.CompleteWrites(); // Signal end of request

            // Read response
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read > 0 && StreamFlowCodec.TryReadResponse(buffer.AsSpan(0, read), out var frame, out _))
                {
                    var respBytes = frame.PayloadLength > 0
                        ? frame.GetPayload(buffer.AsSpan(0, read)).ToArray()
                        : Array.Empty<byte>();
                    return (frame.StatusCode, (ReadOnlyMemory<byte>)respBytes);
                }

                return (HttpStatusCode.InternalServerError, ReadOnlyMemory<byte>.Empty);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            try { await _connection.CloseAsync(0); } catch { }
            await _connection.DisposeAsync();
        }
    }
}
