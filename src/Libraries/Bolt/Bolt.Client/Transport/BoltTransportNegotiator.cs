using System.Net.WebSockets;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging;

namespace Bolt.Client.Transport;

/// <summary>
/// Tries transports in priority order and returns the first working IBoltConnection.
/// WebTransport -> WebSocket. Each attempt has a configurable timeout.
/// QUIC is not used for RPC transport (only for media datagrams via BoltMediaStream).
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
                    BoltTransport.WebTransport => await TryWebTransportAsync(serverUri, attemptCts.Token),
                    BoltTransport.WebSocket => await TryWebSocketAsync(serverUri, options, attemptCts.Token),
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

    private static Task<IBoltConnection?> TryWebTransportAsync(Uri serverUri, CancellationToken ct)
    {
        // WebTransport client is not available in .NET natively — only via browser APIs.
        // Skipped for .NET server-to-server. Activates in Blazor WASM via JS interop (future).
        return Task.FromResult<IBoltConnection?>(null);
    }

    private static async Task<IBoltConnection?> TryWebSocketAsync(Uri serverUri, BoltClientOptions options, CancellationToken ct)
    {
        var wsScheme = serverUri.Scheme switch
        {
            "https" or "wss" or "quic" => "wss",
            _ => "ws"
        };
        var wsUri = new UriBuilder(serverUri) { Scheme = wsScheme }.Uri;
        var accessToken = await ResolveAccessTokenAsync(options, ct);

        var ws = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            if (options.SendAccessTokenAsQueryString || OperatingSystem.IsBrowser())
            {
                wsUri = AppendQueryParameter(wsUri, "access_token", accessToken);
            }
            else
            {
                ws.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            }
        }

        await ws.ConnectAsync(wsUri, ct);
        return new WebSocketBoltConnection(ws);
    }

    private static async ValueTask<string?> ResolveAccessTokenAsync(BoltClientOptions options, CancellationToken ct)
    {
        if (options.AccessTokenProvider is not null)
            return await options.AccessTokenProvider(ct);

        return options.AccessToken;
    }

    private static Uri AppendQueryParameter(Uri uri, string name, string value)
    {
        var builder = new UriBuilder(uri);
        var existingQuery = builder.Query;
        var prefix = string.IsNullOrWhiteSpace(existingQuery)
            ? string.Empty
            : existingQuery.TrimStart('?') + "&";

        builder.Query = prefix + $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
        return builder.Uri;
    }
}
