namespace Bolt.Media.Congestion;

/// <summary>One picture size, the frame rate it is sent at, and the video bitrates it is worth sending at.</summary>
/// <param name="MinKbps">Below this the picture is too soft for its size: drop to a smaller one.</param>
/// <param name="MaxKbps">More than this buys nothing visible at this size.</param>
public readonly record struct VideoRung(int Width, int Height, int Framerate, int MinKbps, int MaxKbps)
{
    /// <summary>A budget must reach this, for a while, before the picture grows to this size.</summary>
    public int UpKbps => MinKbps * 5 / 4;
    public override string ToString() => $"{Height}p{Framerate}";
}

/// <summary>The rung to send and the encoder bitrate within it.</summary>
public readonly record struct VideoSetting(VideoRung Rung, int BitrateKbps)
{
    public override string ToString() => $"{Rung}@{BitrateKbps}k";
}

/// <summary>
/// Maps a video budget to a picture: a continuous bitrate inside a rung, and resolution switches at bitrate
/// thresholds with hysteresis. A switch down happens as soon as the budget falls below the rung's minimum (it
/// skips straight to the rung that fits); a switch up needs the next rung's up-threshold to hold for
/// <see cref="UpHoldMs"/>, and never happens while the path is congested. Every size change costs a keyframe,
/// so the gap between the down and up thresholds is what stops the picture flapping.
/// </summary>
public sealed class VideoRateLadder
{
    /// <summary>
    /// Rungs worst first. 60 fps variants are derived (1.5x the bitrate) and only used at 720p or more, on the
    /// top rung the ceiling allows: the user's preference is a ceiling, and 60 fps is spent only on a budget
    /// that already affords the largest picture.
    /// </summary>
    public static readonly VideoRung[] Rungs =
    [
        new(320, 180, 12, 60, 160),
        new(426, 240, 15, 120, 320),
        new(640, 360, 20, 250, 750),
        new(960, 540, 25, 550, 1_400),
        new(1280, 720, 30, 1_000, 2_600),
        new(1600, 900, 30, 1_800, 3_600),
        new(1920, 1080, 30, 2_600, 5_600),
        new(2560, 1440, 30, 4_800, 9_500),
        new(3840, 2160, 30, 9_500, 18_000)
    ];

    public const int UpHoldMs = 2_500;
    /// <summary>
    /// After a size came down, no size goes up for this long; a size that comes down again soon after going up
    /// doubles it (to <see cref="MaxUpBackoffMs"/>). A link that cannot hold a size stops being offered it.
    /// </summary>
    public const int UpBackoffMs = 8_000;
    public const int MaxUpBackoffMs = 64_000;
    /// <summary>A size that lasted less than this before coming down counts as a failed step up.</summary>
    public const int FailedUpWindowMs = 30_000;
    /// <summary>A bitrate change smaller than this fraction is not worth reconfiguring the encoder for.</summary>
    public const double BitrateStep = 0.1;

    private int _index;
    private int _ceiling = Rungs.Length - 1;
    private int _cpuCeiling = Rungs.Length - 1;
    private bool _allow60;
    private bool _at60;
    private long? _upSince;
    private long _upBlockedUntil = long.MinValue / 2;
    private long _lastUpAt = long.MinValue / 2;
    private int _upBackoffMs = UpBackoffMs;
    private int _bitrate;

    /// <param name="startIndex">Rung to start on.</param>
    /// <param name="allow60">The user allows 60 fps; it is still only used when the budget supports it.</param>
    public VideoRateLadder(int startIndex = 1, bool allow60 = false)
    {
        _index = Math.Clamp(startIndex, 0, Rungs.Length - 1);
        _allow60 = allow60;
        _bitrate = Rungs[_index].MinKbps;
    }

    public int Index => _index;
    public int Ceiling => Math.Min(_ceiling, _cpuCeiling);
    public bool Allow60 => _allow60;
    public VideoSetting Current => new(Rung(_index, _at60), _bitrate);

    public static int IndexForHeight(int height) => Math.Max(0, Array.FindLastIndex(Rungs, x => x.Height <= height));

    private static VideoRung Rung(int index, bool sixty) => sixty && Rungs[index].Height >= 720
        ? Rungs[index] with { Framerate = 60, MinKbps = Rungs[index].MinKbps * 3 / 2, MaxKbps = Rungs[index].MaxKbps * 3 / 2 }
        : Rungs[index];

    /// <summary>Tallest picture this device, battery, call size and user preference allow. Returns true when the current rung had to come down.</summary>
    public bool SetCeiling(int maxHeight, bool allow60)
    {
        _ceiling = IndexForHeight(maxHeight);
        _allow60 = allow60;
        var changed = false;
        if (!_allow60 && _at60) { _at60 = false; changed = true; }
        if (_index > Ceiling) { _index = Ceiling; _upSince = null; changed = true; }
        return changed;
    }

    /// <summary>
    /// This device cannot keep up (the encoder queue backs up): cap the picture one step below where it is,
    /// 60 fps going first. <see cref="RelaxCpuCeiling"/> lifts it again later.
    /// </summary>
    public bool ReduceForCpu()
    {
        if (_at60) { _at60 = false; _upSince = null; return true; }
        if (_index == 0) return false;
        _cpuCeiling = _index - 1;
        _index = _cpuCeiling;
        _upSince = null;
        return true;
    }

    public void RelaxCpuCeiling() => _cpuCeiling = Math.Min(Rungs.Length - 1, _cpuCeiling + 1);

    /// <summary>
    /// Place a budget. Returns the new setting when the picture or its bitrate should change, otherwise null.
    /// <paramref name="congested"/> blocks any size increase.
    /// </summary>
    public VideoSetting? Place(int budgetKbps, long nowMs, bool congested)
    {
        var ceiling = Ceiling;
        var index = Math.Min(_index, ceiling);
        var sixty = _at60 && _allow60;

        // Down: straight to the largest rung the budget still fits, 60 fps first.
        if (budgetKbps < Rung(index, sixty).MinKbps)
        {
            if (sixty && budgetKbps >= Rung(index, false).MinKbps) sixty = false;
            else
            {
                sixty = false;
                while (index > 0 && budgetKbps < Rungs[index].MinKbps) index--;
            }
            _upSince = null;
        }
        else if (!congested && nowMs >= _upBlockedUntil)
        {
            // Up one step at a time, and only on a budget that has held.
            var next = NextUp(index, sixty, ceiling);
            if (next is { } up && budgetKbps >= Rung(up.Index, up.Sixty).UpKbps)
            {
                _upSince ??= nowMs;
                if (nowMs - _upSince.Value >= UpHoldMs)
                {
                    (index, sixty) = up;
                    _upSince = null;
                    _lastUpAt = nowMs;
                }
            }
            else _upSince = null;
        }
        else _upSince = null;

        var rung = Rung(index, sixty);
        var bitrate = Math.Clamp(budgetKbps, rung.MinKbps, rung.MaxKbps);
        var changedRung = index != _index || sixty != _at60;
        if (changedRung && (index < _index || (index == _index && !sixty)))
        {
            _upBackoffMs = nowMs - _lastUpAt <= FailedUpWindowMs ? Math.Min(_upBackoffMs * 2, MaxUpBackoffMs) : UpBackoffMs;
            _upBlockedUntil = nowMs + _upBackoffMs;
        }
        // Cuts apply promptly (the queue is draining on them); raises wait for a worthwhile step.
        var changedRate = bitrate > _bitrate
            ? bitrate - _bitrate >= Math.Max(16, _bitrate * BitrateStep)
            : _bitrate - bitrate >= Math.Max(8, _bitrate * BitrateStep / 2);
        _index = index;
        _at60 = sixty;
        if (!changedRung && !changedRate) return null;
        _bitrate = bitrate;
        return Current;
    }

    private (int Index, bool Sixty)? NextUp(int index, bool sixty, int ceiling)
    {
        // Size first, up to the ceiling; 60 fps only on top of that, at 720p or more, with half as much again.
        if (index < ceiling) return (index + 1, false);
        if (!sixty && _allow60 && Rungs[index].Height >= 720) return (index, true);
        return null;
    }
}
