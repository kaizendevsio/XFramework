using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bolt.Protocol.Transport;
using Bolt.Rtc;
using Bolt.Server;

namespace Yap.Services;

/// <summary>
/// TURN configuration for UDP call media. Exactly one source is used:
/// <list type="bullet">
/// <item>Cloudflare Realtime TURN (<c>Yap:Calls:Turn:CloudflareKeyId</c> + <c>CloudflareApiToken</c>): the server
/// mints short-lived ICE servers per participant through Cloudflare's API. The API token never leaves this process.</item>
/// <item>A TURN server with a shared secret (<c>Urls</c> + <c>SharedSecret</c>, coturn's <c>use-auth-secret</c>): time-limited
/// credentials are derived here (TURN REST API scheme), per participant.</item>
/// <item>Static <c>Urls</c> + <c>Username</c> + <c>Credential</c>, for tests and other providers. Not short-lived; prefer the above.</item>
/// </list>
/// With none configured, every call stays on its WebSocket exactly as before.
/// </summary>
public sealed class YapTurnOptions
{
    public string? CloudflareKeyId { get; init; }
    public string? CloudflareApiToken { get; init; }
    public string[] Urls { get; init; } = [];
    public string? SharedSecret { get; init; }
    public string? Username { get; init; }
    public string? Credential { get; init; }
    /// <summary>Lifetime of minted credentials. Sessions renew a few minutes before it runs out.</summary>
    public int CredentialTtlSeconds { get; init; } = 3600;
    /// <summary>The relay's own peer uses relay candidates only (see <see cref="BoltMediaTransportOptions.RelayOnly"/>).</summary>
    public bool RelayOnly { get; init; } = true;

    public bool UsesCloudflare => !string.IsNullOrWhiteSpace(CloudflareKeyId) && !string.IsNullOrWhiteSpace(CloudflareApiToken);
    public bool UsesSharedSecret => Urls.Length > 0 && !string.IsNullOrEmpty(SharedSecret);
    public bool UsesStatic => Urls.Length > 0 && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Credential);
    public bool IsConfigured => UsesCloudflare || UsesSharedSecret || UsesStatic;

    public static YapTurnOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection("Yap:Calls:Turn");
        return new YapTurnOptions
        {
            CloudflareKeyId = section["CloudflareKeyId"],
            CloudflareApiToken = section["CloudflareApiToken"],
            Urls = section.GetSection("Urls").Get<string[]>() ?? (section["Urls"] is { Length: > 0 } list
                ? list.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : []),
            SharedSecret = section["SharedSecret"],
            Username = section["Username"],
            Credential = section["Credential"],
            CredentialTtlSeconds = Math.Clamp(section.GetValue("CredentialTtlSeconds", 3600), 600, 6 * 3600),
            RelayOnly = section.GetValue("RelayOnly", true),
        };
    }
}

/// <summary>
/// Issues each participant's ICE servers. Cloudflare's endpoint is
/// <c>POST https://rtc.live.cloudflare.com/v1/turn/keys/{keyId}/credentials/generate-ice-servers</c> with
/// <c>Authorization: Bearer {apiToken}</c> and <c>{"ttl": seconds}</c>; it answers with an <c>iceServers</c> array.
/// The participant's copy and the relay's own copy are minted separately, so neither outlives its session.
/// Nothing here logs a credential or the token: failures are reported by status code only.
/// </summary>
public sealed class YapTurnCredentials(YapTurnOptions options, IHttpClientFactory http, ILogger logger, TimeProvider? time = null)
    : IBoltIceServerSource
{
    /// <summary>Log category for TURN credential mints (the container log shows it at Information).</summary>
    public const string LogCategory = "Yap.Calls.Turn";

    public const string HttpClientName = "cloudflare-turn";
    internal static readonly Uri CloudflareBase = new("https://rtc.live.cloudflare.com/");
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async ValueTask<BoltIceGrant?> GrantAsync(ClaimsPrincipal participant, CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(options.CredentialTtlSeconds);
        var expires = _time.GetUtcNow() + ttl;
        if (options.UsesCloudflare)
        {
            var client = await MintCloudflareAsync(ttl, ct);
            var server = client is null ? null : await MintCloudflareAsync(ttl, ct);
            if (client is null || server is null) return null;
            return new BoltIceGrant(ForBrowser(client), ForRelay(server), expires);
        }
        if (options.UsesSharedSecret)
        {
            // TURN REST API (coturn use-auth-secret): username "expiry:label", password base64(HMAC-SHA1(secret, username)).
            var servers = (string label) =>
            {
                var username = $"{expires.ToUnixTimeSeconds()}:{label}";
                var credential = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(options.SharedSecret!), Encoding.UTF8.GetBytes(username)));
                return (IReadOnlyList<RtcIceServer>)[new RtcIceServer(options.Urls, username, credential)];
            };
            // The label is random: it identifies a session to the TURN server's logs, never a person.
            return new BoltIceGrant(servers(Convert.ToHexString(RandomNumberGenerator.GetBytes(8))), servers("relay"), expires);
        }
        if (options.UsesStatic)
        {
            IReadOnlyList<RtcIceServer> servers = [new RtcIceServer(options.Urls, options.Username, options.Credential)];
            return new BoltIceGrant(servers, servers, expires);
        }
        return null;
    }

    private async Task<IReadOnlyList<RtcIceServer>?> MintCloudflareAsync(TimeSpan ttl, CancellationToken ct)
    {
        var client = http.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(CloudflareBase, $"v1/turn/keys/{Uri.EscapeDataString(options.CloudflareKeyId!.Trim())}/credentials/generate-ice-servers"))
        {
            Content = JsonContent.Create(new CloudflareRequest((int)ttl.TotalSeconds), CloudflareJson.Default.CloudflareRequest),
        };
        request.Headers.Authorization = new("Bearer", options.CloudflareApiToken!.Trim());
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Cloudflare TURN refused to issue ICE servers (HTTP {Status}); calls stay on WebSockets", (int)response.StatusCode);
                return null;
            }
            var body = await response.Content.ReadFromJsonAsync(CloudflareJson.Default.JsonElement, ct);
            if (ParseIceServers(body) is not { } servers)
            {
                logger.LogWarning("Cloudflare TURN answered HTTP {Status} without usable ICE servers; calls stay on WebSockets", (int)response.StatusCode);
                return null;
            }
            // The status and how many URLs came back; never a URL's credential, the key or the token.
            logger.LogInformation("Cloudflare TURN issued ICE servers (HTTP {Status}, {Urls} URLs)", (int)response.StatusCode, servers.Sum(x => x.Urls.Length));
            return servers;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Cloudflare TURN could not be reached ({Error}); calls stay on WebSockets", ex.GetType().Name);
            return null;
        }
    }

    /// <summary>Accepts both shapes Cloudflare has used: <c>iceServers</c> as an array, or as a single object.</summary>
    internal static IReadOnlyList<RtcIceServer>? ParseIceServers(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("iceServers", out var servers)) return null;
        var list = new List<RtcIceServer>();
        foreach (var server in servers.ValueKind == JsonValueKind.Array ? servers.EnumerateArray() : Enumerable.Repeat(servers, 1))
        {
            if (server.ValueKind != JsonValueKind.Object || !server.TryGetProperty("urls", out var urls)) continue;
            var values = urls.ValueKind == JsonValueKind.Array
                ? urls.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray()
                : urls.ValueKind == JsonValueKind.String ? [urls.GetString()!] : [];
            if (values.Length == 0) continue;
            list.Add(new RtcIceServer(values,
                server.TryGetProperty("username", out var user) && user.ValueKind == JsonValueKind.String ? user.GetString() : null,
                server.TryGetProperty("credential", out var credential) && credential.ValueKind == JsonValueKind.String ? credential.GetString() : null));
        }
        return list.Any(x => x.Credential is not null) ? list : null;
    }

    /// <summary>
    /// What the browser gets: every TURN URL (UDP, TCP and TLS, so a network that blocks UDP can still reach
    /// TURN over TLS on 443), minus port 53, which browsers block and which only delays gathering.
    /// </summary>
    internal static IReadOnlyList<RtcIceServer> ForBrowser(IReadOnlyList<RtcIceServer> servers) =>
        Keep(servers, url => !IsPort(url, 53));

    /// <summary>
    /// What the relay's own peer gets: TURN over UDP on 3478 (the host's outbound UDP works) and TURN over TLS on
    /// 443 as the fallback. Each URL becomes an allocation, so the other four would only add load.
    /// </summary>
    internal static IReadOnlyList<RtcIceServer> ForRelay(IReadOnlyList<RtcIceServer> servers) =>
        Keep(servers, url => url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase) && IsPort(url, 3478) && url.Contains("transport=udp", StringComparison.OrdinalIgnoreCase)
                             || url.StartsWith("turns:", StringComparison.OrdinalIgnoreCase) && IsPort(url, 443));

    private static IReadOnlyList<RtcIceServer> Keep(IReadOnlyList<RtcIceServer> servers, Func<string, bool> keep) =>
        servers.Where(x => x.Credential is not null)
            .Select(x => x with { Urls = x.Urls.Where(keep).ToArray() })
            .Where(x => x.Urls.Length > 0)
            .ToArray();

    private static bool IsPort(string url, int port)
    {
        var rest = url[(url.IndexOf(':') + 1)..];
        var query = rest.IndexOf('?');
        if (query >= 0) rest = rest[..query];
        var colon = rest.LastIndexOf(':');
        return colon >= 0 && int.TryParse(rest[(colon + 1)..], out var value) && value == port;
    }
}

internal sealed record CloudflareRequest([property: JsonPropertyName("ttl")] int Ttl);

[JsonSerializable(typeof(CloudflareRequest))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class CloudflareJson : JsonSerializerContext;

/// <summary>
/// UDP call media for Yap: the WebRTC sidecar plus TURN credentials, or nothing. Built once per process, at startup
/// (<see cref="YapCallTransportStartup"/>); the gateway gives its options to the relay. When UDP is off it says why,
/// once, and calls carry on over WebSockets.
///
/// <c>Yap:Calls:Udp:Enabled</c> (default true) is the kill switch: false keeps every call on its WebSocket without
/// removing the TURN credentials. The relay then answers every participant's request with "disabled".
/// </summary>
public sealed class YapCallTransport : IAsyncDisposable
{
    /// <summary>Log category for UDP status and each participant's path (the container log shows it at Information).</summary>
    public const string LogCategory = "Yap.Calls.Transport";
    private readonly RtcSidecar? _sidecar;

    public YapCallTransport(IConfiguration configuration, IHttpClientFactory http, ILoggerFactory logs)
    {
        var logger = logs.CreateLogger(LogCategory);
        try
        {
            if (!configuration.GetValue("Yap:Calls:Udp:Enabled", true))
            {
                Status = "off: disabled by Yap:Calls:Udp:Enabled";
                logger.LogInformation("UDP call media is {Status}; calls use WebSockets", Status);
                return;
            }
            var turn = YapTurnOptions.From(configuration);
            if (!turn.IsConfigured)
            {
                Status = "off: no TURN configured";
                logger.LogInformation("UDP call media is off: no TURN credentials are configured; calls use WebSockets");
                return;
            }
            var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = configuration["Yap:Calls:Rtc:SidecarPath"] }, logs.CreateLogger<RtcSidecar>());
            if (!sidecar.IsAvailable)
            {
                Status = "off: WebRTC sidecar missing";
                logger.LogWarning("UDP call media is off: the bolt-rtc sidecar is not installed; calls use WebSockets");
                return;
            }
            _sidecar = sidecar;
            Status = turn.UsesCloudflare ? "on: Cloudflare TURN" : turn.UsesSharedSecret ? "on: TURN (shared secret)" : "on: TURN (static)";
            Options = new BoltMediaTransportOptions
            {
                Peers = sidecar,
                IceServers = new YapTurnCredentials(turn, http, logs.CreateLogger(YapTurnCredentials.LogCategory)),
                RelayOnly = turn.RelayOnly,
                Logger = logger,
            };
            logger.LogInformation("UDP call media is {Status}", Status);
        }
        catch (Exception ex)
        {
            // Never a reason for calls not to work: they stay on their WebSockets.
            Options = null;
            Status = "off: failed to start (" + ex.GetType().Name + ")";
            logger.LogWarning("UDP call media is {Status}; calls use WebSockets", Status);
        }
    }

    /// <summary>Null when calls stay on WebSockets.</summary>
    public BoltMediaTransportOptions? Options { get; }

    /// <summary>For logs and the call configuration endpoint: on or off, and why. Never a credential.</summary>
    public string Status { get; } = "off";

    public async ValueTask DisposeAsync()
    {
        if (_sidecar is not null) await _sidecar.DisposeAsync();
    }
}

/// <summary>Builds <see cref="YapCallTransport"/> when the app starts, so its status is in the log before anyone calls.</summary>
public sealed class YapCallTransportStartup(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = services.GetRequiredService<YapCallTransport>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
