namespace Bolt.Media.Browser;

/// <summary>
/// What a call member reads, as it says in its authenticated epoch key envelope. A sender only uses a format every
/// remote member of the epoch announced, so a call that mixes old and new clients keeps working; an epoch is
/// renegotiated whenever the roster changes. Receiving always accepts every format this build knows.
/// </summary>
public static class CallMediaFormat
{
    /// <summary>
    /// Legacy SFrame frames (the context inside the ciphertext, about 278 bytes a frame) and 20 ms Opus packets only.
    /// An envelope without the field (an older client) means this.
    /// </summary>
    public const int Legacy = 1;

    /// <summary>
    /// Also compact SFrame frames (the context authenticated but never sent, about 20 bytes a frame) and Opus packets
    /// of up to 60 ms (several 20 ms frames in one packet).
    /// </summary>
    public const int Compact = 2;

    /// <summary>What this build announces.</summary>
    public const int Current = Compact;

    /// <summary>The most every one of <paramref name="peers"/> reads (an unknown or missing value counts as legacy).</summary>
    public static int Common(IEnumerable<int> peers)
    {
        var common = int.MaxValue;
        foreach (var peer in peers) common = Math.Min(common, Math.Clamp(peer, Legacy, Current));
        return common == int.MaxValue ? Legacy : common;
    }

    /// <summary>The longest Opus packet every peer can play, in milliseconds.</summary>
    public static int MaxAudioFrameMs(int common) => common >= Compact ? 60 : 20;
}
