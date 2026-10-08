using System.Buffers;
using Bolt.Client;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging;

namespace Bolt.Media;

/// <summary>Where a call's media is going right now.</summary>
public enum MediaPathKind { WebSocket, Negotiating, Datagram }

/// <summary>A participant's view of its media path, for diagnostics.</summary>
/// <param name="Kind">The pipe media uses now.</param>
/// <param name="Description">"UDP/relay", "TLS/relay", "WebSocket" and the like.</param>
/// <param name="RttMs">The data channel's measured round trip, when known.</param>
/// <param name="Reason">Why media is on the WebSocket, when it is.</param>
/// <param name="AudioRedundancy">Audio leaves with the previous frame alongside (the relay reported loss).</param>
/// <param name="RelayLeg">The relay's own leg to its TURN server ("UDP/relay", "TLS/relay"), as the relay reported it.</param>
public sealed record MediaPathStatus(MediaPathKind Kind, string Description, double? RttMs = null, string? Reason = null, bool AudioRedundancy = false,
    string? RelayLeg = null);

public sealed class MediaTransportClientOptions
{
    /// <summary>Give up on a session whose channel has not opened by then (ICE through TURN takes 1-3 s normally).</summary>
    public TimeSpan OpenTimeout { get; init; } = TimeSpan.FromSeconds(15);
    /// <summary>Backoff after a failed attempt: first, then doubling up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan FirstRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(60);
    /// <summary>Use a path whose local leg rides TCP/TLS to the TURN server (a network that blocks UDP).</summary>
    public bool AllowStreamBasedPath { get; init; } = true;
    public TimeSpan ReportInterval { get; init; } = TimeSpan.FromSeconds(1);
    public int RedundancyOnLossPermille { get; init; } = 15;
    public int RedundancyOffLossPermille { get; init; } = 5;
    public TimeSpan RedundancyHold { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>
    /// An open channel whose buffer has drained nothing for this long is stalled for sending (see
    /// <see cref="DatagramDrainWatch"/>): media takes the WebSocket until it drains again.
    /// </summary>
    public int DrainStallMs { get; init; } = DatagramDrainWatch.DefaultTimeoutMs;
    /// <summary>How media moves between the data channel and the socket (see <see cref="DatagramPathHysteresis"/>).</summary>
    public DatagramHysteresisOptions PathHysteresis { get; init; } = new();
    /// <summary>Redundancy is added only while the channel's own buffer is this short.</summary>
    public long RedundancyBacklogBytes { get; init; } = 8 * 1024;
    /// <summary>
    /// A video frame the relay's transport feedback reports lost on this device's uplink is sent again at once (and again
    /// if that copy is lost too). Allow this much time beyond the measured uplink round trip, bounded by 1.5 s
    /// from the original send (0: never). A fixed 400 ms lifetime expires before feedback on a long mobile path.
    /// </summary>
    public int ResendLostVideoMs { get; init; } = 400;
    /// <summary>Without a media pacer, automatic repairs stand back while the data channel holds this many bytes.</summary>
    public long RepairBacklogBytes { get; init; } = 8 * 1024;
}

/// <summary>
/// The participant's side of the datagram media path (Phase 3). After the call socket is up it asks the relay
/// for a session, offers a WebRTC peer with the short-lived ICE servers the relay minted for it, and trickles
/// candidates, all over the same authenticated socket. Once the data channel opens, <see cref="TrySend"/>
/// takes media frames; until then, and whenever the channel fails, it refuses them and the caller sends on the
/// socket, so a failure only ever costs the datagram path, never the call. It retries with backoff, restarts
/// ICE when the network changes, and moves to a fresh session when the relay renews credentials.
///
/// Frames are already SFrame-encrypted when they get here; the channel's DTLS is an extra hop layer only.
/// </summary>
public sealed class MediaTransportClient : IAsyncDisposable
{
    private readonly BoltClient _client;
    private readonly Func<RtcPeerOptions, CancellationToken, ValueTask<IRtcPeer>> _createPeer;
    private readonly MediaTransportClientOptions _options;
    private readonly ILogger _logger;
    private readonly Func<long> _clock;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Action<BoltConnection, byte[], int> _handler;
    // Signalling is applied strictly in arrival order: a candidate must never overtake the answer it follows.
    private readonly System.Threading.Channels.Channel<(MediaTransportKind Kind, byte[] Payload)> _inbox =
        System.Threading.Channels.Channel.CreateBounded<(MediaTransportKind, byte[])>(
            new System.Threading.Channels.BoundedChannelOptions(128) { SingleReader = true, FullMode = System.Threading.Channels.BoundedChannelFullMode.DropWrite });
    private readonly Task _inboxLoop;

    private Session? _active;
    private Session? _pending;
    private bool _requested;
    private bool _disabled;
    private int _failures;
    private long _retryAt;
    private string? _reason = "starting";
    private int _redundancy;
    private long _lossQuietSince;
    private Task _loop = Task.CompletedTask;
    private bool _disposed;
    private DatagramDrainWatch _drain = new();
    private DatagramPathHysteresis _path = new();
    /// <summary>The path flapped too often: no new session until the network changes.</summary>
    private bool _parked;
    private string? _relayLeg;
    /// <summary>The relay was told the active channel is stalled (and has not been told it is back).</summary>
    private bool _reportedStalled;
    private readonly TransportFeedbackEstimator _feedback = new();
    private readonly List<long> _arrivals = [];
    private int _transportSequence;
    /// <summary>Stamped video messages, by transport sequence, until the relay reports them arrived or lost.</summary>
    private readonly (ushort Sequence, byte[]? Message, long SentAt)[] _resendable = new (ushort, byte[]?, long)[1024];
    private long _resent;

    /// <summary>Video frames sent again because the relay reported them lost on the uplink.</summary>
    public long UplinkResent => Interlocked.Read(ref _resent);
    /// <summary>Optional media pacer: enqueue an unchanged video frame, its first send time, and its repair deadline.</summary>
    public Func<byte[], long, long, bool>? QueueVideoRepair { get; set; }

    private int RepairLifetimeMs => _options.ResendLostVideoMs <= 0 ? 0 :
        (int)Math.Min(1_500, _options.ResendLostVideoMs + Math.Clamp(ActivePeer?.Path?.RttMs ?? 0, 0, 5_000));

    /// <summary>Send a queued repair on the active datagram path, preserving its original age across repeat losses.</summary>
    public bool TrySendVideoRepair(ReadOnlySpan<byte> frame, long firstSentAt)
    {
        if (ActivePeer is not { } peer || RepairLifetimeMs == 0 || _clock() - firstSentAt > RepairLifetimeMs) return false;
        if (!SendMessage(peer, frame, StampsMessages, resendable: true, firstSentAt)) return false;
        Interlocked.Increment(ref _resent);
        return true;
    }

    private sealed class Session(string id, MediaTransportConfig config, long createdAt)
    {
        public string Id { get; } = id;
        public MediaTransportConfig Config { get; } = config;
        public long CreatedAt { get; } = createdAt;
        public IRtcPeer? Peer;
        public bool Opened;
        public bool Closed;
        /// <summary>The ICE restart waiting for its answer, if one is in flight.</summary>
        public TaskCompletionSource? Restart;
        /// <summary>This side's candidates wait for the offer they belong to.</summary>
        public CandidateGate Candidates { get; } = new();
    }

    /// <summary>Longest an ICE restart may wait for the relay's answer before the channel is given up.</summary>
    private static readonly TimeSpan RestartAnswerTimeout = TimeSpan.FromSeconds(10);

    // One ICE restart at a time: a second offer before the first answer would leave the browser applying an
    // answer to an offer it has already replaced. Changes during a restart are folded into one more.
    private bool _restarting;
    private bool _restartAgain;

    public MediaTransportClient(BoltClient client, Func<RtcPeerOptions, CancellationToken, ValueTask<IRtcPeer>> createPeer,
        ILogger logger, MediaTransportClientOptions? options = null, Func<long>? clock = null)
    {
        _client = client;
        _createPeer = createPeer;
        _logger = logger;
        _options = options ?? new MediaTransportClientOptions();
        _drain = new DatagramDrainWatch(_options.DrainStallMs);
        _path = new DatagramPathHysteresis(_options.PathHysteresis);
        _clock = clock ?? (static () => Environment.TickCount64);
        _handler = HandleFrame;
        _client.RegisterFrameHandler(FrameType.MediaTransport, _handler);
        _inboxLoop = Task.Run(ProcessInboxAsync);
    }

    /// <summary>Raised when the path changes (opened, failed, switched, or its route changed).</summary>
    public event Action<MediaPathStatus>? StatusChanged;

    /// <summary>
    /// Media that was on the data channel had to move to the WebSocket because the channel failed, closed or stalled.
    /// Whatever the sender measured meanwhile (a backlog that never drained, reports that never arrived) describes the
    /// dead channel, not the link: the rate control starts this path over.
    /// </summary>
    public event Action? PathLost;

    /// <summary>
    /// Supplies the loss (thousandths) of audio that reached this participant since the last call; the relay
    /// uses it to decide whether to send this participant's audio with redundancy.
    /// </summary>
    public Func<int>? ReceiveLossPermille { get; set; }

    /// <summary>The open data channel, when there is one and it carries media (it has not stalled).</summary>
    public IRtcPeer? ActivePeer { get { lock (_sync) return _drain.Stalled || !_path.Usable ? null : OpenPeerLocked(); } }

    private IRtcPeer? OpenPeerLocked() => _active is { Opened: true, Closed: false, Peer: { State: RtcChannelState.Open } peer } ? peer : null;

    public bool IsDatagramActive => ActivePeer is not null;

    /// <summary>Largest message the active channel takes (less the transport stamp, when there is one); 0 when media goes over the socket.</summary>
    public int MaxMessageBytes => ActivePeer is { } peer ? peer.MaxMessageBytes - (StampsMessages ? TransportSequenceCodec.HeaderSize : 0) : 0;

    /// <summary>
    /// The active path's relay reports when each message arrived (<see cref="MediaTransportFeatures.TransportFeedback"/>):
    /// every message sent on it carries a transport-wide sequence number. An older relay gets them unstamped.
    /// </summary>
    public bool StampsMessages
    {
        get { lock (_sync) return MediaTransportFeatures.Has(_active?.Config, MediaTransportFeatures.TransportFeedback); }
    }

    /// <summary>The uplink as the relay's transport feedback describes it, after each report (see <see cref="TransportFeedbackEstimator"/>).</summary>
    public event Action<TransportSignal>? TransportFeedback;

    private static long NowMicroseconds() =>
        (long)(System.Diagnostics.Stopwatch.GetTimestamp() * (1_000_000.0 / System.Diagnostics.Stopwatch.Frequency));

    /// <summary>Bytes the channel has accepted and not yet sent; part of the sender's transport backlog.</summary>
    public long BufferedAmount => ActivePeer?.BufferedAmount ?? 0;

    /// <summary>Audio leaves with the previous frame alongside: the relay reports loss on this participant's audio.</summary>
    public bool AudioRedundancy => Volatile.Read(ref _redundancy) != 0;

    /// <summary>
    /// The active path's relay resends lost video frames (<see cref="MediaTransportFeatures.Nack"/>). Only then does a
    /// receiver ask: an older relay would refuse a NACK on the data channel.
    /// </summary>
    public bool SupportsNack
    {
        get { lock (_sync) return OpenPeerLocked() is not null && MediaTransportFeatures.Has(_active?.Config, MediaTransportFeatures.Nack); }
    }

    public MediaPathStatus Status
    {
        get
        {
            lock (_sync)
            {
                if (!_drain.Stalled && _path.Usable && OpenPeerLocked() is { } peer)
                    return new(MediaPathKind.Datagram, peer.Path?.Describe() ?? "UDP", peer.Path?.RttMs, AudioRedundancy: AudioRedundancy, RelayLeg: _relayLeg);
                if (_pending is not null || (_active is not null && !_active.Opened))
                    return new(MediaPathKind.Negotiating, "WebSocket", Reason: "negotiating");
                return new(MediaPathKind.WebSocket, "WebSocket", Reason: _reason);
            }
        }
    }

    /// <summary>Ask the relay for a session (once the socket is registered) and keep the path healthy from then on.</summary>
    public void Start()
    {
        lock (_sync)
        {
            if (_disposed || !_loop.IsCompleted) return;
            _retryAt = _clock();
            _loop = Task.Run(() => RunAsync(_lifetime.Token));
        }
    }

    /// <summary>
    /// The device's network changed (online again, a different interface). An open channel restarts ICE in
    /// place; with none, the next attempt starts now instead of waiting out its backoff.
    /// </summary>
    public void NetworkChanged()
    {
        Session? active;
        lock (_sync)
        {
            if (_disposed || _disabled) return;
            // A new network: the old one's flaps say nothing about it.
            _path.Reset();
            _parked = false;
            active = _active is { Opened: true, Closed: false } ? _active : null;
            if (active is null) { _failures = 0; _retryAt = _clock(); return; }
            if (_restarting) { _restartAgain = true; return; }
            _restarting = true;
        }
        _ = RestartIceLoopAsync(active);
    }

    private async Task RestartIceLoopAsync(Session session)
    {
        try
        {
            while (await RestartIceAsync(session))
            {
                lock (_sync)
                {
                    if (!_restartAgain || session.Closed || _disposed) return;
                    _restartAgain = false;
                }
            }
        }
        finally
        {
            lock (_sync) { _restarting = false; _restartAgain = false; }
        }
    }

    /// <summary>
    /// Hand one Bolt frame to the data channel. False when there is none, the frame is too large for one
    /// message, or the channel would not take it: the caller sends that frame on the socket.
    /// Audio with <paramref name="audio"/> set may leave with the previous audio frame of its stream.
    /// </summary>
    public bool TrySend(ReadOnlySpan<byte> frame, bool audio = false)
    {
        var peer = ActivePeer;
        var stamp = StampsMessages;
        if (peer is null || frame.IsEmpty || frame.Length > peer.MaxMessageBytes - (stamp ? TransportSequenceCodec.HeaderSize : 0))
            return false;
        if (audio && frame[0] == (byte)FrameType.MediaFrame && BoltCodec.TryReadMediaFrameHeader(frame, out var streamId))
            return SendAudio(peer, streamId, frame, stamp);
        // Video is the only media a lost message costs more than itself of (the whole picture, and what refers to it).
        return SendMessage(peer, frame, stamp, resendable: !audio && frame[0] == (byte)FrameType.MediaFrame);
    }

    /// <summary>
    /// One message on the channel, stamped when the relay reports arrivals. A refused message used up its number without
    /// being recorded, so the gap it leaves is never counted as loss.
    /// </summary>
    /// <param name="firstSentAt">For a message sent again: when it was first sent (a resend may itself be lost, and is
    /// sent again while the original is still young enough to matter).</param>
    private bool SendMessage(IRtcPeer peer, ReadOnlySpan<byte> message, bool stamp, bool resendable = false, long? firstSentAt = null)
    {
        if (!stamp)
        {
            if (!peer.TrySend(message)) return false;
            _drain.Sent(message.Length);
            return true;
        }
        var size = TransportSequenceCodec.HeaderSize + message.Length;
        if (size > peer.MaxMessageBytes) return false;
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            var sequence = unchecked((ushort)Interlocked.Increment(ref _transportSequence));
            TransportSequenceCodec.Write(buffer, sequence, message);
            // Timed before the send: WebKit's send() can block the page for hundreds of ms when its buffer is full, and a
            // send time taken after that is late. One late send time lowered the delay floor for every later message, so
            // the uplink read a standing 400 ms queue that was not there for the next 10-20 s.
            var sentAt = NowMicroseconds();
            if (!peer.TrySend(buffer.AsSpan(0, size))) return false;
            _feedback.OnSent(sequence, size, sentAt);
            _drain.Sent(size);
            if (resendable && _options.ResendLostVideoMs > 0)
                lock (_resendable) _resendable[sequence % _resendable.Length] = (sequence, message.ToArray(), firstSentAt ?? _clock());
            return true;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    // ── Start probe ──

    private LinkProbe? _probe;
    /// <summary>
    /// A probe step stops sending while the channel holds more than this many milliseconds of its rate (at least 16 KB,
    /// as WebKit reports its buffer coarsely): audio sent meanwhile never waits behind much more than that, and a step
    /// that held back is judged on what it did send.
    /// </summary>
    private const int ProbeBacklogMs = 60;
    /// <summary>A step whose timers ran this late was measured on a busy page: its echo timing says nothing about the downlink.</summary>
    private const int PageBusyLagMs = 40;

    /// <summary>A start probe finished (its padding may still sit in the channel's buffer for a moment).</summary>
    public event Action? ProbeEnded;

    /// <summary>A start probe is running: its padding sits in the channel's buffer and the relay's feedback, so the rate loop holds still.</summary>
    public bool Probing => Volatile.Read(ref _probe) is not null;
    private const int ProbeMessageBytes = 1_100;

    /// <summary>
    /// Measure the link before media needs it (WebRTC-style initial probing, see <see cref="LinkProbe"/>): short bursts of
    /// padding at <paramref name="stepsKbps"/>, each <paramref name="stepMs"/> long, timed by the relay's transport
    /// feedback (uplink) and its echo (downlink). Stops at the first step the uplink does not carry; stops asking for
    /// echoes at the first the downlink does not. Each step holds back while the channel has a backlog, so audio sent
    /// meanwhile never waits behind it. Null when there is no open channel, or its relay does not time arrivals.
    /// </summary>
    public async Task<LinkProbeResult?> ProbeAsync(IReadOnlyList<int> stepsKbps, int stepMs = LinkProbe.DefaultStepMs, CancellationToken ct = default)
    {
        if (ActivePeer is not { } first || !StampsMessages || stepsKbps.Count == 0) return null;
        var probe = new LinkProbe();
        Volatile.Write(ref _probe, probe);
        var started = _clock();
        var steps = new List<ProbeStep>();
        var echo = true;
        var echoFailures = 0;
        var size = Math.Clamp(first.MaxMessageBytes - TransportSequenceCodec.HeaderSize, PaddingCodec.HeaderSize + 64, ProbeMessageBytes);
        try
        {
            foreach (var rate in stepsKbps)
            {
                var step = probe.BeginStep(rate);
                var flags = echo ? PaddingCodec.Echo : (byte)0;
                var begin = NowMicroseconds();
                long sent = 0;
                uint index = 0;
                var sentMessages = 0;
                // How late the page ran this step's timers: a busy page also handles the echoes late, in bursts.
                var lagMs = 0L;
                async Task Pause(int ms)
                {
                    var before = _clock();
                    await Task.Delay(ms, ct);
                    lagMs = Math.Max(lagMs, _clock() - before - ms);
                }
                var backlogLimit = Math.Max(16_384L, rate * ProbeBacklogMs / 8);
                while (!ct.IsCancellationRequested)
                {
                    var elapsedUs = NowMicroseconds() - begin;
                    if (elapsedUs >= stepMs * 1000L) break;
                    // Token bucket: whatever the timer's jitter, the step offers its rate over its length.
                    var owed = rate * elapsedUs / 8_000;
                    while (sent + size <= owed && ActivePeer is { } peer && peer.BufferedAmount <= backlogLimit)
                    {
                        if (!SendPadding(peer, probe, step, index++, flags, size)) break;
                        sent += size;
                        sentMessages++;
                    }
                    await Pause(5);
                }
                // Wait for the relay to report every message of the step (and its echoes), a couple of round trips at most.
                var rtt = ActivePeer?.Path?.RttMs is double measured && measured > 0 ? measured : 100;
                var deadline = _clock() + (long)Math.Clamp(rtt * 2 + 120, 150, 900);
                while (_clock() < deadline && !ct.IsCancellationRequested)
                {
                    var (uplink, echoes) = probe.Pending(step, echo);
                    if (uplink == 0 && echoes == 0) break;
                    await Pause(10);
                }
                var verdict = probe.Judge(step, stamped: true, echo, pageBusy: lagMs > PageBusyLagMs);
                steps.Add(verdict);
                // A step the device's own channel held back ends the probe: higher rates would only be held back more.
                if (sentMessages == 0 || !verdict.UplinkPassed || verdict.HeldBack || verdict.Inconclusive) break;
                // Echoes stop after two failed round trips in a row (the downlink's limit, confirmed; see LinkProbe.Conclude).
                if (echo) echoFailures = verdict.EchoPassed ? 0 : echoFailures + 1;
                if (echoFailures >= 2) echo = false;
            }
        }
        catch (OperationCanceledException) { return null; }
        finally
        {
            Volatile.Write(ref _probe, null);
            ProbeEnded?.Invoke();
        }
        var result = LinkProbe.Conclude(steps, _clock() - started, probe.MinRoundTripMs());
        _logger.LogInformation("Start probe: {Result}", result);
        return result;
    }

    /// <summary>One stamped probe message. False when the channel would not take it.</summary>
    private bool SendPadding(IRtcPeer peer, LinkProbe probe, int step, uint index, byte flags, int size)
    {
        var total = TransportSequenceCodec.HeaderSize + size;
        var buffer = ArrayPool<byte>.Shared.Rent(total);
        try
        {
            PaddingCodec.Write(buffer.AsSpan(TransportSequenceCodec.HeaderSize, size), flags, (byte)step, index);
            var sequence = unchecked((ushort)Interlocked.Increment(ref _transportSequence));
            buffer[0] = (byte)FrameType.TransportSequenced;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1), sequence);
            // Timed before the send: the page's own clock for an echo it times itself starts here too.
            var now = NowMicroseconds();
            if (!peer.TrySend(buffer.AsSpan(0, total))) return false;
            // Not recorded for the media's own estimate: the probe judges its padding itself, and a probe step that builds
            // a queue on purpose must not read as the media's congestion.
            probe.OnSent(step, index, sequence, total, now);
            _drain.Sent(total);
            return true;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    /// <summary>A message from the relay: transport feedback is this client's own, everything else goes to the frame handlers.</summary>
    private void OnPeerMessage(ReadOnlyMemory<byte> data)
    {
        var message = data.Span;
        if (message.IsEmpty) return;
        if (message[0] == (byte)FrameType.Padding)
        {
            // The echo of a start probe message: only the probe reads it.
            if (PaddingCodec.TryRead(message, out var flags, out var step, out var index) && (flags & PaddingCodec.Echoed) != 0)
                Volatile.Read(ref _probe)?.OnEcho(step, index, NowMicroseconds());
            return;
        }
        if (message[0] != (byte)FrameType.TransportFeedback)
        {
            _client.DispatchDatagram(message);
            return;
        }
        TransportSignal? signal;
        List<(byte[] Message, long FirstSentAt)>? resend = null;
        lock (_arrivals)
        {
            if (!TransportFeedbackCodec.TryRead(message, out var first, _arrivals)) return;
            _feedback.OnFeedback(first, _arrivals, _clock());
            Volatile.Read(ref _probe)?.OnFeedback(first, _arrivals);
            signal = _feedback.Signal;
            resend = TakeLost(first, _arrivals);
        }
        // What never reached the relay goes again now: a receiver can only get it from this device, and asking for it
        // (its NACK, forwarded by the relay) would take two more trips across both legs, longer than it waits.
        if (resend is not null && ActivePeer is { } peer)
            foreach (var (lost, firstSentAt) in resend)
                if (QueueVideoRepair is { } queue) queue(lost, firstSentAt, firstSentAt + RepairLifetimeMs);
                else if (peer.BufferedAmount <= _options.RepairBacklogBytes) TrySendVideoRepair(lost, firstSentAt);
        if (signal is { } value)
        {
            try { TransportFeedback?.Invoke(value); }
            catch (Exception ex) { _logger.LogDebug(ex, "A transport feedback listener failed"); }
        }
    }

    /// <summary>Video messages a report says never arrived (and are still worth sending); every reported one is forgotten.</summary>
    private List<(byte[] Message, long FirstSentAt)>? TakeLost(ushort first, List<long> arrivals)
    {
        List<(byte[] Message, long FirstSentAt)>? lost = null;
        var now = _clock();
        var lifetime = RepairLifetimeMs;
        lock (_resendable)
        {
            for (var index = 0; index < arrivals.Count; index++)
            {
                var sequence = unchecked((ushort)(first + index));
                ref var slot = ref _resendable[sequence % _resendable.Length];
                if (slot.Message is null || slot.Sequence != sequence) continue;
                if (arrivals[index] < 0 && lifetime > 0 && now - slot.SentAt <= lifetime) (lost ??= []).Add((slot.Message, slot.SentAt));
                slot = default;
            }
        }
        return lost;
    }

    // The previous audio frame per stream, for redundancy.
    private readonly Dictionary<Guid, byte[]> _lastAudio = new();

    private bool SendAudio(IRtcPeer peer, Guid streamId, ReadOnlySpan<byte> frame, bool stamp)
    {
        byte[]? previous;
        lock (_lastAudio) previous = _lastAudio.GetValueOrDefault(streamId);
        var sent = false;
        if (AudioRedundancy && previous is not null && peer.BufferedAmount <= _options.RedundancyBacklogBytes)
        {
            var size = MediaBundleCodec.Size(previous.Length, frame.Length);
            if (size <= peer.MaxMessageBytes)
            {
                var bundle = ArrayPool<byte>.Shared.Rent(size);
                try
                {
                    MediaBundleCodec.Write(bundle, previous, frame);
                    sent = SendMessage(peer, bundle.AsSpan(0, size), stamp);
                }
                finally { ArrayPool<byte>.Shared.Return(bundle); }
            }
        }
        if (!sent && !SendMessage(peer, frame, stamp)) return false;
        lock (_lastAudio)
        {
            if (_lastAudio.Count >= 8 && !_lastAudio.ContainsKey(streamId)) _lastAudio.Clear();
            _lastAudio[streamId] = frame.ToArray();
        }
        return true;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var nextReport = _clock();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(250, ct);
                var now = _clock();
                Session? expired = null;
                bool request = false;
                lock (_sync)
                {
                    if (_disabled) return;
                    var negotiating = _pending ?? (_active is { Opened: false } ? _active : null);
                    if (negotiating is not null && now - negotiating.CreatedAt > (long)_options.OpenTimeout.TotalMilliseconds)
                        expired = negotiating;
                    else if (negotiating is null && !_requested && !_parked && _active is null && now >= _retryAt && _client.IsConnected)
                    {
                        _requested = true;
                        request = true;
                    }
                }
                if (expired is not null)
                {
                    _logger.LogInformation("The datagram media path did not open in time; media stays on the WebSocket");
                    await FailAsync(expired, "timeout", notifyRelay: true);
                }
                if (request)
                    await SendAsync(MediaTransportKind.Request, new MediaTransportRequest(1, RtcDefaults.MaxMessageBytes));
                await EvaluatePathAsync(now);
                if (now >= nextReport)
                {
                    nextReport = now + (long)_options.ReportInterval.TotalMilliseconds;
                    if (IsDatagramActive && ReceiveLossPermille is { } loss && _active is { } active)
                        await SendAsync(MediaTransportKind.Report, new MediaTransportReport(active.Id, Math.Clamp(loss(), 0, 1000)));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogWarning(ex, "The datagram media path loop ended"); }
    }

    /// <summary>
    /// Whether media goes on the channel. It must be open with ICE connected and keep draining what it is given (a
    /// buffer that drains nothing for <see cref="MediaTransportClientOptions.DrainStallMs"/> means nothing arrives).
    /// Media leaves a bad path at once, comes back only after a hold and sustained health, and after repeated flaps the
    /// session is closed and none is asked for until the network changes (<see cref="DatagramPathHysteresis"/>).
    /// </summary>
    private async Task EvaluatePathAsync(long now)
    {
        IRtcPeer? peer;
        Session? active;
        DatagramDrainWatch drain;
        DatagramPathHysteresis path;
        lock (_sync)
        {
            peer = OpenPeerLocked();
            active = _active;
            drain = _drain;
            path = _path;
        }
        if (peer is not null)
        {
            drain.Observe(peer.BufferedAmount, now);
            // Stuck for good: give this session up so a fresh one (new TURN allocations, new path) is tried on the backoff.
            if (drain.Stalled && drain.StuckForMs(now) >= _options.DrainStallMs * 5 && active is not null)
            {
                await FailAsync(active, "stalled", notifyRelay: true);
                return;
            }
        }
        var healthy = peer is not null && !drain.Stalled;
        if (path.Observe(healthy, now))
        {
            if (path.Usable)
            {
                lock (_sync) _reason = null;
                _logger.LogInformation("Call media is back on the datagram path ({Switches} switches so far)", path.Switches);
            }
            else
            {
                lock (_sync) _reason = path.GivenUp ? "flapping" : "stalled";
                _logger.LogInformation(path.GivenUp
                    ? "The datagram media path flapped {Flaps} times; media stays on the WebSocket until the network changes"
                    : "The datagram media path went bad (flap {Flaps}); media continues on the WebSocket for a while", path.Flaps);
                PathLostNow();
            }
            Raise();
        }
        if (active is { Opened: true, Closed: false }) await ReportStalledAsync(active.Id);
        if (path.GivenUp && active is { Closed: false })
        {
            lock (_sync) _parked = true;
            await FailAsync(active, "flapping", notifyRelay: true);
        }
    }

    /// <summary>
    /// Tell the relay whether this participant's channel carries media, so its media for this participant follows: a
    /// stalled channel is usually dead both ways, and only this side may know. Follows the hysteresis, not each blip.
    /// </summary>
    private async Task ReportStalledAsync(string session)
    {
        bool send, stalled;
        lock (_sync)
        {
            stalled = !_path.Usable;
            send = stalled != _reportedStalled && _active?.Id == session;
            if (send) _reportedStalled = stalled;
        }
        if (send) await SendAsync(MediaTransportKind.State, new MediaTransportStateMessage(session, stalled ? "stalled" : "open"));
    }

    private void PathLostNow()
    {
        try { PathLost?.Invoke(); }
        catch (Exception ex) { _logger.LogDebug(ex, "A media path listener failed"); }
    }

    private void HandleFrame(BoltConnection connection, byte[] buffer, int length)
    {
        if (!MediaTransportCodec.TryRead(buffer.AsSpan(0, length), out var kind, out var payload))
            return;
        _inbox.Writer.TryWrite((kind, payload.ToArray()));
    }

    private async Task ProcessInboxAsync()
    {
        try
        {
            await foreach (var (kind, payload) in _inbox.Reader.ReadAllAsync(_lifetime.Token))
                await HandleAsync(kind, payload);
        }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(MediaTransportKind kind, byte[] payload)
    {
        Session? subject = null;
        try
        {
            switch (kind)
            {
                case MediaTransportKind.Config when MediaTransportCodec.Decode<MediaTransportConfig>(payload) is { } config:
                    await OnConfigAsync(config);
                    break;
                case MediaTransportKind.Answer when MediaTransportCodec.Decode<MediaTransportDescription>(payload) is { } answer:
                    subject = FindSession(answer.Session);
                    if (subject?.Peer is { } peer)
                    {
                        await peer.SetAnswerAsync(answer.Sdp, _lifetime.Token);
                        if (answer.IceRestart) subject.Restart?.TrySetResult();
                    }
                    break;
                case MediaTransportKind.Candidate when MediaTransportCodec.Decode<MediaTransportCandidate>(payload) is { } candidate:
                    subject = FindSession(candidate.Session);
                    if (subject?.Peer is { } target)
                        await target.AddCandidateAsync(new RtcCandidate(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex), _lifetime.Token);
                    break;
                case MediaTransportKind.State when MediaTransportCodec.Decode<MediaTransportStateMessage>(payload) is { State: "open", Path: { } relayPath } open
                                                   && FindSession(open.Session) is not null:
                    // The relay's own leg, for diagnostics: "UDP/relay" is the point of the datagram path; TCP or TLS is not.
                    lock (_sync) _relayLeg = relayPath.Describe();
                    Raise();
                    break;
                case MediaTransportKind.State when MediaTransportCodec.Decode<MediaTransportStateMessage>(payload) is { } state:
                    if (state.State is "failed" or "closed" && FindSession(state.Session) is { } lost)
                        await FailAsync(lost, "relay-" + state.State, notifyRelay: false);
                    break;
                case MediaTransportKind.Close when MediaTransportCodec.Decode<MediaTransportClose>(payload) is { } close:
                    if (FindSession(close.Session) is { } closed)
                        await FailAsync(closed, close.Reason == "superseded" ? null : "relay-closed", notifyRelay: false);
                    break;
                case MediaTransportKind.Report when MediaTransportCodec.Decode<MediaTransportReport>(payload) is { } report:
                    ApplyRelayLoss(Math.Clamp(report.ReceiveLossPermille, 0, 1000));
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Datagram path signalling failed");
            // The session the message was for, even an open one: a restart answer the browser refused leaves the two
            // ends with different ICE credentials, and nothing sent on that channel would arrive. Media takes the
            // socket at once and a fresh session is tried on the usual backoff.
            Session? broken;
            lock (_sync) broken = subject is { Closed: false } ? subject : _pending ?? (_active is { Opened: false } ? _active : null);
            if (broken is not null) await FailAsync(broken, "negotiation", notifyRelay: true);
        }
    }

    private async Task OnConfigAsync(MediaTransportConfig config)
    {
        if (config.Unavailable is { } reason || string.IsNullOrEmpty(config.Session) || config.IceServers.Length == 0)
        {
            lock (_sync)
            {
                _requested = false;
                _reason = config.Unavailable ?? "unavailable";
                if (config.Unavailable == "disabled") _disabled = true;
                else ScheduleRetryLocked(config.Unavailable == "rate-limited" ? TimeSpan.FromSeconds(10) : null);
            }
            Raise();
            return;
        }

        var session = new Session(config.Session, config, _clock());
        Session? replaced;
        lock (_sync)
        {
            if (_disposed) return;
            _requested = false;
            // A config while a channel is open is the relay renewing credentials: negotiate beside it, then switch.
            if (_active is { Opened: true, Closed: false }) { replaced = _pending; _pending = session; }
            else { replaced = _active; _active = session; }
        }
        if (replaced is not null) await CloseSessionAsync(replaced, notifyRelay: false);
        Raise();
        var maxMessage = Math.Clamp(config.MaxMessageBytes, 256, RtcDefaults.MaxMessageBytes);
        var peer = await _createPeer(new RtcPeerOptions(config.IceServers, config.IceTransportPolicy == "relay", maxMessage), _lifetime.Token);
        lock (_sync)
        {
            if (session.Closed || _disposed) { _ = peer.DisposeAsync(); return; }
            session.Peer = peer;
        }
        peer.LocalCandidate += candidate =>
        {
            if (!session.Candidates.TryHold(candidate)) _ = SendCandidateAsync(session, candidate);
        };
        peer.StateChanged += state => _ = OnPeerStateAsync(session, state);
        peer.PathChanged += _ => Raise();
        peer.Message += OnPeerMessage;
        // A peer that times probe echoes where they land (a browser's page) hands them over that way instead.
        if (peer is IRtcProbeEchoSource echoes) echoes.ProbeEcho += (step, index, arrivedUs) => Volatile.Read(ref _probe)?.OnEcho(step, index, arrivedUs);
        session.Candidates.Hold();
        var offer = await peer.CreateOfferAsync(iceRestart: false, _lifetime.Token);
        await SendAsync(MediaTransportKind.Offer, new MediaTransportDescription(session.Id, offer));
        await SendHeldCandidatesAsync(session);
    }

    private Task SendCandidateAsync(Session session, RtcCandidate candidate) => SendAsync(MediaTransportKind.Candidate,
        new MediaTransportCandidate(session.Id, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex));

    /// <summary>The offer is out: candidates gathered while it was made follow it, in order.</summary>
    private async Task SendHeldCandidatesAsync(Session session)
    {
        foreach (var candidate in session.Candidates.Release())
            await SendCandidateAsync(session, candidate);
    }

    private async Task OnPeerStateAsync(Session session, RtcChannelState state)
    {
        if (state == RtcChannelState.Open)
        {
            Session? superseded = null;
            lock (_sync)
            {
                if (session.Closed) return;
                if (!_options.AllowStreamBasedPath && session.Peer?.Path is { IsStreamBased: true })
                {
                    _reason = "stream-based-path";
                }
                else
                {
                    if (!session.Opened && ReferenceEquals(_active, session)) { _drain = new DatagramDrainWatch(_options.DrainStallMs); _reportedStalled = false; }
                    session.Opened = true;
                    if (ReferenceEquals(_pending, session))
                    {
                        superseded = _active;
                        _active = session;
                        _pending = null;
                        _drain = new DatagramDrainWatch(_options.DrainStallMs);
                        _reportedStalled = false;
                    }
                    _failures = 0;
                    _reason = null;
                }
            }
            if (!session.Opened)
            {
                await FailAsync(session, "stream-based-path", notifyRelay: true);
                return;
            }
            _logger.LogInformation("Call media moved to the datagram path ({Path})", session.Peer?.Path?.Describe() ?? "UDP");
            if (superseded is not null) await CloseSessionAsync(superseded, notifyRelay: false);
            await EvaluatePathAsync(_clock());
            Raise();
            return;
        }
        if (state == RtcChannelState.Stalled)
        {
            // The path under an open channel went quiet (or is being rebuilt by an ICE restart). ActivePeer is null
            // until it is back and the hysteresis lets media return; the session is kept, ICE may recover by itself.
            await EvaluatePathAsync(_clock());
            return;
        }
        if (state is RtcChannelState.Failed or RtcChannelState.Closed)
            await FailAsync(session, state == RtcChannelState.Failed ? "ice-failed" : "closed", notifyRelay: true);
    }

    /// <summary>One ICE restart, until its answer is applied. False when the session is gone or the restart failed.</summary>
    private async Task<bool> RestartIceAsync(Session session)
    {
        var answered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            if (session.Peer is not { } peer || session.Closed) return false;
            session.Restart = answered;
            session.Candidates.Hold();
            var offer = await peer.CreateOfferAsync(iceRestart: true, _lifetime.Token);
            await SendAsync(MediaTransportKind.Offer, new MediaTransportDescription(session.Id, offer, IceRestart: true));
            await SendHeldCandidatesAsync(session);
            await answered.Task.WaitAsync(RestartAnswerTimeout, _lifetime.Token);
            return !session.Closed;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ICE restart failed");
            await FailAsync(session, "ice-restart", notifyRelay: true);
            return false;
        }
        finally { Interlocked.CompareExchange(ref session.Restart, null, answered); }
    }

    /// <summary>Drop a session: media falls back to the socket at once, and a new attempt is scheduled.</summary>
    private async Task FailAsync(Session session, string? reason, bool notifyRelay)
    {
        lock (_sync)
        {
            if (session.Closed) return;
            // A renewal that fails while the current channel is open costs nothing: that channel carries on.
            var current = ReferenceEquals(_active, session) || _active is not { Opened: true, Closed: false };
            if (reason is not null && current)
            {
                _reason = reason;
                _failures++;
                ScheduleRetryLocked(null);
            }
        }
        if (reason is not null && session.Opened)
        {
            _logger.LogInformation("The datagram media path closed ({Reason}); media continues on the WebSocket", reason);
            bool carrying;
            lock (_sync) carrying = ReferenceEquals(_active, session);
            if (carrying) PathLostNow();
        }
        // A restart waiting for its answer ends with the session.
        session.Restart?.TrySetResult();
        await CloseSessionAsync(session, notifyRelay, reason ?? "client");
        Raise();
    }

    /// <param name="reason">Why, for the relay's log: "timeout", "ice-failed", "negotiation" and the like.</param>
    private async Task CloseSessionAsync(Session session, bool notifyRelay, string reason = "client")
    {
        IRtcPeer? peer;
        lock (_sync)
        {
            if (session.Closed) return;
            session.Closed = true;
            if (ReferenceEquals(_active, session)) { _active = _pending; _pending = null; }
            if (ReferenceEquals(_pending, session)) _pending = null;
            peer = session.Peer;
            if (_active is null) { Volatile.Write(ref _redundancy, 0); lock (_lastAudio) _lastAudio.Clear(); }
        }
        if (notifyRelay) await SendAsync(MediaTransportKind.Close, new MediaTransportClose(session.Id, reason));
        if (peer is not null)
        {
            try { await peer.DisposeAsync(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Closing the data channel failed"); }
        }
    }

    private void ScheduleRetryLocked(TimeSpan? delay)
    {
        _requested = false;
        var backoff = delay ?? TimeSpan.FromMilliseconds(Math.Min(_options.MaxRetryDelay.TotalMilliseconds,
            _options.FirstRetryDelay.TotalMilliseconds * Math.Pow(2, Math.Max(0, _failures - 1))));
        _retryAt = _clock() + (long)backoff.TotalMilliseconds;
    }

    private void ApplyRelayLoss(int lossPermille)
    {
        var now = _clock();
        if (lossPermille >= _options.RedundancyOnLossPermille)
        {
            _lossQuietSince = 0;
            if (Interlocked.Exchange(ref _redundancy, 1) == 0) Raise();
            return;
        }
        if (!AudioRedundancy) return;
        if (lossPermille >= _options.RedundancyOffLossPermille) { _lossQuietSince = 0; return; }
        if (_lossQuietSince == 0) _lossQuietSince = now;
        else if (now - _lossQuietSince >= (long)_options.RedundancyHold.TotalMilliseconds)
        {
            Volatile.Write(ref _redundancy, 0);
            _lossQuietSince = 0;
            Raise();
        }
    }

    private Session? FindSession(string id)
    {
        lock (_sync)
            return _active?.Id == id ? _active : _pending?.Id == id ? _pending : null;
    }

    private async Task SendAsync<T>(MediaTransportKind kind, T message)
    {
        try { await _client.GetPrimaryConnection().SendAsync(MediaTransportCodec.Encode(kind, message), _lifetime.Token); }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The socket is going away; the call's own reconnect takes it from here.
        }
    }

    private void Raise()
    {
        try { StatusChanged?.Invoke(Status); }
        catch (Exception ex) { _logger.LogDebug(ex, "A media path listener failed"); }
    }

    public async ValueTask DisposeAsync()
    {
        Session? active, pending;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            active = _active;
            pending = _pending;
        }
        _client.UnregisterFrameHandler(FrameType.MediaTransport, _handler);
        _inbox.Writer.TryComplete();
        try { await _lifetime.CancelAsync(); } catch (ObjectDisposedException) { }
        try { await Task.WhenAll(_loop, _inboxLoop).WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* Shutdown. */ }
        if (active is not null) await CloseSessionAsync(active, notifyRelay: false);
        if (pending is not null) await CloseSessionAsync(pending, notifyRelay: false);
        lock (_sync) _active = _pending = null;
        _lifetime.Dispose();
    }
}
