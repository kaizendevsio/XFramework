namespace Bolt.Media.Congestion;

/// <summary>
/// How long each Opus packet is. Every packet pays the same framing below the media: on a datagram path about
/// 140 bytes (IP 20, UDP 8, TURN 4, DTLS about 29, SCTP 28, Bolt 30, SFrame about 20), so 20 ms packets cost about
/// 55 kbps of framing next to 24-32 kbps of voice, and 60 ms packets about 18 kbps.
///
/// Packets stay at 20 ms while the link has room: latency comes first, and a lost 60 ms packet is a longer gap.
/// They grow to <see cref="LongFrameMs"/> (never past <see cref="MaxFrameMs"/>, what every receiver plays) when the
/// link is scarce: video stood down for bandwidth, or the rate controller found the link congesting below
/// <see cref="LongBelowKbps"/> within the last <see cref="ScarceMemoryMs"/> (its congestion point, not its estimate: an
/// audio-only call on a good link sends little and never learns a limit, and must not pay latency for it; and a
/// controller probing above the last congestion point has not found more room). Scarcity must hold for
/// <see cref="LongAfterMs"/>; the way back waits <see cref="ShortAfterMs"/> after scarcity ends, so a wobbling estimate
/// does not reconfigure the encoder each time.
/// </summary>
public sealed class AudioPacketization
{
    public const int ShortFrameMs = 20;
    public int LongFrameMs { get; init; } = 60;
    public int LongBelowKbps { get; init; } = 600;
    public int LongAfterMs { get; init; } = 1_000;
    public int ShortAfterMs { get; init; } = 5_000;
    /// <summary>A congestion point below <see cref="LongBelowKbps"/> keeps the link scarce for this long.</summary>
    public int ScarceMemoryMs { get; init; } = 30_000;

    private int _maxFrameMs = ShortFrameMs;
    private long? _scarceSince, _roomySince, _congestedAt;

    /// <summary>The longest packet every receiver plays (see CallMediaFormat), and that this encoder accepted.</summary>
    public int MaxFrameMs
    {
        get => _maxFrameMs;
        set => _maxFrameMs = Math.Clamp(value, ShortFrameMs, 120) / ShortFrameMs * ShortFrameMs;
    }

    /// <summary>The packet length asked of the encoder last.</summary>
    public int FrameMs { get; private set; } = ShortFrameMs;

    /// <summary>One rate-loop tick. Returns the new packet length when it changes, otherwise null.</summary>
    /// <param name="congestionKbps">Where the controller last found the link congested; 0 when it knows no limit.</param>
    /// <param name="videoSuspended">Video stood down for bandwidth.</param>
    public int? Update(int congestionKbps, bool videoSuspended, long nowMs)
    {
        var longest = Math.Min(LongFrameMs, MaxFrameMs);
        if (FrameMs > longest) return Set(longest);
        if (congestionKbps > 0 && congestionKbps < LongBelowKbps) _congestedAt = nowMs;
        var scarce = videoSuspended || (_congestedAt is { } congested && nowMs - congested < ScarceMemoryMs);
        if (scarce) { _roomySince = null; _scarceSince ??= nowMs; }
        else { _scarceSince = null; _roomySince ??= nowMs; }
        if (FrameMs < longest && scarce && (videoSuspended || nowMs - _scarceSince!.Value >= LongAfterMs))
            return Set(longest);
        if (FrameMs > ShortFrameMs && !scarce && nowMs - _roomySince!.Value >= ShortAfterMs)
            return Set(ShortFrameMs);
        return null;
    }

    /// <summary>
    /// What the encoder actually took. A browser that refuses a packet length keeps the one it has, and that length
    /// becomes the ceiling, so it is not asked again on every tick.
    /// </summary>
    public void Applied(int frameMs)
    {
        if (frameMs < FrameMs) MaxFrameMs = Math.Max(ShortFrameMs, frameMs);
        FrameMs = Math.Max(ShortFrameMs, frameMs);
    }

    private int Set(int frameMs)
    {
        FrameMs = frameMs;
        return frameMs;
    }
}
