namespace Bolt.Media.Browser;

/// <summary>One rung of the send ladder. Bitrates are video only; audio is budgeted separately.</summary>
public readonly record struct VideoTier(int Width, int Height, int Framerate, int BitrateKbps)
{
    public override string ToString() => $"{Height}p{Framerate}@{BitrateKbps}k";
}

/// <summary>What the last observation window measured. Every field is a real measurement, not a guess.</summary>
/// <param name="AllowedKbps">Bitrate the receiver's congestion control is currently allowing.</param>
/// <param name="MeasuredFps">Frames the encoder actually emitted in the window.</param>
/// <param name="EncodeBacklog">Frames queued in front of the encoder: the CPU/thermal signal.</param>
/// <param name="SendBacklog">Encoded frames waiting on the transport.</param>
public readonly record struct VideoConditions(int AllowedKbps, double MeasuredFps, int EncodeBacklog, int SendBacklog);

/// <summary>
/// Sender-side quality control.
///
/// There is no SFU transcoding here, so the sender alone decides the picture every receiver gets.
/// It reacts to three independent pressures - the receiver's allowed bitrate, this device's encode
/// backlog, and dropped sends. Low camera cadence alone does not establish pressure; measured
/// frame rate only gates upward probing. Low-light exposure can reduce cadence without overload.
///
/// Down is fast (two bad windows) and up is slow (twelve good windows), so a brief dip costs a
/// second of sharpness while a genuine recovery is not mistaken for one.
/// </summary>
public sealed class VideoAdaptation
{
    /// <summary>Resolution rungs, worst first. The user preference and measured conditions bound the active tier.</summary>
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

    /// <summary>Observation windows of pressure before a drop, of calm before a climb, and of
    /// starvation before video stands down entirely.</summary>
    public const int DownAfter = 2, UpAfter = 12, SuspendAfter = 5;
    /// <summary>Windows to wait before blindly retrying a suspended picture.</summary>
    public const int ResumeProbeAfter = 15;
    /// <summary>Below this the picture is worthless and the audio needs the room.</summary>
    public const int SuspendKbps = 120;

    private int ceiling = Ladder.Length - 1;
    private int index;
    private int bad, good, starved, suspendedFor;

    private int maxFramerate;
    public VideoAdaptation(int startIndex = 3, int maxFramerate = 30)
    {
        index = Math.Clamp(startIndex, 0, Ladder.Length - 1);
        this.maxFramerate = maxFramerate == 60 ? 60 : 30;
    }
    private VideoTier Tier(int i) => maxFramerate == 60 && Ladder[i].Height >= 720
        ? Ladder[i] with { Framerate = 60, BitrateKbps = Ladder[i].BitrateKbps * 3 / 2 } : Ladder[i];
    public static int IndexForHeight(int height) => Math.Max(0, Array.FindLastIndex(Ladder, x => x.Height <= height));

    /// <summary>Current rung, or null while video is suspended for lack of bandwidth.</summary>
    public VideoTier? Current => Suspended ? null : Tier(index);
    public int Index => index;
    public int Ceiling => ceiling;
    public bool Suspended { get; private set; }

    /// <summary>
    /// Hard cap from what this device and call can sustain: encoder maximum height, the codec's
    /// software ceiling, battery state and the number of streams this call has to decode.
    /// </summary>
    public bool SetCeiling(int maxHeight)
    {
        var limit = 0;
        for (var i = Ladder.Length - 1; i >= 0; i--)
            if (Ladder[i].Height <= maxHeight) { limit = i; break; }
        if (limit == ceiling) return false;
        ceiling = limit;
        if (index <= ceiling) return false;
        index = ceiling; bad = good = 0;
        return true;
    }

    /// <summary>Feed one observation window. Returns the new tier when the picture must change.</summary>
    public VideoTier? Observe(VideoConditions conditions)
    {
        var tier = Tier(index);
        var starving = conditions.AllowedKbps is > 0 and < SuspendKbps;
        starved = starving ? starved + 1 : 0;
        if (Suspended)
        {
            suspendedFor++;
            var healthy = starved == 0 && conditions.AllowedKbps >= Ladder[0].BitrateKbps * 3 / 2;
            // Receiver feedback dries up while nothing is being sent, so waiting for it would strand
            // the picture forever. After a cool-down the smallest rung is retried blind; if the link
            // is still that bad, the next few windows suspend it again at almost no cost.
            if (!healthy && suspendedFor < ResumeProbeAfter) return null;
            Suspended = false; index = 0; bad = good = starved = suspendedFor = 0;
            return Ladder[0];
        }
        if (starved >= SuspendAfter) { Suspended = true; bad = good = 0; suspendedFor = 0; return null; }

        // Slow camera cadence (for example low-light exposure) is not congestion. Require
        // an actual full encoder queue, dropped sends, or current receiver feedback.
        var strained = conditions.EncodeBacklog >= 2 || conditions.SendBacklog >= 8 ||
            (conditions.AllowedKbps > 0 && conditions.AllowedKbps < tier.BitrateKbps * 85 / 100);
        if (strained)
        {
            good = 0;
            if (++bad < DownAfter || index == 0) return null;
            bad = 0;
            if (maxFramerate > 30) { maxFramerate = 30; return Tier(index); }
            index--;
            return Tier(index);
        }

        bad = 0;
        var next = Math.Min(index + 1, ceiling);
        var roomToClimb = next > index && conditions.EncodeBacklog == 0 && conditions.SendBacklog <= 1 &&
            (conditions.MeasuredFps == 0 || conditions.MeasuredFps >= tier.Framerate * 0.85) &&
            conditions.AllowedKbps >= Tier(next).BitrateKbps * 130 / 100;
        if (!roomToClimb) { good = 0; return null; }
        if (++good < UpAfter) return null;
        good = 0; index = next;
        return Tier(index);
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
