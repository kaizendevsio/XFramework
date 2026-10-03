namespace Bolt.Protocol.Transport;

/// <summary>
/// Whether a data channel still drains what it is given. A channel can stay "open", with ICE content, while nothing
/// handed to it leaves: SCTP gets no acknowledgments (a dead path in one direction, a stuck association), so its
/// buffer only grows. Media sent there is lost, and a sender that counts that buffer as its own backlog stops sending
/// altogether and reads the silence as a link with no capacity. Once the buffer has held bytes without draining any
/// for <see cref="TimeoutMs"/>, the channel is stalled and media takes the WebSocket; it is back as soon as anything
/// drains. Works the same for any implementation's buffered amount: the bytes it was given minus what it still holds.
/// </summary>
public sealed class DatagramDrainWatch(int timeoutMs = DatagramDrainWatch.DefaultTimeoutMs)
{
    /// <summary>
    /// A buffer that has drained nothing for this long is stalled. On a 1 s round trip a working channel drains at
    /// least once a round trip; ICE's own "disconnected" takes about as long but misses a path that is dead one way.
    /// </summary>
    public const int DefaultTimeoutMs = 2_000;

    private long _added;
    private long _lastBuffered;
    private long _lastProgressAt = long.MinValue;
    private int _stalled;

    public int TimeoutMs { get; } = timeoutMs;
    public bool Stalled => Volatile.Read(ref _stalled) != 0;

    /// <summary>How long the buffer has gone without draining anything, as of <paramref name="nowMs"/>.</summary>
    public long StuckForMs(long nowMs) => _lastProgressAt == long.MinValue ? 0 : Math.Max(0, nowMs - _lastProgressAt);

    /// <summary>The channel accepted <paramref name="bytes"/>.</summary>
    public void Sent(int bytes) => Interlocked.Add(ref _added, bytes);

    /// <summary>
    /// Look at the buffer now. Returns true when the stalled state changed (either way). Call it regularly while the
    /// channel is open; a gap between calls only delays the verdict.
    /// </summary>
    public bool Observe(long buffered, long nowMs)
    {
        var added = Interlocked.Exchange(ref _added, 0);
        var drained = _lastBuffered + added - buffered;
        _lastBuffered = buffered;
        if (_lastProgressAt == long.MinValue || buffered <= 0 || drained > 0)
        {
            _lastProgressAt = nowMs;
            return Volatile.Read(ref _stalled) != 0 && Interlocked.Exchange(ref _stalled, 0) != 0;
        }
        return nowMs - _lastProgressAt >= TimeoutMs && Interlocked.Exchange(ref _stalled, 1) == 0;
    }

    /// <summary>A new channel: forget everything.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _added, 0);
        _lastBuffered = 0;
        _lastProgressAt = long.MinValue;
        Volatile.Write(ref _stalled, 0);
    }
}
