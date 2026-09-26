// The phone side of a resumable call (RESUME=1): it times its link with relay heartbeats, decides
// when the transport is dead with the app's own CallLinkMonitor, and resumes on a new TCP connection
// with the app's own CallReconnector (both compiled in from Bolt.Media). Optional events:
//   IPCHANGE_AT_S   the old connection is silently blackholed (iptables DROP on its port, both ways)
//                   and every later connection comes from a second address: a Wi-Fi to mobile switch.
//   RX_STALL_AT_S / RX_STALL_FOR_S   the app stops reading (a frozen tab) long enough for the relay's
//                   stall watchdog to retire the socket; the phone then resumes.
// Outage timestamps written by run-profile.sh (/tmp/outage-start, /tmp/outage-end, host clock) let
// the summary say how long after the network came back audio and a decodable picture did.
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using Bolt.Media;
using Bolt.Protocol;

internal static class ResumingReceiver
{
    private const string Id = "receiver";

    public static async Task<int> RunAsync()
    {
        var seconds = Env.Int("SECONDS", 180);
        var relay = new Uri(Env.Text("RELAY_HTTP", "http://relay:8080/"));
        var network = Network.Create(Env.Int("IPCHANGE_AT_S", -1) >= 0);
        var link = new CallLinkMonitor(new CallLinkOptions { GraceMs = Env.Int("GRACE_S", 45) * 1000 });
        var metrics = new Metrics();
        var events = new Events(Env.Int("IPCHANGE_AT_S", -1), Env.Int("RX_STALL_AT_S", -1), Env.Int("RX_STALL_FOR_S", 0));
        var resumes = new List<object>();
        var clock = Stopwatch.StartNew();
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(seconds + 150));

        Session? session = null;
        for (var attempt = 0; session is null; attempt++)
        {
            try { session = await Session.OpenAsync(relay, network, resume: false, patience.Token); }
            catch when (attempt < 240 && !patience.IsCancellationRequested) { await Task.Delay(500); }
        }
        clock.Restart();
        link.Connected(Environment.TickCount64);
        Env.Log($"RX connected from {session.LocalEndPoint}");

        var end = "socket closed";
        var gaveUp = false;
        while (true)
        {
            var reason = await session.RunAsync(link, metrics, events, clock, patience.Token);
            if (reason is null || clock.Elapsed.TotalSeconds >= seconds - 3 || patience.IsCancellationRequested) { end = "closed by relay"; break; }
            link.Lost(Environment.TickCount64);
            var lostAt = clock.Elapsed.TotalSeconds;
            Env.Log($"LOST t={lostAt:F2} reason={reason} rttMs={link.SmoothedRttMs:F0}");
            Session? next = null;
            var reconnector = new CallReconnector(link, () => Environment.TickCount64, (delay, ct) => Task.Delay(delay, ct));
            var outcome = await reconnector.RunAsync(async ct =>
            {
                try { next = await Session.OpenAsync(relay, network, resume: true, ct); return CallResumeAttempt.Resumed; }
                catch (HttpRequestException error) when (error.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                { return CallResumeAttempt.Refused; }
            }, patience.Token);
            if (outcome != CallResumeOutcome.Resumed || next is null)
            {
                gaveUp = true;
                end = $"gave up ({outcome}) at {clock.Elapsed.TotalSeconds:F1}s";
                Env.Log($"GAVE-UP t={clock.Elapsed.TotalSeconds:F2} outcome={outcome} attempts={reconnector.Attempts}");
                break;
            }
            var resumedAt = clock.Elapsed.TotalSeconds;
            events.Resumed(Env.NowMs());
            Env.Log($"RESUMED t={resumedAt:F2} attempts={reconnector.Attempts} from={next.LocalEndPoint}");
            resumes.Add(new { lostAtS = Math.Round(lostAt, 2), resumedAtS = Math.Round(resumedAt, 2),
                timeToResumeMs = (long)((resumedAt - lostAt) * 1000), attempts = reconnector.Attempts, reason, from = next.LocalEndPoint });
            session = next;
        }

        var (eventStart, eventEnd) = events.Window();
        // After an IP change the old path may still hand over what was already in flight; what counts
        // is media on the connection that replaced it.
        var backFrom = events.Kind == "ip-change" && events.ResumedAtMs is { } resumedMs && eventEnd is { } changed ? Math.Max(resumedMs, changed) : eventEnd;
        Env.Log("SUMMARY " + JsonSerializer.Serialize(new
        {
            side = "receiver",
            mode = "resume",
            end,
            endedAtS = Math.Round(clock.Elapsed.TotalSeconds, 1),
            gaveUp,
            resumes,
            eventKind = events.Kind,
            audioBackAfterMs = metrics.FirstAudioAfter(backFrom) is { } audio && eventEnd is { } e1 ? audio - e1 : (long?)null,
            videoBackAfterMs = metrics.FirstPictureAfter(backFrom) is { } picture && eventEnd is { } e2 ? picture - e2 : (long?)null,
            eventSeconds = eventStart is { } s && eventEnd is { } e ? Math.Round((e - s) / 1000.0, 1) : (double?)null,
            audioReceived = metrics.AudioReceived,
            audioDelivered = metrics.AudioDelivered,
            audioDelayMs = metrics.AudioDelay(),
            longestAudioGapMs = metrics.LongestAudioGapMs,
            picturesDecodable = metrics.PicturesDecodable,
            keyframes = metrics.Keyframes
        }));
        return 0;
    }

    /// <summary>One call socket: a fresh TCP connection, registered, and (on resume) back in the relay's room.</summary>
    private sealed class Session(ClientWebSocket socket, string localEndPoint, Network network)
    {
        private readonly SemaphoreSlim sending = new(1, 1);
        public string LocalEndPoint { get; } = localEndPoint;

        public static async Task<Session> OpenAsync(Uri relay, Network network, bool resume, CancellationToken ct)
        {
            using var http = network.Http();
            var ticket = await http.GetStringAsync(new Uri(relay, $"ticket?id={Id}&resume={(resume ? 1 : 0)}"), ct);
            var socket = new ClientWebSocket();
            var handler = network.Handler();
            try
            {
                await socket.ConnectAsync(new UriBuilder(new Uri(relay, $"ws-resume?ticket={ticket}")) { Scheme = "ws" }.Uri, new HttpMessageInvoker(handler), ct);
                var session = new Session(socket, network.LastLocalEndPoint ?? "?", network);
                await session.SendAsync(Frames.Write(w => BoltCodec.WriteRegister(w, Id, Id)), ct);
                var buffer = new byte[64 * 1024];
                while (true)
                {
                    var frame = await Frames.ReceiveAsync(socket, buffer, ct) ?? throw new IOException("Closed before registering.");
                    if (Frames.Unbatch(frame).Any(x => x[0] == (byte)FrameType.RegisterAck && x.Length > 1 && x[1] == 1)) break;
                }
                (await http.GetAsync(new Uri(relay, $"ready?id={Id}"), ct)).EnsureSuccessStatusCode();
                return session;
            }
            catch
            {
                socket.Abort();
                socket.Dispose();
                throw;
            }
        }

        private async Task SendAsync(byte[] frame, CancellationToken ct)
        {
            await sending.WaitAsync(ct);
            try { await socket.SendAsync(frame, WebSocketMessageType.Binary, true, ct); }
            finally { sending.Release(); }
        }

        /// <summary>Receive until the socket ends. Null: the relay closed the call normally. Otherwise why it was lost.</summary>
        public async Task<string?> RunAsync(CallLinkMonitor link, Metrics metrics, Events events, Stopwatch clock, CancellationToken ct)
        {
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            string? lost = null;
            var frozen = false;
            var watch = Task.Run(async () =>
            {
                long lastHeartbeat = 0;
                while (!stop.IsCancellationRequested)
                {
                    await Task.Delay(250, stop.Token);
                    if (frozen) continue;
                    var now = Environment.TickCount64;
                    if (events.IpChangeDue(clock))
                    {
                        Env.Log($"IPCHANGE t={clock.Elapsed.TotalSeconds:F2} blackholing {LocalEndPoint}");
                        network.Blackhole(LocalEndPoint);
                    }
                    if (now - lastHeartbeat >= link.Options.HeartbeatIntervalMs)
                    {
                        lastHeartbeat = now;
                        var stamp = new byte[8];
                        BinaryPrimitives.WriteInt64LittleEndian(stamp, now);
                        try { await SendAsync(Frames.Write(w => BoltCodec.WriteCallSignal(w, Guid.Empty, (SignalType)0x0E, stamp)), stop.Token); }
                        catch (Exception) when (!stop.IsCancellationRequested) { /* The receive side will notice. */ }
                    }
                    if (link.Evaluate(now) == CallLinkState.Reconnecting)
                    {
                        lost ??= "heartbeat silence";
                        socket.Abort();
                        return;
                    }
                }
            });

            var buffer = new byte[128 * 1024];
            try
            {
                while (true)
                {
                    if (events.StallDue(clock) is { } stallFor)
                    {
                        // A frozen app: nothing is read, nothing is sent, nothing is judged.
                        Env.Log($"STALL t={clock.Elapsed.TotalSeconds:F2} for {stallFor}s");
                        frozen = true;
                        await Task.Delay(TimeSpan.FromSeconds(stallFor), ct);
                        frozen = false;
                        events.StallEnded();
                        link.Inbound(Environment.TickCount64);
                    }
                    var message = await Frames.ReceiveAsync(socket, buffer, ct);
                    if (message is null) return null;
                    var now = Environment.TickCount64;
                    link.Inbound(now);
                    foreach (var frame in Frames.Unbatch(message))
                    {
                        if (frame[0] == (byte)FrameType.CallSignal && BoltCodec.TryReadCallSignal(frame, out var signal) &&
                            (byte)signal.SignalType == 0x0E && signal.PayloadLength == 8)
                        { link.Echo(BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(signal.PayloadOffset, 8)), now); continue; }
                        metrics.Add(frame);
                    }
                }
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                return lost ?? $"socket: {error.GetType().Name}";
            }
            finally
            {
                stop.Cancel();
                try { await watch; } catch { /* Stopped. */ }
                socket.Abort();
                socket.Dispose();
            }
        }
    }

    /// <summary>What reached the phone: audio continuity and delay, and pictures a decoder could show.</summary>
    private sealed class Metrics
    {
        /// <summary>Delivered within this long of being sent counts as live, not as backlog draining after a gap.</summary>
        private const long FreshMs = 2_000;
        private readonly List<long> audioDelays = [];
        private readonly List<(long Arrival, long Sent)> audioArrivals = [], decodableArrivals = [];
        private readonly Dictionary<uint, int> pending = [];
        private uint? firstAudio, lastAudio, lastDecodable;
        private long? lastAudioArrival;
        public long AudioReceived, LongestAudioGapMs, PicturesDecodable, Keyframes;

        public void Add(byte[] frame)
        {
            if (frame[0] != (byte)FrameType.MediaFrame || !BoltCodec.TryReadMediaFrame(frame, out var header)) return;
            var payload = header.GetPayload(frame);
            if (payload.Length < Payload.HeaderSize) return;
            var now = Env.NowMs();
            if (payload[8] == Payload.Audio)
            {
                var sent = BinaryPrimitives.ReadInt64LittleEndian(payload);
                audioDelays.Add(now - sent);
                audioArrivals.Add((now, sent));
                AudioReceived++;
                firstAudio ??= header.SequenceNumber;
                lastAudio = header.SequenceNumber;
                if (lastAudioArrival is { } previous) LongestAudioGapMs = Math.Max(LongestAudioGapMs, now - previous);
                lastAudioArrival = now;
                return;
            }
            var picture = BinaryPrimitives.ReadUInt32LittleEndian(payload[10..]);
            var count = BinaryPrimitives.ReadUInt16LittleEndian(payload[16..]);
            if ((pending[picture] = pending.GetValueOrDefault(picture) + 1) < count) return;
            pending.Remove(picture);
            foreach (var stale in pending.Keys.Where(x => x < picture).ToArray()) pending.Remove(stale);
            var isKey = payload[9] == 1;
            if (isKey) Keyframes++;
            if (isKey || lastDecodable == picture - 1)
            { lastDecodable = picture; PicturesDecodable++; decodableArrivals.Add((now, BinaryPrimitives.ReadInt64LittleEndian(payload))); }
        }

        public double AudioDelivered => firstAudio is { } first && lastAudio is { } last && last >= first
            ? Math.Round(100.0 * AudioReceived / (last - first + 1), 1) : 0;
        public object AudioDelay()
        {
            var sorted = audioDelays.Order().ToArray();
            long P(double q) => sorted.Length == 0 ? -1 : sorted[(int)Math.Min(sorted.Length - 1, Math.Floor(sorted.Length * q))];
            return new { p50 = P(.5), p90 = P(.9), p99 = P(.99), max = P(1) };
        }
        /// <summary>When live audio (not the backlog of the gap) was first heard again after <paramref name="unixMs"/>.</summary>
        public long? FirstAudioAfter(long? unixMs) => FirstFresh(audioArrivals, unixMs);
        public long? FirstPictureAfter(long? unixMs) => FirstFresh(decodableArrivals, unixMs);
        private static long? FirstFresh(List<(long Arrival, long Sent)> arrivals, long? unixMs) => unixMs is { } t
            ? arrivals.Where(x => x.Arrival >= t && x.Arrival - x.Sent <= FreshMs).Select(x => (long?)x.Arrival).FirstOrDefault()
            : null;
    }

    /// <summary>The run's one disturbance: a netem outage (timed by run-profile.sh), an IP change, or an app stall.</summary>
    private sealed class Events(int ipChangeAt, int stallAt, int stallFor)
    {
        private bool ipChanged, stalled;
        private long? start, end;
        public string Kind => ipChangeAt >= 0 ? "ip-change" : stallAt >= 0 ? "app-stall" : File.Exists("/tmp/outage-start") ? "outage" : "none";

        public bool IpChangeDue(Stopwatch clock)
        {
            if (ipChangeAt < 0 || ipChanged || clock.Elapsed.TotalSeconds < ipChangeAt) return false;
            ipChanged = true;
            // The new network is there at once; the call's own detection is what the result measures.
            start = end = Env.NowMs();
            return true;
        }

        public int? StallDue(Stopwatch clock)
        {
            if (stallAt < 0 || stalled || clock.Elapsed.TotalSeconds < stallAt) return null;
            stalled = true;
            start = Env.NowMs();
            return stallFor;
        }

        public void StallEnded() => end = Env.NowMs();

        /// <summary>The first resume after the disturbance began, in unix ms.</summary>
        public long? ResumedAtMs { get; private set; }
        public void Resumed(long unixMs) { if (start is { } began ? unixMs >= began : File.Exists("/tmp/outage-start")) ResumedAtMs ??= unixMs; }

        public (long? Start, long? End) Window()
        {
            if (Kind == "outage")
                return (Read("/tmp/outage-start"), Read("/tmp/outage-end"));
            return (start, end);
        }

        private static long? Read(string path) => File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out var value) ? value : null;
    }

    /// <summary>
    /// Where connections come from. Each connection is a new TCP socket (a new source port); after an
    /// IP change, also a new source address. The old connection's port is dropped both ways, so the
    /// relay sees it go silent rather than close.
    /// </summary>
    private sealed class Network
    {
        private IPAddress? bind;
        private IPAddress? alternate;
        public string? LastLocalEndPoint { get; private set; }

        public static Network Create(bool secondAddress)
        {
            var network = new Network();
            if (secondAddress) network.alternate = AddAlternateAddress();
            return network;
        }

        /// <summary>One attempt's API client: a fresh connection, reused for that attempt's ticket and rejoin, as a browser would.</summary>
        public HttpClient Http() => new(Handler()) { Timeout = TimeSpan.FromSeconds(20) };

        public SocketsHttpHandler Handler() => new()
        {
            ConnectCallback = async (context, ct) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    if (bind is { } address) socket.Bind(new IPEndPoint(address, 0));
                    await socket.ConnectAsync(context.DnsEndPoint, ct);
                    LastLocalEndPoint = socket.LocalEndPoint?.ToString();
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };

        /// <summary>The old path dies silently, and whatever connects next comes from the second address.</summary>
        public void Blackhole(string localEndPoint)
        {
            if (IPEndPoint.TryParse(localEndPoint, out var endpoint))
            {
                Run("iptables", $"-I INPUT -p tcp --dport {endpoint.Port} -j DROP");
                Run("iptables", $"-I OUTPUT -p tcp --sport {endpoint.Port} -j DROP");
            }
            bind = alternate;
        }

        private static IPAddress? AddAlternateAddress()
        {
            var output = Run("ip", "-4 -o addr show dev eth0");
            var cidr = output.Split(' ', StringSplitOptions.RemoveEmptyEntries).SkipWhile(x => x != "inet").Skip(1).FirstOrDefault();
            if (cidr is null || !IPNetwork.TryParse(cidr, out _) || cidr.Split('/') is not [var address, var prefix]) return null;
            var bytes = IPAddress.Parse(address).GetAddressBytes();
            bytes[3] = (byte)(bytes[3] == 200 ? 201 : 200);
            var alternate = new IPAddress(bytes);
            Run("ip", $"addr add {alternate}/{prefix} dev eth0");
            Env.Log($"RX second address {alternate}/{prefix}");
            return alternate;
        }

        private static string Run(string file, string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(file, arguments) { RedirectStandardOutput = true, RedirectStandardError = true })!;
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);
                if (process.ExitCode != 0) Env.Log($"RX {file} {arguments} failed: {error.Trim()}");
                return output;
            }
            catch (Exception error) { Env.Log($"RX {file} unavailable: {error.Message}"); return ""; }
        }
    }
}
