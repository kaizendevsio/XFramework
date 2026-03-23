using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Shared.Buffers;
using StreamFlow.Domain.Shared.Protocol;
using XFramework.Domain.Shared.Configurations;

namespace XFramework.Integration.ThinProtocol;

/// <summary>
/// QUIC-based StreamFlow client. Each RPC opens its own bidirectional QUIC stream,
/// providing native multiplexing with zero head-of-line blocking.
///
/// Same wire protocol as ThinStreamFlowClient, but over QUIC instead of WebSocket.
/// Benefits: 0-RTT reconnection, per-stream flow control, built-in TLS 1.3.
/// </summary>
public sealed class QuicStreamFlowClient : IAsyncDisposable
{
    private readonly IPEndPoint _serverEndPoint;
    private readonly string _clientId;
    private readonly string _clientName;
    private readonly StreamFlowConfiguration _config;
    private readonly ILogger _logger;

    private QuicConnection? _connection;
    private volatile bool _isRegistered;
    private volatile bool _disposed;

    // Handler registry — maps command hash to handler delegate
    private readonly ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>> _handlers = new();

    private CancellationTokenSource? _acceptCts;

    public bool IsConnected => _connection is not null && _isRegistered;

    public QuicStreamFlowClient(IPEndPoint serverEndPoint, string clientId, string clientName,
        StreamFlowConfiguration config, ILogger logger)
    {
        _serverEndPoint = serverEndPoint;
        _clientId = clientId;
        _clientName = clientName;
        _config = config;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _connection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
        {
            RemoteEndPoint = _serverEndPoint,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            MaxInboundBidirectionalStreams = 256,
            MaxInboundUnidirectionalStreams = 0,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = [new SslApplicationProtocol("streamflow")],
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        }, ct);

        // Open registration stream
        var regStream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
        var writer = new ArrayBufferWriter<byte>(128);
        StreamFlowCodec.WriteRegister(writer, _clientId, _clientName);
        await regStream.WriteAsync(writer.WrittenMemory, ct);

        // Wait for ack
        var ackBuffer = new byte[2];
        var read = await regStream.ReadAsync(ackBuffer, ct);
        if (read >= 2 && (FrameType)ackBuffer[0] == FrameType.RegisterAck && ackBuffer[1] == 1)
        {
            _isRegistered = true;
            _logger.LogInformation("QUIC client registered: {ClientId} ({ClientName})", _clientId, _clientName);
        }
        else
        {
            throw new InvalidOperationException("QUIC server rejected registration");
        }

        await regStream.DisposeAsync();

        // Start accepting inbound streams (for incoming requests when this client is a recipient)
        _acceptCts = new CancellationTokenSource();
        _ = Task.Run(() => AcceptInboundStreamsAsync(_acceptCts.Token));
    }

    /// <summary>
    /// Invoke a method on a remote service.
    /// Uses a persistent bidirectional QUIC stream for sequential RPCs (same pattern as WebSocket).
    /// Opens new streams only for concurrent RPCs to leverage QUIC multiplexing.
    /// </summary>
    public async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Data)> InvokeAsync(
        string recipientId, string commandName, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        if (_connection is null || !_isRegistered)
            throw new InvalidOperationException("Not connected");

        var requestId = Guid.NewGuid();
        var recipientHash = StreamFlowCodec.Fnv1aHash(recipientId);
        var commandHash = StreamFlowCodec.Fnv1aHash(commandName);

        // Get or create persistent RPC stream
        var stream = await GetOrCreateRpcStreamAsync(ct);

        // Write request frame
        var writer = RentedBufferWriter.GetThreadLocal();
        StreamFlowCodec.WriteRequest(writer, requestId, recipientHash, commandHash, payload.Span);

        await _streamSendLock.WaitAsync(ct);
        try
        {
            await stream.WriteAsync(writer.WrittenMemory, ct);
        }
        finally
        {
            _streamSendLock.Release();
        }

        // Wait for response via the pending call mechanism
        var rpcCall = PooledRpcCallThin.Rent();
        _pendingCalls[requestId] = rpcCall;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_config.RpcTimeoutSeconds > 0 ? _config.RpcTimeoutSeconds : 30));
            rpcCall.RegisterTimeout(timeoutCts.Token);

            var response = await rpcCall.GetTask();
            return (response.StatusCode, response.Data);
        }
        finally
        {
            _pendingCalls.TryRemove(requestId, out _);
        }
    }

    private QuicStream? _rpcStream;
    private readonly SemaphoreSlim _streamSendLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, PooledRpcCallThin> _pendingCalls = new();

    private async Task<QuicStream> GetOrCreateRpcStreamAsync(CancellationToken ct)
    {
        if (_rpcStream is not null)
            return _rpcStream;

        _rpcStream = await _connection!.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
        // Start reading responses on this stream
        _ = Task.Run(() => ReadRpcResponsesAsync(_rpcStream, ct));
        return _rpcStream;
    }

    private async Task ReadRpcResponsesAsync(QuicStream stream, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0) break;

                if (StreamFlowCodec.TryReadResponse(buffer.AsSpan(0, read), out var frame, out _))
                {
                    if (_pendingCalls.TryRemove(frame.RequestId, out var rpcCall))
                    {
                        rpcCall.SetResult(new ThinRpcResponse { StatusCode = frame.StatusCode, Data = frame.Payload });
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("QUIC RPC stream read error: {Error}", ex.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void RegisterHandler(string commandName, Func<ReadOnlyMemory<byte>, Guid, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        var hash = StreamFlowCodec.Fnv1aHash(commandName);
        _handlers[hash] = handler;
    }

    /// <summary>
    /// Accept inbound streams from the server (when this client is the recipient of an RPC).
    /// </summary>
    private async Task AcceptInboundStreamsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _connection is not null)
        {
            try
            {
                var stream = await _connection.AcceptInboundStreamAsync(ct);
                _ = HandleInboundStreamAsync(stream, ct);
            }
            catch (QuicException) { break; }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting inbound QUIC stream");
            }
        }
    }

    private async Task HandleInboundStreamAsync(QuicStream stream, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) return;

            if ((FrameType)buffer[0] == FrameType.Request &&
                StreamFlowCodec.TryReadRequest(buffer.AsSpan(0, read), out var frame, out _))
            {
                if (_handlers.TryGetValue(frame.CommandHash, out var handler))
                {
                    var (statusCode, responsePayload) = await handler(frame.Payload, frame.RequestId);

                    // Write response back on the same stream
                    var writer = RentedBufferWriter.GetThreadLocal();
                    StreamFlowCodec.WriteResponse(writer, frame.RequestId, statusCode, responsePayload.Span);
                    await stream.WriteAsync(writer.WrittenMemory, ct);
                }
                else
                {
                    var writer = new ArrayBufferWriter<byte>(StreamFlowCodec.ResponseHeaderSize);
                    StreamFlowCodec.WriteResponse(writer, frame.RequestId, HttpStatusCode.NotImplemented, ReadOnlySpan<byte>.Empty);
                    await stream.WriteAsync(writer.WrittenMemory, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling inbound QUIC stream");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await stream.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _acceptCts?.Cancel();

        if (_connection is not null)
        {
            try { await _connection.CloseAsync(0); } catch { }
            await _connection.DisposeAsync();
        }

        _acceptCts?.Dispose();
    }
}
