#if HARNESS_ADAPTIVE
// Phase 3 in the harness (UDP=1): the receiver's datagram media path. It does what the browser's
// MediaTransportClient does over the call socket - asks the relay for a session, offers a WebRTC peer with the
// TURN credentials the relay minted, trickles candidates - but its peer is the same bolt-rtc sidecar (Pion) the
// relay uses, in the offering role, since a container has no browser. Media from the channel joins the media
// from the socket; a channel that fails or never opens leaves the call on the socket, as on a phone.
using System.Diagnostics;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Rtc;

internal sealed class HarnessDatagram : IAsyncDisposable
{
    private static readonly Lazy<RtcSidecar> Sidecar = new(() => new RtcSidecar(new RtcSidecarOptions { ExecutablePath = "/app/bolt-rtc" }));
    private readonly Func<byte[], Task> _sendOnSocket;
    private readonly Action<byte[]> _deliver;
    private readonly Stopwatch _clock;
    private readonly SequenceWindow _audio = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IRtcPeer? _peer;
    private string? _session;
    private long _requestedAt;
    private int _failures;
    private long _retryAt;
    private Task _loop = Task.CompletedTask;

    public HarnessDatagram(Func<byte[], Task> sendOnSocket, Action<byte[]> deliver, Stopwatch clock)
    {
        _sendOnSocket = sendOnSocket;
        _deliver = deliver;
        _clock = clock;
    }

    public static bool Enabled => Env.Int("UDP", 0) == 1;

    public bool IsOpen => _peer is { State: RtcChannelState.Open };
    public string Path => _peer is { State: RtcChannelState.Open } peer ? peer.Path?.Describe() ?? "UDP" : "WebSocket";
    public double? OpenedAtS { get; private set; }
    /// <summary>The path media last took on the channel (the summary is written after the call ended and closed it).</summary>
    public string? LastOpenPath { get; private set; }
    public long DatagramFrames, SocketFrames, Duplicates, IceRestarts, Failures, Opens;
    public readonly List<string> Timeline = [];

    public void Start() => _loop = Task.Run(RunAsync);

    private void Note(string text)
    {
        var line = $"{_clock.Elapsed.TotalSeconds:F2}s {text}";
        lock (Timeline) if (Timeline.Count < 40) Timeline.Add(line);
        Env.Log($"UDP {line}");
    }

    /// <summary>Request a session now and again after each failure (5 s, doubling, up to 60 s), report loss once a second.</summary>
    private async Task RunAsync()
    {
        var nextReport = 0L;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(250, _stop.Token);
                var now = Environment.TickCount64;
                if (_peer is null && _session is null && now >= _retryAt)
                {
                    _session = "";
                    _requestedAt = now;
                    await _sendOnSocket(MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, RtcDefaults.MaxMessageBytes)));
                }
                if (_peer is { State: not RtcChannelState.Open } pending && now - _requestedAt > 15_000)
                    await FailAsync(pending, "timeout");
                if (IsOpen && now >= nextReport && _session is { Length: > 0 } session)
                {
                    nextReport = now + 1000;
                    var (expected, received) = _audio.Sample();
                    await _sendOnSocket(MediaTransportCodec.Encode(MediaTransportKind.Report, new MediaTransportReport(session, LossMath.Permille(expected, received))));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Env.Log($"UDP loop ended: {error.Message}"); }
    }

    /// <summary>One MediaTransport frame from the relay (arrived on the socket).</summary>
    public async Task HandleAsync(byte[] frame)
    {
        if (!MediaTransportCodec.TryRead(frame, out var kind, out var payloadSpan)) return;
        var payload = payloadSpan.ToArray();
        await _gate.WaitAsync();
        try
        {
            switch (kind)
            {
                case MediaTransportKind.Config when MediaTransportCodec.Decode<MediaTransportConfig>(payload) is { } config:
                    if (config.Unavailable is { } reason || config.IceServers.Length == 0)
                    {
                        Note($"relay offers no datagram path ({config.Unavailable})");
                        _session = null;
                        _retryAt = Environment.TickCount64 + 60_000;
                        return;
                    }
                    if (_peer is not null) await _peer.DisposeAsync();
                    _session = config.Session;
                    var peer = _peer = await Sidecar.Value.CreateAsync(RtcPeerRole.Offer,
                        new RtcPeerOptions(config.IceServers, config.IceTransportPolicy == "relay", config.MaxMessageBytes), _stop.Token);
                    peer.LocalCandidate += candidate => _ = _sendOnSocket(MediaTransportCodec.Encode(MediaTransportKind.Candidate,
                        new MediaTransportCandidate(config.Session, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex)));
                    peer.StateChanged += state => _ = OnStateAsync(peer, state);
                    peer.PathChanged += path =>
                    {
                        LastOpenPath = path.Describe();
                        Note($"path {path.Describe()} rtt={path.RttMs:F0}ms");
                    };
                    peer.Message += data => OnMessage(data.Span);
                    var offer = await peer.CreateOfferAsync(false, _stop.Token);
                    await _sendOnSocket(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(config.Session, offer)));
                    break;
                case MediaTransportKind.Answer when MediaTransportCodec.Decode<MediaTransportDescription>(payload) is { } answer:
                    if (answer.Session == _session && _peer is { } offered) await offered.SetAnswerAsync(answer.Sdp, _stop.Token);
                    break;
                case MediaTransportKind.Candidate when MediaTransportCodec.Decode<MediaTransportCandidate>(payload) is { } candidate:
                    if (candidate.Session == _session && _peer is { } target)
                        await target.AddCandidateAsync(new RtcCandidate(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex), _stop.Token);
                    break;
                case MediaTransportKind.State when MediaTransportCodec.Decode<MediaTransportStateMessage>(payload) is { } state:
                    if (state.Session == _session && state.State is "failed" or "closed" && _peer is { } lost)
                        _ = FailAsync(lost, "relay-" + state.State);
                    break;
            }
        }
        catch (Exception error) when (!_stop.IsCancellationRequested)
        {
            Note($"signalling failed: {error.GetType().Name}");
        }
        finally { _gate.Release(); }
    }

    private async Task OnStateAsync(IRtcPeer peer, RtcChannelState state)
    {
        if (!ReferenceEquals(peer, _peer)) return;
        if (state == RtcChannelState.Open)
        {
            Opens++;
            OpenedAtS ??= Math.Round(_clock.Elapsed.TotalSeconds, 2);
            _failures = 0;
            LastOpenPath = peer.Path?.Describe() ?? "UDP";
            Note($"open via {LastOpenPath}");
            return;
        }
        if (state is RtcChannelState.Failed or RtcChannelState.Closed) await FailAsync(peer, state.ToString().ToLowerInvariant());
    }

    private async Task FailAsync(IRtcPeer peer, string reason)
    {
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _peer, null, peer), peer)) return;
        Failures++;
        _failures++;
        var session = _session;
        _session = null;
        _retryAt = Environment.TickCount64 + (long)Math.Min(60_000, 5_000 * Math.Pow(2, _failures - 1));
        Note($"closed ({reason}); media on the WebSocket");
        if (session is { Length: > 0 })
            try { await _sendOnSocket(MediaTransportCodec.Encode(MediaTransportKind.Close, new MediaTransportClose(session, "client"))); } catch { }
        await peer.DisposeAsync();
    }

    /// <summary>The device moved to a new network: restart ICE on the open channel (as the browser does on a network change).</summary>
    public async Task NetworkChangedAsync()
    {
        if (_peer is not { State: RtcChannelState.Open } peer || _session is not { Length: > 0 } session) { _retryAt = 0; return; }
        IceRestarts++;
        Note("network changed: ICE restart");
        var offer = await peer.CreateOfferAsync(true, _stop.Token);
        await _sendOnSocket(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(session, offer, IceRestart: true)));
    }

    private void OnMessage(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty || !DatagramFramePolicy.AcceptFromRelay((FrameType)message[0])) return;
        if ((FrameType)message[0] == FrameType.MediaBundle)
        {
            Span<Range> frames = stackalloc Range[MediaBundleCodec.MaxFrames];
            if (!MediaBundleCodec.TryRead(message, frames, out var count)) return;
            for (var index = 0; index < count; index++) Accept(message[frames[index]].ToArray(), datagram: true);
            return;
        }
        Accept(message.ToArray(), datagram: true);
    }

    /// <summary>Every media frame, from either path: second copies of audio are dropped, as the client does.</summary>
    public void Accept(byte[] frame, bool datagram)
    {
        if (frame[0] == (byte)FrameType.MediaFrame && BoltCodec.TryReadMediaFrame(frame, out var header) &&
            header.GetPayload(frame) is { Length: >= Payload.HeaderSize } body && body[8] == Payload.Audio &&
            !_audio.TryMark(header.SequenceNumber))
        {
            Interlocked.Increment(ref Duplicates);
            return;
        }
        if (datagram) Interlocked.Increment(ref DatagramFrames);
        else if (frame[0] == (byte)FrameType.MediaFrame) Interlocked.Increment(ref SocketFrames);
        _deliver(frame);
    }

    public object Summary() => new
    {
        path = LastOpenPath ?? "WebSocket",
        openAtEnd = IsOpen,
        openedAtS = OpenedAtS,
        opens = Opens,
        failures = Failures,
        iceRestarts = IceRestarts,
        datagramFrames = DatagramFrames,
        socketMediaFrames = SocketFrames,
        duplicatesDropped = Duplicates,
        timeline = string.Join(" | ", Timeline),
    };

    /// <summary>Blackhole every UDP flow the sidecar has open (its TURN allocations): the old network is gone.</summary>
    /// <summary>Blackhole every UDP flow the sidecar has open; returns the rules, for <see cref="RestoreUdp"/>.</summary>
    public static List<string> BlackholeUdp()
    {
        var rules = new List<string>();
        try
        {
            using var list = Process.Start(new ProcessStartInfo("ss", "-uanpH") { RedirectStandardOutput = true })!;
            var output = list.StandardOutput.ReadToEnd();
            list.WaitForExit(5000);
            foreach (var line in output.Split('\n').Where(x => x.Contains("bolt-rtc")))
            {
                var local = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(3);
                if (local is null || local.LastIndexOf(':') is var colon && colon < 0) continue;
                var port = local[(colon + 1)..];
                foreach (var rule in new[] { $"OUTPUT -p udp --sport {port} -j DROP", $"INPUT -p udp --dport {port} -j DROP" })
                {
                    Process.Start("iptables", "-I " + rule)?.WaitForExit(5000);
                    rules.Add(rule);
                }
                Env.Log($"UDP blackholed local port {port}");
            }
        }
        catch (Exception error) { Env.Log($"UDP blackhole failed: {error.Message}"); }
        return rules;
    }

    /// <summary>Lift what <see cref="BlackholeUdp"/> put in place: the same flows work again.</summary>
    public static void RestoreUdp(List<string> rules)
    {
        foreach (var rule in rules)
            try { Process.Start("iptables", "-D " + rule)?.WaitForExit(5000); }
            catch (Exception error) { Env.Log($"UDP restore failed: {error.Message}"); }
        if (rules.Count > 0) Env.Log("UDP restored");
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _loop; } catch { }
        if (_peer is { } peer) await peer.DisposeAsync();
    }
}

/// <summary>The relay's TURN credentials in the harness: coturn's shared-secret (TURN REST) scheme, like Yap's generic TURN option.</summary>
internal sealed class HarnessIce : Bolt.Server.IBoltIceServerSource
{
    public static bool Configured => Env.Text("TURN_URL", "") != "" && Env.Text("TURN_SECRET", "") != "";

    public ValueTask<Bolt.Server.BoltIceGrant?> GrantAsync(System.Security.Claims.ClaimsPrincipal participant, CancellationToken ct)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        RtcIceServer[] Servers(string label)
        {
            var username = $"{expires.ToUnixTimeSeconds()}:{label}";
            var credential = Convert.ToBase64String(System.Security.Cryptography.HMACSHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes(Env.Text("TURN_SECRET", "")), System.Text.Encoding.UTF8.GetBytes(username)));
            return [new RtcIceServer([Env.Text("TURN_URL", "")], username, credential)];
        }
        return ValueTask.FromResult<Bolt.Server.BoltIceGrant?>(new Bolt.Server.BoltIceGrant(Servers("phone"), Servers("relay"), expires));
    }
}
#endif
