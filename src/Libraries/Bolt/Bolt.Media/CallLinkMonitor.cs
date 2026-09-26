namespace Bolt.Media;

// Dependency-free on purpose (BCL only): the call network harness compiles this exact file into its
// simulated phone, so the thresholds it measures are the ones the app ships.

/// <summary>How a call's transport looks from the phone.</summary>
public enum CallLinkState
{
    /// <summary>The relay answers and nothing is late.</summary>
    Connected,
    /// <summary>Still connected, but the relay has gone quiet for a while or answers slowly: "Poor connection".</summary>
    Degraded,
    /// <summary>The transport is gone. The seat is held while the phone resumes on a new connection.</summary>
    Reconnecting,
    /// <summary>The grace period ran out or the server refused the resume. The call is over.</summary>
    Failed
}

/// <summary>Liveness and resume timing. Every duration has an RTT-scaled floor; see <see cref="CallLinkMonitor"/>.</summary>
public sealed record CallLinkOptions
{
    /// <summary>How often a heartbeat is sent while the transport is up.</summary>
    public int HeartbeatIntervalMs { get; init; } = 2000;
    /// <summary>Silence (no frame at all from the relay) after which the call shows "Poor connection".</summary>
    public int DegradedSilenceMs { get; init; } = 3000;
    /// <summary>Silence after which the transport is treated as dead and the phone reconnects.</summary>
    public int LostSilenceMs { get; init; } = 8000;
    /// <summary>A smoothed heartbeat round trip at or above this is also "Poor connection".</summary>
    public int PoorRttMs { get; init; } = 1200;
    /// <summary>
    /// The send rate controller's own verdict (its queuing delay above its high-delay threshold, or video suspended
    /// for bandwidth) must hold this long before it shows as "Poor connection", and be gone this long before it
    /// clears: the controller acts on a spike in a quarter second, a person should not see every spike.
    /// </summary>
    public int SendPathHoldMs { get; init; } = 3000;
    /// <summary>
    /// How long the phone keeps trying to resume. It matches the server's seat hold, so a phone never
    /// gives up on a seat that is still being held for it (or keeps trying one that is gone).
    /// </summary>
    public int GraceMs { get; init; } = 45_000;
    /// <summary>After a network hint (online, network type change, page shown again), how long an answer may take.</summary>
    public int ProbeMs { get; init; } = 3000;
    /// <summary>Waits before successive resume attempts; the last one repeats. Jittered by <see cref="Jitter"/>.</summary>
    public IReadOnlyList<int> BackoffMs { get; init; } = [0, 500, 1000, 2000, 3000, 5000];
    /// <summary>Relative jitter on every backoff wait (0.2 is ±20%), so phones that lost the same cell do not retry in lockstep.</summary>
    public double Jitter { get; init; } = 0.2;
}

/// <summary>
/// Decides, from traffic the phone can observe, whether a call's transport is healthy, slow, or dead.
///
/// Silence only means something once the relay has answered a heartbeat on this connection: a relay
/// without heartbeat support, or a quiet call, is not a dead one. A 1 s RTT link is not "dead" either:
/// every threshold is at least a multiple of the smoothed round trip (degraded at 3×, lost at 6×), so
/// the floors in <see cref="CallLinkOptions"/> only apply to fast links. Times are milliseconds on the
/// caller's monotonic clock (<see cref="Environment.TickCount64"/> in the app), which keeps this
/// deterministic under test.
/// </summary>
public sealed class CallLinkMonitor(CallLinkOptions? options = null)
{
    private readonly Lock _gate = new();
    private long _lastInbound;
    private long? _lostAt, _probeSince, _probeDeadline;
    private double? _srtt;
    private bool _answered;
    private long? _pathPoorSince, _pathFineSince;

    public CallLinkOptions Options { get; } = options ?? new CallLinkOptions();
    public CallLinkState State { get; private set; } = CallLinkState.Connected;

    /// <summary>Smoothed heartbeat round trip (RFC 6298 style, α = 1/8), or null before the first echo.</summary>
    public double? SmoothedRttMs { get { lock (_gate) return _srtt; } }

    /// <summary>Whether the relay on the current connection answers heartbeats, so that silence is evidence.</summary>
    public bool HeartbeatsAnswered { get { lock (_gate) return _answered; } }

    /// <summary>When the transport was lost, while <see cref="CallLinkState.Reconnecting"/>.</summary>
    public long? LostAtMs { get { lock (_gate) return _lostAt; } }

    public int DegradedAfterMs { get { lock (_gate) return DegradedAfterLocked(); } }
    public int LostAfterMs { get { lock (_gate) return LostAfterLocked(); } }

    /// <summary>How long one resume attempt (ticket, socket, rejoin) may take before it is abandoned and retried.</summary>
    public int ResumeAttemptTimeoutMs { get { lock (_gate) return Math.Max(10_000, Scaled(8)); } }

    /// <summary>How long a key epoch may wait for every acknowledgment: three control round trips at the very least.</summary>
    public int EpochConfirmationTimeoutMs { get { lock (_gate) return Math.Max(30_000, Scaled(10)); } }

    /// <summary>A new transport is up (first connect or a resume). The round-trip estimate survives; the evidence does not.</summary>
    public void Connected(long now)
    {
        lock (_gate)
        {
            State = CallLinkState.Connected;
            _lastInbound = now;
            _lostAt = _probeSince = _probeDeadline = null;
            _answered = false;
            // A new transport has a new send path; its controller says for itself whether it is poor.
            _pathPoorSince = _pathFineSince = null;
        }
    }

    /// <summary>
    /// What the send rate controller makes of the path at <paramref name="now"/> (see <see cref="CallLinkOptions.SendPathHoldMs"/>).
    /// It can only make the call "Poor connection": whether the transport is dead is decided by silence alone, so
    /// a congested link the controller is managing is never torn down, and a dead one is never kept for being slow.
    /// </summary>
    public void SendPath(bool poor, long now)
    {
        lock (_gate)
        {
            if (poor) { _pathPoorSince ??= now; _pathFineSince = null; }
            else { _pathFineSince ??= now; _pathPoorSince = null; }
        }
    }

    /// <summary>Whether the send path counts as poor now, with the hold on both edges.</summary>
    public bool SendPathPoor { get { lock (_gate) return PathPoorLocked(long.MaxValue / 2); } }

    /// <summary>Something arrived from the relay at <paramref name="at"/>.</summary>
    public void Inbound(long at)
    {
        lock (_gate) if (at > _lastInbound) _lastInbound = at;
    }

    /// <summary>The relay echoed a heartbeat stamped <paramref name="sentAt"/>.</summary>
    public void Echo(long sentAt, long now)
    {
        lock (_gate)
        {
            if (now > _lastInbound) _lastInbound = now;
            _answered = true;
            var rtt = now - sentAt;
            if (rtt is < 0 or > 120_000) return;
            _srtt = _srtt is { } smoothed ? smoothed + (rtt - smoothed) / 8 : rtt;
        }
    }

    /// <summary>
    /// A network hint: the browser came back online, switched network, or showed the page again. The
    /// socket may or may not have survived, so it gets a short, RTT-scaled chance to prove it did.
    /// </summary>
    public void Probe(long now)
    {
        lock (_gate)
        {
            if (State is not (CallLinkState.Connected or CallLinkState.Degraded)) return;
            _probeSince = now;
            _probeDeadline = now + Math.Max(Options.ProbeMs, Scaled(3));
        }
    }

    /// <summary>
    /// Re-assess the connected link. Returns <see cref="CallLinkState.Reconnecting"/> once, at the moment
    /// silence or a failed probe proves the transport dead; the caller then starts resuming.
    /// </summary>
    public CallLinkState Evaluate(long now)
    {
        lock (_gate)
        {
            if (State is CallLinkState.Reconnecting or CallLinkState.Failed) return State;
            var silence = now - _lastInbound;
            if (_probeSince is { } since && _lastInbound > since) _probeSince = _probeDeadline = null;
            if ((_answered && silence >= LostAfterLocked()) || (_probeDeadline is { } deadline && now >= deadline))
            {
                LoseLocked(now);
                return State;
            }
            var slow = _srtt is { } rtt && rtt >= Options.PoorRttMs;
            var quiet = _answered && silence >= DegradedAfterLocked();
            var congested = PathPoorLocked(now);
            if (State == CallLinkState.Connected && (slow || quiet || congested)) State = CallLinkState.Degraded;
            else if (State == CallLinkState.Degraded && !quiet && !congested && !PathRecoveringLocked(now) &&
                     (_srtt is not { } smoothed || smoothed < Options.PoorRttMs * 0.8))
                State = CallLinkState.Connected;
            return State;
        }
    }

    /// <summary>The transport is gone (socket closed, or <see cref="Evaluate"/> said so). True only on the transition.</summary>
    public bool Lost(long now)
    {
        lock (_gate)
        {
            if (State is CallLinkState.Reconnecting or CallLinkState.Failed) return false;
            LoseLocked(now);
            return true;
        }
    }

    /// <summary>A resume attempt got the call back.</summary>
    public void Resumed(long now) => Connected(now);

    /// <summary>The call is over for good.</summary>
    public void Fail() { lock (_gate) State = CallLinkState.Failed; }

    /// <summary>Milliseconds of grace left while reconnecting; never negative.</summary>
    public long RemainingGraceMs(long now)
    {
        lock (_gate) return _lostAt is { } lost ? Math.Max(0, lost + Options.GraceMs - now) : Options.GraceMs;
    }

    /// <summary>The wait before resume attempt <paramref name="attempt"/> (0-based), jittered by <paramref name="unit"/> in [0, 1).</summary>
    public int BackoffDelayMs(int attempt, double unit)
    {
        var steps = Options.BackoffMs;
        if (steps.Count == 0) return 0;
        var basis = steps[Math.Clamp(attempt, 0, steps.Count - 1)];
        var jittered = basis * (1 + Options.Jitter * (2 * Math.Clamp(unit, 0, 1) - 1));
        return (int)Math.Max(0, Math.Round(jittered));
    }

    private void LoseLocked(long now)
    {
        State = CallLinkState.Reconnecting;
        _lostAt ??= now;
        _probeSince = _probeDeadline = null;
    }

    private bool PathPoorLocked(long now) => _pathPoorSince is { } since && now - since >= Options.SendPathHoldMs;
    /// <summary>The controller's poor verdict cleared less than a hold ago: not yet good enough to clear the notice.</summary>
    private bool PathRecoveringLocked(long now) => _pathFineSince is { } since && now - since < Options.SendPathHoldMs;

    private int Scaled(double multiple) => _srtt is { } rtt ? (int)Math.Min(int.MaxValue, Math.Ceiling(rtt * multiple)) : 0;
    private int DegradedAfterLocked() => Math.Max(Options.DegradedSilenceMs, Scaled(3));
    private int LostAfterLocked() => Math.Max(Math.Max(Options.LostSilenceMs, Scaled(6)), DegradedAfterLocked() + Options.HeartbeatIntervalMs);
}
