using System.Text.Json;
using Bolt.Client;
using Bolt.Media.Browser;
using Microsoft.JSInterop;

namespace Bolt.Rtc.CallClient;

/// <summary>
/// What the browser call tests drive from Playwright. <see cref="Start"/> joins a hosted, SFrame-encrypted call the way
/// the Yap client does (VoiceState.ConnectEncryptedGroupAsync and its epoch steps), with the epoch keys handed over by
/// the test instead of the authenticated envelopes, then publishes audio and turns the camera on.
/// </summary>
public static class CallDriver
{
    private static BoltMediaService? _media;
    private static HttpClient? _http;
    private static ILoggerFactory? _logs;
    private static BoltClient? _client;
    private static string _phase = "idle";
    private static string? _error;

    public static bool Ready => _media is not null;

    internal static void Bind(BoltMediaService media, HttpClient http, ILoggerFactory logs) => (_media, _http, _logs) = (media, http, logs);

    public sealed record Peer(string Id, string Kid, string Key);

    public sealed record Config(string Endpoint, string Join, Guid CallId, string Epoch, string Binding, Peer Local, Peer[] Remote,
        int Height, int Framerate, int Ceiling, bool Video = true);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [JSInvokable]
    public static async Task<string> Start(string json)
    {
        var config = JsonSerializer.Deserialize<Config>(json, Json)!;
        var media = _media ?? throw new InvalidOperationException("The page has not rendered yet.");
        try
        {
            _phase = "voice";
            await media.PrepareVoiceAsync();
            _phase = "sframe";
            await media.ConfigureSFrameAsync(config.CallId, config.Local.Id);
            var client = _client = new BoltClient(new Uri(config.Endpoint), config.Local.Id, "call test",
                new BoltClientOptions { MinConnections = 1, MaxConnections = 1, MaxFrameBytes = 65536, AutoReconnect = false },
                _logs!.CreateLogger("CallTest.Bolt"));
            _phase = "initialize";
            await media.InitializeAsync(client);
            _phase = "connect";
            await client.ConnectAsync(CancellationToken.None);
            await media.JoinHostedGroupAsync(config.CallId);
            // The host admits the participant (YapCallGateway.Groups: Server.JoinGroupCallAsync).
            _phase = "admit";
            (await _http!.PostAsync(config.Join, null)).EnsureSuccessStatusCode();
            _phase = "epoch";
            var local = Key(config.Local);
            await media.InstallSFrameEpochAsync(config.Epoch, config.Binding, local, config.Remote.Select(Key).ToArray(), CallMediaFormat.Current);
            await media.ActivateSFrameEpochAsync(config.Epoch, config.Binding);
            _phase = "audio";
            await media.StartHostedAudioAsync(config.CallId);
            await media.StartAudioAsync();
            if (config.Video)
            {
                _phase = "video";
                await media.StartVideoAsync(config.CallId, VideoCodec.H264, config.Ceiling, preferredHeight: config.Height,
                    preferredFramerate: config.Framerate);
            }
            _phase = "running";
            return "ok";
        }
        catch (Exception error)
        {
            _error = $"{_phase}: {error.GetType().Name}: {error.Message}";
            return _error;
        }
    }

    private static SFrameSenderKey Key(Peer peer) => new(peer.Id, peer.Kid, Convert.FromBase64String(peer.Key));

    /// <summary>Everything the test asserts on from this side, as JSON.</summary>
    [JSInvokable]
    public static async Task<string> Stats()
    {
        var media = _media;
        if (media is null) return "{}";
        var diagnostics = media.IsInitialized ? await media.GetVideoDiagnosticsAsync(true) : null;
        var rate = media.SendRate;
        var path = media.IsInitialized ? media.MediaPath : null;
        return JsonSerializer.Serialize(new
        {
            phase = _phase, error = _error,
            path = path?.Description, pathKind = path?.Kind.ToString(), pathReason = path?.Reason, relayLeg = path?.RelayLeg, rttMs = path?.RttMs,
            tier = media.ActiveVideoTier is { } tier ? new { tier.Width, tier.Height, tier.Framerate, tier.BitrateKbps } : null,
            rate = rate is { } r ? new { r.TotalKbps, r.VideoKbps, r.AudioKbps, r.VideoSuspended, signal = r.Signal.ToString(), r.DelayMs } : null,
            transport = diagnostics?.Transport,
            receive = media.IsInitialized ? media.GetVideoReceiveStats() : [],
            send = media.LastSendTick is { } tick ? new
            {
                pacerDelayMs = tick.Pacer.QueueDelayMs, pacerSentKbps = tick.Pacer.SentKbps, backlog = tick.Pacer.BacklogBytes,
                capacityKbps = tick.Pacer.CapacityKbps, droppedPictures = media.PacerDroppedPictures, baseLosses = media.PacerBaseLosses,
                uplinkDelayMs = tick.Transport?.QueueDelayMs, uplinkLoss = tick.Transport is { } t ? Math.Round(t.LossFraction, 3) : (double?)null,
                uplinkDeliveredKbps = tick.Transport?.DeliveredKbps,
                relayQueueMs = tick.Relay?.QueueDelayMs, relayCapacityKbps = tick.Relay?.CapacityKbps, relayDropping = tick.Relay?.Dropping,
                relayBaseLost = tick.Relay?.BaseLost, receiverDelayMs = tick.Receiver?.QueueDelayMs, receiverKbps = tick.Receiver?.ReceivedKbps,
            } : null,
        }, Json);
    }

    [JSInvokable]
    public static async Task Stop()
    {
        var media = _media;
        try { if (media?.IsInitialized == true) await media.StopVideoAsync(); } catch { /* Tearing down. */ }
        if (_client is { } client) await client.DisposeAsync();
    }
}
