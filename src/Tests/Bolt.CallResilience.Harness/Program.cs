// Call relay network harness. See README.md.
//
//   relay     Kestrel + the real BoltServer group relay with Yap's options, a local sender that
//             publishes Opus-sized audio and fragmented video exactly as the browser client does
//             (clear MediaFrame headers, opaque payloads standing in for SFrame ciphertext), and
//             a policy that authorizes both participants (optionally failing for a window).
//             ADAPTIVE=1 makes that sender the browser client's real send path (pacer, rate
//             controller, picture ladder) over a synthetic encoder; see AdaptiveSender.cs.
//   receiver  A participant on the far side of a tc-netem shaped link. It measures one-way delay
//             (both containers share the host clock), audio continuity and decodable video.
//
// Every run ends with one "SUMMARY {json}" line per side; summarize.py turns them into a table.
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;

var mode = args.FirstOrDefault() ?? "relay";
return mode switch
{
    "relay" => await Relay.RunAsync(),
    "receiver" => await Receiver.RunAsync(),
    _ => Usage()
};

static int Usage()
{
    Console.Error.WriteLine("usage: Bolt.CallResilience.Harness relay|receiver");
    return 2;
}

internal static class Env
{
    public static int Int(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;
    public static string Text(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static void Log(string line) => Console.WriteLine(line);
}

/// <summary>
/// Payload the harness puts where SFrame ciphertext would be. The relay never reads it.
/// [0..8) send time (unix ms), [8] kind (0 audio, 1 video), [9] keyframe, [10..14) picture id,
/// [14..16) fragment index, [16..18) fragment count, [18] temporal layer, [19..23) the picture this one
/// refers to (uint.MaxValue for a keyframe), so the receiver can tell exactly which pictures decode.
/// </summary>
internal static class Payload
{
    public const int HeaderSize = 23;
    public const byte Audio = 0, Video = 1;

    public static byte[] Create(int size, byte kind, bool keyframe = false, uint picture = 0, int index = 0, int count = 1,
        int layer = 0, uint? reference = null)
    {
        var payload = new byte[Math.Max(HeaderSize, size)];
        BinaryPrimitives.WriteInt64LittleEndian(payload, Env.NowMs());
        payload[8] = kind;
        payload[9] = keyframe ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(10), picture);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14), (ushort)index);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16), (ushort)count);
        payload[18] = (byte)layer;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(19), keyframe ? uint.MaxValue : reference ?? picture - 1);
        return payload;
    }
}

internal static class Frames
{
    public static byte[] Write(Action<ArrayBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Reads one whole WebSocket message; null when the socket closed.</summary>
    public static async Task<byte[]?> ReceiveAsync(WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        var length = 0;
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(length), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            length += result.Count;
            if (result.EndOfMessage) return buffer.AsSpan(0, length).ToArray();
            if (length == buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
        }
    }

    /// <summary>Flattens a Bolt batch into its frames; a plain frame is returned as is.</summary>
    public static IEnumerable<byte[]> Unbatch(byte[] message)
    {
        if (message.Length > 0 && message[0] == (byte)FrameType.Batch && BoltCodec.TryReadBatch(message, out var batch))
        {
            var frames = new List<byte[]>();
            foreach (var frame in batch) frames.Add(frame.ToArray());
            return frames;
        }
        return [message];
    }
}

internal static class Relay
{
    public static async Task<int> RunAsync()
    {
        var seconds = Env.Int("SECONDS", 180);
        var call = Guid.NewGuid();
        var clock = Stopwatch.StartNew();
        double T() => Math.Round(clock.Elapsed.TotalSeconds, 2);
        var policy = new Policy(clock, Env.Int("AUTH_DELAY_MS", 30), Env.Int("AUTH_FAIL_AT_S", -1), Env.Int("AUTH_FAIL_FOR_S", 0));

        // Exactly the Yap encrypted-group relay (YapCallGateway). Options a baseline relay does not
        // have are skipped, so the same harness measures before and after.
        var options = new BoltServerOptions
        {
            MediaEnabled = true, RequireSecureTransport = true, AuthenticatedMediaOnly = true, RequireEncryptedMedia = true,
            CallAuthorizer = policy, GroupCallAuthorizer = policy, MaxActiveCalls = 64, MaxActiveCallsPerPrincipal = 1,
            MaxCallParticipants = 8, MaxMediaStreamsPerPrincipal = 2, MaxFrameBytes = 64 * 1024,
            SendQueueCapacity = 64, SendQueueByteCapacity = 1024 * 1024, SendEnqueueTimeoutMs = Env.Int("DEADLINE_MS", 250),
            MaxConnectionsPerPrincipal = 2, MaxConnectionLifetimeSeconds = 3600
        };
        var stallApplied = TrySet(options, "TransportSendStallTimeoutMs", Env.Int("STALL_MS", 15_000));
        TrySet(options, "GroupAuthorizationGraceSeconds", Env.Int("AUTH_GRACE_S", 120));

        var counters = new ConcurrentDictionary<string, long>();
        using var meters = ListenToRelayMetrics(counters);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls("http://0.0.0.0:8080");
        var app = builder.Build();
        app.UseWebSockets();
        using var server = new BoltServer(app.Services.GetRequiredService<ILogger<BoltServer>>(), options);

        string? outcome = null;
        var unsentLimited = false;
        server.GroupParticipantRemoved += (_, id) =>
        {
            Env.Log($"REMOVED t={T()} client={id}");
            outcome ??= $"{id} removed at {T()}s";
        };
        app.Map("/ws", async (HttpContext context) =>
        {
            var id = context.Request.Query["id"].ToString();
            // Yap limits the kernel's unsent backlog on call sockets; relays without the helper skip it.
            unsentLimited |= LimitUnsentBytes(context, Env.Int("UNSENT_BYTES", 32 * 1024));
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id), new Claim("bolt_media_client_id", id)], "harness"));
            await server.HandleConnectionAsync(new WebSocketBoltConnection(socket), principal, context.RequestAborted, isSecureTransport: true);
            Env.Log($"CONNECTION-ENDED t={T()} client={id}");
            if (T() < seconds) outcome ??= $"{id} connection ended at {T()}s";
        });
        await app.StartAsync();

        var profile = new SenderProfile(
            AudioPayload: Env.Int("AUDIO_PAYLOAD", 104),
            VideoKbps: Env.Int("VIDEO_KBPS", 180),
            Fps: Math.Max(1, Env.Int("FPS", 15)),
            KeyframeRatio: Env.Int("KF_RATIO", 6),
            KeyframeIntervalMs: Env.Int("KF_MS", 10_000),
            KeyframeOnDemand: Env.Int("KF_ON_DEMAND", 1) == 1);
        Env.Log($"RELAY start {JsonSerializer.Serialize(profile)} deadlineMs={options.SendEnqueueTimeoutMs} stallApplied={stallApplied} seconds={seconds}");

#if HARNESS_ADAPTIVE
        var adaptive = Env.Int("ADAPTIVE", 0) == 1;
        var adaptiveProfile = new AdaptiveProfile(
            StartHeight: Env.Int("START_HEIGHT", 240),
            AudioKbps: Env.Int("AUDIO_KBPS", 32),
            KeyframeRatio: Env.Int("KF_RATIO", 6),
            KeyframeIntervalMs: Env.Int("KF_MS", 10_000),
            TemporalLayers: Env.Int("SVC", 1) == 1,
            Overshoot: Env.Int("OVERSHOOT_PCT", 100) / 100.0);
        await using var adaptiveSender = adaptive ? new AdaptiveSender(clock) : null;
        if (adaptive) Env.Log($"RELAY adaptive {JsonSerializer.Serialize(adaptiveProfile)}");
#else
        const bool adaptive = false;
#endif
        await using var sender = new Sender(clock);
#if HARNESS_ADAPTIVE
        if (adaptiveSender is not null) await adaptiveSender.ConnectAsync(new Uri("ws://127.0.0.1:8080/ws?id=sender"));
        else
#endif
        await sender.ConnectAsync(new Uri("ws://127.0.0.1:8080/ws?id=sender"));
        if (!await JoinAsync(server, call, "sender", TimeSpan.FromSeconds(10)) ||
            !await JoinAsync(server, call, "receiver", TimeSpan.FromSeconds(120)))
        {
            Env.Log("SUMMARY " + JsonSerializer.Serialize(new { side = "relay", outcome = "join failed" }));
            return 1;
        }
        Env.Log($"RELAY joined t={T()} unsentLimited={unsentLimited}");
        clock.Restart();
#if HARNESS_ADAPTIVE
        if (adaptiveSender is not null) adaptiveSender.Start(call, adaptiveProfile);
        else
#endif
        sender.Start(call, profile);

        long AudioSent() =>
#if HARNESS_ADAPTIVE
            adaptiveSender?.AudioSent ??
#endif
            sender.AudioSent;
        long VideoSent() =>
#if HARNESS_ADAPTIVE
            adaptiveSender?.VideoSent ??
#endif
            sender.VideoSent;
        long Keyframes() =>
#if HARNESS_ADAPTIVE
            adaptiveSender?.Keyframes ??
#endif
            sender.Keyframes;
        long KeyRequests() =>
#if HARNESS_ADAPTIVE
            adaptiveSender?.KeyRequests ??
#endif
            sender.KeyRequests;

        while (clock.Elapsed.TotalSeconds < seconds)
        {
            await Task.Delay(1000);
            Env.Log($"RELAY t={T():F0} sentAudio={AudioSent()} sentVideo={VideoSent()} keyframes={Keyframes()} " +
                    $"keyRequests={KeyRequests()} {string.Join(' ', counters.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))}");
        }

        object? rate = null;
#if HARNESS_ADAPTIVE
        rate = adaptiveSender?.Report(seconds);
#endif
        Env.Log("SUMMARY " + JsonSerializer.Serialize(new
        {
            side = "relay",
            outcome = outcome ?? "survived",
            seconds,
            adaptive,
            audioSent = AudioSent(),
            videoPicturesSent = VideoSent(),
            keyframesSent = Keyframes(),
            keyframeRequestsReceived = KeyRequests(),
            rate,
            counters
        }));
#if HARNESS_ADAPTIVE
        if (adaptiveSender is not null) await adaptiveSender.DisposeAsync();
#endif
        await sender.DisposeAsync();
        // Close the calls with a handshake and give the close frames time to cross the shaped link;
        // a container that just exits takes its queued packets (and the receiver's FIN) with it.
        server.Dispose();
        await Task.Delay(3000);
        await app.StopAsync(TimeSpan.FromSeconds(2));
        return 0;
    }

    private static async Task<bool> JoinAsync(BoltServer server, Guid call, string id, TimeSpan patience)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < patience)
        {
            if (await server.JoinGroupCallAsync(call, id)) return true;
            await Task.Delay(200);
        }
        return false;
    }

    private static bool LimitUnsentBytes(HttpContext context, int bytes)
    {
        var tuning = typeof(BoltServer).Assembly.GetType("Bolt.Server.BoltSocketTuning");
        var socket = context.Features.Get<Microsoft.AspNetCore.Connections.Features.IConnectionSocketFeature>()?.Socket;
        return tuning?.GetMethod("TryLimitUnsentBytes")?.Invoke(null, [socket, bytes]) is true;
    }

    private static bool TrySet(object target, string property, object value)
    {
        var info = target.GetType().GetProperty(property);
        if (info is null || !info.CanWrite) return false;
        info.SetValue(target, value);
        return true;
    }

    private static MeterListener ListenToRelayMetrics(ConcurrentDictionary<string, long> counters)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == "Bolt.Server" && instrument is Counter<long>) meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var name = instrument.Name.Replace("bolt.server.", "");
            foreach (var tag in tags) name += $"[{tag.Value}]";
            counters.AddOrUpdate(name, value, (_, current) => current + value);
        });
        listener.Start();
        return listener;
    }
}

internal sealed record SenderProfile(int AudioPayload, int VideoKbps, int Fps, int KeyframeRatio, int KeyframeIntervalMs, bool KeyframeOnDemand);

/// <summary>A participant on an unshaped link publishing one audio and one video stream.</summary>
internal sealed class Sender(Stopwatch clock) : IAsyncDisposable
{
    private const int FragmentPayload = 4084 + 12 + 26; // fragment + fragment header + SFrame overhead
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Guid _audio = Guid.NewGuid(), _video = Guid.NewGuid();
    private uint _audioSequence, _videoSequence;
    private volatile bool _keyframeRequested;
    private Task _loops = Task.CompletedTask;
    public long AudioSent, VideoSent, Keyframes, KeyRequests;

    public async Task ConnectAsync(Uri uri)
    {
        await _socket.ConnectAsync(uri, CancellationToken.None);
        await SendAsync(Frames.Write(w => BoltCodec.WriteRegister(w, "sender", "sender")));
        var buffer = new byte[64 * 1024];
        await Frames.ReceiveAsync(_socket, buffer, CancellationToken.None); // RegisterAck
        _ = Task.Run(ReceiveLoopAsync);
    }

    public void Start(Guid call, SenderProfile profile)
    {
        _loops = Task.WhenAll(Task.Run(() => AudioLoopAsync(call, profile)), Task.Run(() => VideoLoopAsync(call, profile)));
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (!_stop.IsCancellationRequested && await Frames.ReceiveAsync(_socket, buffer, _stop.Token) is { } message)
                foreach (var frame in Frames.Unbatch(message))
                    if (frame[0] == (byte)FrameType.MediaKeyRequest && BoltCodec.TryReadMediaKeyRequest(frame, out var stream) && stream == _video)
                    { Interlocked.Increment(ref KeyRequests); _keyframeRequested = true; }
        }
        catch { /* Shutdown. */ }
    }

    private async Task AudioLoopAsync(Guid call, SenderProfile profile)
    {
        await SendAsync(Frames.Write(w => BoltCodec.WriteMediaConfig(w, _audio, call, MediaType.Audio, CodecId.Opus, 48_000, 1, 32, 0x10, [])));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        var timestamp = 0u;
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                timestamp += 960;
                var payload = Payload.Create(profile.AudioPayload, Payload.Audio);
                var sequence = ++_audioSequence;
                var ts = timestamp;
                await SendAsync(Frames.Write(w => BoltCodec.WriteMediaFrame(w, _audio, sequence, ts, 0x10, payload)));
                Interlocked.Increment(ref AudioSent);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task VideoLoopAsync(Guid call, SenderProfile profile)
    {
        if (profile.VideoKbps <= 0) return;
        await SendAsync(Frames.Write(w => BoltCodec.WriteMediaConfig(w, _video, call, MediaType.Video, CodecId.H264, 426, 240, profile.VideoKbps, 0x10, [])));
        // Delta size such that the average over one keyframe interval matches the tier bitrate.
        var picturesPerInterval = Math.Max(1, profile.Fps * profile.KeyframeIntervalMs / 1000);
        var bytesPerInterval = profile.VideoKbps * 1000.0 / 8 * profile.KeyframeIntervalMs / 1000.0;
        var delta = (int)(bytesPerInterval / (picturesPerInterval - 1 + profile.KeyframeRatio));
        var key = delta * profile.KeyframeRatio;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000.0 / profile.Fps));
        uint picture = 0;
        long lastKeyframe = long.MinValue / 2;
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                var now = clock.ElapsedMilliseconds;
                // The browser encoder's policy: forced first picture, a long safety interval, and
                // coalesced on-demand requests at most once a second.
                var isKey = picture == 0 || now - lastKeyframe >= profile.KeyframeIntervalMs ||
                            (profile.KeyframeOnDemand && _keyframeRequested && now - lastKeyframe >= 1000);
                if (isKey) { lastKeyframe = now; _keyframeRequested = false; Interlocked.Increment(ref Keyframes); }
                picture++;
                var size = isKey ? key : delta;
                var count = Math.Max(1, (size + FragmentPayload - 1) / FragmentPayload);
                var timestamp = (uint)(now * 90);
                for (var index = 0; index < count; index++)
                {
                    var payload = Payload.Create(Math.Min(FragmentPayload, size - index * FragmentPayload), Payload.Video, isKey, picture, index, count,
                        layer: 0, reference: picture - 1);
                    var sequence = ++_videoSequence;
                    var flags = (byte)(0x10 | (isKey && index == 0 ? 0x01 : 0));
                    await SendAsync(Frames.Write(w => BoltCodec.WriteMediaFrame(w, _video, sequence, timestamp, flags, payload)));
                }
                Interlocked.Increment(ref VideoSent);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SendAsync(byte[] frame)
    {
        await _sendLock.WaitAsync();
        try { await _socket.SendAsync(frame, WebSocketMessageType.Binary, true, CancellationToken.None); }
        finally { _sendLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _loops; } catch { }
        _socket.Abort();
        _socket.Dispose();
    }
}

internal static class Receiver
{
    public static async Task<int> RunAsync()
    {
        var uri = new Uri(Env.Text("RELAY", "ws://relay:8080/ws?id=receiver"));
        using var socket = new ClientWebSocket();
        for (var attempt = 0; ; attempt++)
        {
            try { await socket.ConnectAsync(uri, CancellationToken.None); break; }
            catch when (attempt < 120) { await Task.Delay(500); }
        }
        await socket.SendAsync(Frames.Write(w => BoltCodec.WriteRegister(w, "receiver", "receiver")), WebSocketMessageType.Binary, true, CancellationToken.None);

        var clock = Stopwatch.StartNew();
        var kinds = new Dictionary<Guid, MediaType>();
        var audioDelays = new List<long>();
        var videoDelays = new List<long>();
        var window = new Window();
        long? firstAudioSequence = null, lastAudioSequence = null, lastAudioArrival = null, firstMediaAt = null;
        long audioReceived = 0, longestAudioGap = 0, silentSeconds = 0, frozenSeconds = 0;
        long picturesComplete = 0, picturesDecodable = 0, keyframes = 0;
        var layerPictures = new long[4];
        var pending = new Dictionary<uint, int>();
        // Pictures a decoder showed, so a later picture can be checked against the one it refers to.
        var decodable = new HashSet<uint>();
        var decodableOrder = new Queue<uint>();
        uint? lastDecodable = null;
#if HARNESS_ADAPTIVE
        // Like the browser client's receive side: a delay report per stream every 250 ms, back to the sender.
        var feedback = new ConcurrentDictionary<Guid, ReceiverFeedback>();
        var sendLock = new SemaphoreSlim(1, 1);
        using var feedbackStop = new CancellationTokenSource();
        var feedbackLoop = Task.Run(async () =>
        {
            try
            {
                while (!feedbackStop.IsCancellationRequested)
                {
                    await Task.Delay(250, feedbackStop.Token);
                    foreach (var stream in feedback.Values)
                    {
                        byte[]? report;
                        lock (stream) report = stream.Build(Environment.TickCount64);
                        if (report is null) continue;
                        await sendLock.WaitAsync(feedbackStop.Token);
                        try { await socket.SendAsync(report, WebSocketMessageType.Binary, true, feedbackStop.Token); }
                        finally { sendLock.Release(); }
                    }
                }
            }
            catch { /* The call ended. */ }
        });
#endif
        var end = "socket closed";
        var buffer = new byte[128 * 1024];
        var lastTick = 0L;
        // Stop on our own if the relay's close never arrives (it can be lost with a dead link).
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(Env.Int("SECONDS", 180) + 150));
        try
        {
            while (await Frames.ReceiveAsync(socket, buffer, patience.Token) is { } message)
            {
                var now = Env.NowMs();
                foreach (var frame in Frames.Unbatch(message))
                {
                    if (frame[0] == (byte)FrameType.MediaConfig && BoltCodec.TryReadMediaConfig(frame, out var config))
                    { kinds[config.StreamId] = config.MediaType; continue; }
                    if (frame[0] != (byte)FrameType.MediaFrame || !BoltCodec.TryReadMediaFrame(frame, out var header)) continue;
                    var payload = header.GetPayload(frame);
                    if (payload.Length < Payload.HeaderSize) continue;
#if HARNESS_ADAPTIVE
                    var isAudioStream = payload[8] == Payload.Audio;
                    var tracker = feedback.GetOrAdd(header.StreamId, id => new ReceiverFeedback(id, isAudioStream));
                    lock (tracker) tracker.Observe(header.SequenceNumber, header.Timestamp, frame.Length, Environment.TickCount64);
#endif
                    firstMediaAt ??= clock.ElapsedMilliseconds;
                    var delay = now - BinaryPrimitives.ReadInt64LittleEndian(payload);
                    window.Bytes += frame.Length;
                    if (payload[8] == Payload.Audio)
                    {
                        audioDelays.Add(delay); window.Audio.Add(delay); audioReceived++;
                        firstAudioSequence ??= header.SequenceNumber;
                        lastAudioSequence = header.SequenceNumber;
                        var arrival = clock.ElapsedMilliseconds;
                        if (lastAudioArrival is { } previous) longestAudioGap = Math.Max(longestAudioGap, arrival - previous);
                        lastAudioArrival = arrival;
                        continue;
                    }

                    videoDelays.Add(delay); window.Video.Add(delay);
                    var picture = BinaryPrimitives.ReadUInt32LittleEndian(payload[10..]);
                    var count = BinaryPrimitives.ReadUInt16LittleEndian(payload[16..]);
                    var got = pending[picture] = pending.GetValueOrDefault(picture) + 1;
                    if (got < count) continue;
                    pending.Remove(picture);
                    foreach (var stale in pending.Keys.Where(x => x < picture).ToArray()) pending.Remove(stale);
                    picturesComplete++;
                    var isKey = payload[9] == 1;
                    if (isKey) keyframes++;
                    // A picture decodes if it is a keyframe or the picture it refers to decoded. Without temporal
                    // layers that is the one right before it; with them, the last lower-layer picture.
                    var reference = BinaryPrimitives.ReadUInt32LittleEndian(payload[19..]);
                    if (isKey || decodable.Contains(reference))
                    {
                        lastDecodable = picture; picturesDecodable++; window.Decodable++;
                        layerPictures[Math.Min(3, (int)payload[18])]++;
                        decodable.Add(picture); decodableOrder.Enqueue(picture);
                        if (decodableOrder.Count > 4096) decodable.Remove(decodableOrder.Dequeue());
                    }
                }

                var second = clock.ElapsedMilliseconds / 1000;
                if (second > lastTick)
                {
                    for (var skipped = lastTick + 1; skipped < second; skipped++) { silentSeconds++; frozenSeconds++; }
                    lastTick = second;
                    if (firstMediaAt is not null)
                    {
                        if (window.Audio.Count == 0) silentSeconds++;
                        if (window.Decodable == 0 && lastDecodable is not null) frozenSeconds++;
                    }
                    Env.Log($"RX t={second} kbps={window.Bytes * 8 / 1000} audio={window.Audio.Count} audioDelay p50={Percentile(window.Audio, .5)} max={Percentile(window.Audio, 1)} " +
                            $"videoDelay p50={Percentile(window.Video, .5)} decodable={window.Decodable}");
                    window = new Window();
                }
            }
        }
        catch (Exception error) { end = $"{error.GetType().Name}: {error.Message}"; }
#if HARNESS_ADAPTIVE
        feedbackStop.Cancel();
        await feedbackLoop;
#endif

        var span = firstAudioSequence is { } first && lastAudioSequence is { } last ? last - first + 1 : 0;
        Env.Log("SUMMARY " + JsonSerializer.Serialize(new
        {
            side = "receiver",
            end,
            endedAtS = Math.Round(clock.Elapsed.TotalSeconds, 1),
            audioReceived,
            audioDelivered = span > 0 ? Math.Round(100.0 * audioReceived / span, 1) : 0,
            audioDelayMs = new { p50 = Percentile(audioDelays, .5), p90 = Percentile(audioDelays, .9), p99 = Percentile(audioDelays, .99), max = Percentile(audioDelays, 1) },
            longestAudioGapMs = longestAudioGap,
            silentSeconds,
            videoDelayMsP50 = Percentile(videoDelays, .5),
            picturesComplete,
            picturesDecodable,
            decodableByLayer = layerPictures,
            keyframes,
            frozenSeconds
        }));
        return 0;
    }

    private static long Percentile(List<long> values, double q)
    {
        if (values.Count == 0) return -1;
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Min(sorted.Length - 1, Math.Floor(sorted.Length * q))];
    }

    private sealed class Window
    {
        public readonly List<long> Audio = [], Video = [];
        public long Bytes, Decodable;
    }
}

/// <summary>Authorizes both harness participants, optionally failing (throwing) for a window like a hub reconnect.</summary>
internal sealed class Policy(Stopwatch clock, int delayMs, int failAtSeconds, int failForSeconds) : IBoltCallAuthorizer, IBoltGroupCallAuthorizer
{
    public ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default) => ValueTask.FromResult(false);

    public async ValueTask<bool> AuthorizeParticipantAsync(Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default)
    {
        await Task.Delay(delayMs, ct);
        var t = clock.Elapsed.TotalSeconds;
        if (failAtSeconds >= 0 && t >= failAtSeconds && t < failAtSeconds + failForSeconds)
            throw new IOException("Simulated policy backend outage (hub reconnect).");
        return true;
    }
}
