using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.WebSockets;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging;

namespace Bolt.Client.Transport;

/// <summary>
/// Tries transports in priority order and returns the first working IBoltConnection.
/// QUIC -> WebTransport -> WebSocket. Each attempt has a configurable timeout.
/// </summary>
public sealed class BoltTransportNegotiator
{
    private readonly ILogger _logger;

    /// <summary>The transport that was used for the last successful connection.</summary>
    public BoltTransport? LastTransportUsed { get; private set; }

    public BoltTransportNegotiator(ILogger logger) => _logger = logger;

    public async Task<IBoltConnection> ConnectAsync(Uri serverUri, BoltClientOptions options, CancellationToken ct)
    {
        foreach (var transport in options.PreferredTransports)
        {
            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(options.TransportAttemptTimeoutMs);

                IBoltConnection? conn = transport switch
                {
                    BoltTransport.Quic => await TryQuicAsync(serverUri, attemptCts.Token),
                    BoltTransport.WebTransport => await TryWebTransportAsync(serverUri, attemptCts.Token),
                    BoltTransport.WebSocket => await TryWebSocketAsync(serverUri, attemptCts.Token),
                    _ => null
                };

                if (conn is not null)
                {
                    LastTransportUsed = transport;
                    _logger.LogInformation("Bolt connected via {Transport} to {Uri}", transport, serverUri);
                    return conn;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("Transport {Transport} timed out, trying next", transport);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Transport {Transport} failed, trying next", transport);
            }
        }

        throw new InvalidOperationException(
            $"All transports failed for {serverUri}. Tried: {string.Join(", ", options.PreferredTransports)}");
    }

    private static async Task<IBoltConnection?> TryQuicAsync(Uri serverUri, CancellationToken ct)
    {
        if (!QuicConnection.IsSupported)
            return null;

        var port = serverUri.Port > 0 ? serverUri.Port : 443;
        var connection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
        {
            RemoteEndPoint = new IPEndPoint(
                (await Dns.GetHostAddressesAsync(serverUri.Host, ct))[0], port),
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            MaxInboundBidirectionalStreams = 1024,
            MaxInboundUnidirectionalStreams = 1024,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = [new SslApplicationProtocol("bolt")],
                TargetHost = serverUri.Host,
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        }, ct);

        var quicConn = new QuicBoltConnection(connection);
        // Don't pass the attempt-timeout CT — the pool must live as long as the connection.
        // It gets cancelled when QuicBoltConnection.CloseAsync/DisposeAsync is called.
        await quicConn.StartStreamPoolAsync();
        return quicConn;
    }

    private static Task<IBoltConnection?> TryWebTransportAsync(Uri serverUri, CancellationToken ct)
    {
        // WebTransport client is not available in .NET natively — only via browser APIs.
        // Skipped for .NET server-to-server. Activates in Blazor WASM via JS interop (future).
        return Task.FromResult<IBoltConnection?>(null);
    }

    private static async Task<IBoltConnection?> TryWebSocketAsync(Uri serverUri, CancellationToken ct)
    {
        var wsScheme = serverUri.Scheme switch
        {
            "https" or "wss" or "quic" => "wss",
            _ => "ws"
        };
        var wsUri = new UriBuilder(serverUri) { Scheme = wsScheme }.Uri;

        var ws = new ClientWebSocket();
        await ws.ConnectAsync(wsUri, ct);
        return new WebSocketBoltConnection(ws);
    }
}
