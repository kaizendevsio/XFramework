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
        var endpoint = serverUri.GetLeftPart(UriPartial.Path);
        string? failure = null;
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
                    _logger.LogInformation("Bolt connected via {Transport} to {Uri}", transport, endpoint);
                    return conn;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                failure = "Timeout";
                _logger.LogDebug("Transport {Transport} timed out, trying next", transport);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Browser/socket exception messages can contain the one-use ticket or access token.
                failure = ex is WebSocketException socket
                    ? $"{ex.GetType().Name} ({socket.WebSocketErrorCode})"
                    : ex.GetType().Name;
                _logger.LogDebug("Transport {Transport} failed ({Failure}), trying next", transport, failure);
            }
        }

        throw new InvalidOperationException(
            $"All transports failed for {endpoint}. Tried: {string.Join(", ", options.PreferredTransports)}. Failure: {failure ?? "Unavailable"}");
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

        try
        {
            await ws.ConnectAsync(wsUri, ct);
            return new WebSocketBoltConnection(ws);
        }
        catch
        {
            ws.Dispose();
            throw;
        }
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
