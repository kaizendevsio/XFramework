namespace Bolt.Protocol.Transport;

/// <summary>Timing for <see cref="DatagramPathHysteresis"/>.</summary>
public sealed record DatagramHysteresisOptions
{
    /// <summary>After the first time the path goes bad, media stays on the WebSocket at least this long.</summary>
    public int FirstHoldMs { get; init; } = 10_000;
    /// <summary>Each further time doubles the hold, up to this.</summary>
    public int MaxHoldMs { get; init; } = 60_000;
    /// <summary>Once the hold is over, the path must have been healthy for this long without a break before media returns.</summary>
    public int HealthyForMs { get; init; } = 5_000;
    /// <summary>After this many times going bad, the path is not used again (until <see cref="DatagramPathHysteresis.Reset"/>).</summary>
    public int MaxFlaps { get; init; } = 3;
}

/// <summary>
/// Decides whether a datagram path carries media, from its health samples, with hysteresis. A path that goes bad is
/// left at once (media takes the WebSocket); it is used again only after a hold that doubles every time it goes bad,
/// and only after it has been healthy for a sustained stretch; after <see cref="DatagramHysteresisOptions.MaxFlaps"/>
/// it is given up. Every switch costs reordered or lost frames and a keyframe, and a sender's rate control reads the
/// gap as congestion: a path that flaps every few seconds is far worse than the WebSocket it keeps leaving.
/// Thread-safe; times are milliseconds on the caller's monotonic clock.
/// </summary>
public sealed class DatagramPathHysteresis(DatagramHysteresisOptions? options = null)
{
    private readonly object _sync = new();
    private bool _usable;
    private bool _everUsable;
    private long _holdUntil = long.MinValue;
    private long? _healthySince;

    public DatagramHysteresisOptions Options { get; } = options ?? new DatagramHysteresisOptions();

    /// <summary>Whether media goes on the datagram path now.</summary>
    public bool Usable { get { lock (_sync) return _usable; } }

    /// <summary>Times the path went bad while carrying media.</summary>
    public int Flaps { get; private set; }

    /// <summary>The path flapped too often: it stays off until <see cref="Reset"/> (a network change).</summary>
    public bool GivenUp { get { lock (_sync) return Flaps >= Options.MaxFlaps; } }

    /// <summary>Times media moved between the datagram path and the WebSocket (either way).</summary>
    public int Switches { get; private set; }

    /// <summary>When the current hold ends, while one runs.</summary>
    public long HoldUntil { get { lock (_sync) return _holdUntil; } }

    /// <summary>Feed one health sample. Returns true when <see cref="Usable"/> changed.</summary>
    public bool Observe(bool healthy, long nowMs)
    {
        lock (_sync)
        {
            if (_usable)
            {
                if (healthy) return false;
                _usable = false;
                Flaps++;
                Switches++;
                var hold = Math.Min((long)Options.MaxHoldMs, (long)Options.FirstHoldMs << Math.Min(20, Flaps - 1));
                _holdUntil = nowMs + hold;
                _healthySince = null;
                return true;
            }
            if (!healthy) { _healthySince = null; return false; }
            _healthySince ??= nowMs;
            // The first open needs no proof; every return does.
            if (Flaps >= Options.MaxFlaps || nowMs < _holdUntil || (_everUsable && nowMs - _healthySince.Value < Options.HealthyForMs))
                return false;
            _usable = _everUsable = true;
            Switches++;
            return true;
        }
    }

    /// <summary>
    /// A new network: the old path's flaps and holds no longer say anything. A path in use stays in use; one that was
    /// held comes back as soon as it is healthy, like a first open.
    /// </summary>
    public void Reset()
    {
        lock (_sync)
        {
            if (!_usable) _everUsable = false;
            Flaps = 0;
            _holdUntil = long.MinValue;
            _healthySince = null;
        }
    }
}
