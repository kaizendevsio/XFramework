using Bolt.Media.Congestion;

namespace Bolt.Media.Browser;

/// <summary>A picture size, frame rate and encoder bitrate. Bitrates are video only; audio is budgeted separately.</summary>
public readonly record struct VideoTier(int Width, int Height, int Framerate, int BitrateKbps)
{
    public override string ToString() => $"{Height}p{Framerate}@{BitrateKbps}k";
}

/// <summary>
/// The camera's send quality for one call.
///
/// There is no SFU transcoding here, so the sender alone decides the picture every receiver gets. Since
/// phase 1 of call network resilience that decision is continuous: <see cref="SendRateController"/> estimates
/// what the path carries from queue delay and the relay's reports, and <see cref="VideoRateLadder"/> turns the
/// video part of that into an encoder bitrate inside a picture size, switching sizes at bitrate thresholds with
/// hysteresis. The fixed <see cref="Ladder"/> remains the set of sizes calls negotiate and start from.
/// </summary>
public sealed class VideoAdaptation
{
    /// <summary>Negotiable picture sizes, worst first, with their nominal bitrates. The user preference and measured conditions bound the active picture.</summary>
    public static readonly VideoTier[] Ladder =
    [
        new(426, 240, 15, 180),
        new(640, 360, 20, 400),
        new(960, 540, 25, 800),
        new(1280, 720, 30, 1500),
        new(1600, 900, 30, 2400),
        new(1920, 1080, 30, 3800),
        new(2560, 1440, 30, 6500),
        new(3840, 2160, 30, 14000)
    ];

    private readonly VideoRateLadder _rates;
    private bool _allow60;
    private int _ceilingHeight = 2160;

    /// <param name="startIndex">Index into <see cref="Ladder"/> to start on, at its nominal bitrate.</param>
    /// <param name="maxFramerate">60 allows 60 fps where the budget supports it; anything else caps at 30.</param>
    public VideoAdaptation(int startIndex = 0, int maxFramerate = 30)
    {
        var start = Ladder[Math.Clamp(startIndex, 0, Ladder.Length - 1)];
        _allow60 = maxFramerate == 60;
        _rates = new VideoRateLadder(VideoRateLadder.IndexForHeight(start.Height), _allow60);
        _rates.Place(start.BitrateKbps, 0, congested: true);
    }

    public static int IndexForHeight(int height) => Math.Max(0, Array.FindLastIndex(Ladder, x => x.Height <= height));

    /// <summary>The continuous ladder the rate loop drives.</summary>
    public VideoRateLadder Rates => _rates;

    /// <summary>Current picture, or null while video is suspended for lack of bandwidth.</summary>
    public VideoTier? Current => Suspended ? null : ToTier(_rates.Current);
    public int Index => _rates.Index;
    /// <summary>Video stands down for lack of bandwidth; set by the call's rate loop.</summary>
    public bool Suspended { get; set; }

    public static VideoTier ToTier(VideoSetting setting) =>
        new(setting.Rung.Width, setting.Rung.Height, setting.Rung.Framerate, setting.BitrateKbps);

    /// <summary>
    /// Hard cap from what this device and call can sustain: encoder maximum height, the codec's software
    /// ceiling, battery state, the number of streams this call has to decode and the user's preference.
    /// Returns true when the current picture had to shrink.
    /// </summary>
    public bool SetCeiling(int maxHeight)
    {
        _ceilingHeight = maxHeight;
        return _rates.SetCeiling(maxHeight, _allow60);
    }

    /// <summary>The encoder refused 60 fps: stay at 30 from now on.</summary>
    public bool LimitTo30Fps()
    {
        if (!_allow60) return false;
        _allow60 = false;
        return _rates.SetCeiling(_ceilingHeight, false);
    }

    /// <summary>
    /// Ceiling for a call with <paramref name="senders"/> people sending video at once. Every extra
    /// sender is another decode on the weakest phone in the call, so the cap falls as the call grows.
    /// </summary>
    public static int HeightCapForParticipants(int senders) => senders switch
    {
        <= 2 => 2160,
        3 => 720,
        _ => 540
    };

    /// <summary>More than this many people and the call stays audio-only; no phone decodes five live streams.</summary>
    public const int MaxVideoParticipants = 4;
}
