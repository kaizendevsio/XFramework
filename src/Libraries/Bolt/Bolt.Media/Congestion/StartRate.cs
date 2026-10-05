namespace Bolt.Media.Congestion;

/// <summary>What is known about a path when its picture starts, from best to weakest evidence.</summary>
/// <param name="UplinkKbps">This device's uplink as its start probe measured it.</param>
/// <param name="UplinkAtLeast">The uplink carried the probe's top rate: its limit is higher.</param>
/// <param name="ReceiversDownlinkKbps">The worst receiver's downlink as that receiver reported it (its own probe or hints).</param>
/// <param name="ReceiversDownlinkAtLeast">That report is a lower bound.</param>
/// <param name="CachedKbps">Where this device's last call on the same kind of network settled (its stable send rate).</param>
/// <param name="NetworkCapKbps">A ceiling from the browser's own network hints (a 3G connection, data saver).</param>
public readonly record struct StartHints(
    int? UplinkKbps = null,
    bool UplinkAtLeast = false,
    int? ReceiversDownlinkKbps = null,
    bool ReceiversDownlinkAtLeast = false,
    int? CachedKbps = null,
    int? NetworkCapKbps = null);

/// <summary>Where a picture starts: the send estimate (audio and video together) and what it was based on.</summary>
public readonly record struct StartEstimate(int TotalKbps, string Source)
{
    public override string ToString() => $"{TotalKbps}k ({Source})";
}

/// <summary>
/// The call's start rate: what the link is known to carry before any media crossed it, so the picture starts at the
/// best size the link holds instead of at the bottom of the ladder. Measured values win over remembered ones, and
/// either over a guess; the sender's uplink and the worst receiver's downlink both bound it; the user's preference
/// bounds the picture later (<see cref="VideoRateLadder.Start"/>).
/// </summary>
public static class StartRate
{
    public const string UplinkProbe = "uplink probe", ReceiverDownlink = "receiver downlink", LastCall = "last call",
        Default = "default", NetworkHint = "network hint";

    /// <summary>A measured limit is started under by this much: room for the keyframe and for the measurement's error.</summary>
    public const double MeasuredHeadroom = 0.85;
    /// <summary>A remembered rate is older evidence: started further under.</summary>
    public const double CachedHeadroom = 0.7;
    /// <summary>
    /// With nothing known: a middle picture (540p at its nominal rate) a mobile link usually carries and a fast one
    /// leaves quickly (the controller's start-up ramp).
    /// </summary>
    public const int UnknownVideoKbps = 800;
    public static StartEstimate Choose(in StartHints hints, int audioWireKbps, int minTotalKbps, int maxTotalKbps)
    {
        // This device's own side: measured, else remembered, else a middle picture. A lower bound counts nearly in full:
        // the link carried that much and more.
        int total;
        string source;
        if (hints.UplinkKbps is > 0 and var up)
        {
            total = (int)Math.Round(hints.UplinkAtLeast ? up * 0.95 : up * MeasuredHeadroom);
            source = UplinkProbe;
        }
        else if (hints.CachedKbps is > 0 and var cached)
        {
            total = (int)Math.Round(cached * CachedHeadroom);
            source = LastCall;
        }
        else
        {
            total = UnknownVideoKbps + audioWireKbps;
            source = Default;
        }
        // The worst receiver's side bounds it only where that receiver found its limit: "at least" says nothing about
        // where the limit is.
        if (hints.ReceiversDownlinkKbps is > 0 and var down && !hints.ReceiversDownlinkAtLeast &&
            (int)Math.Round(down * MeasuredHeadroom) is var downLimit && downLimit < total)
        {
            total = downLimit;
            source = ReceiverDownlink;
        }
        if (hints.NetworkCapKbps is > 0 and var cap && cap < total)
        {
            total = cap;
            source = NetworkHint;
        }
        return new StartEstimate(Math.Clamp(total, minTotalKbps, maxTotalKbps), source);
    }
}
