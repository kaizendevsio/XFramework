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

    /// <summary>
    /// Also video fragments that carry the picture's orientation (<see cref="VideoFrameFragments"/>, header version 2):
    /// the sender encodes the camera's sensor pixels without redrawing them upright, and the receiver turns the picture
    /// when it paints it. The orientation is inside the SFrame plaintext, so it is as authenticated as the picture.
    /// </summary>
    public const int Oriented = 3;

    /// <summary>What this build announces.</summary>
    public const int Current = Oriented;

    /// <summary>The most every one of <paramref name="peers"/> reads (an unknown or missing value counts as legacy).</summary>
    public static int Common(IEnumerable<int> peers)
    {
        var common = int.MaxValue;
        foreach (var peer in peers) common = Math.Min(common, Math.Clamp(peer, Legacy, Current));
        return common == int.MaxValue ? Legacy : common;
    }

    /// <summary>Every peer reads a picture's orientation from its fragments, so the sender need not redraw it upright.</summary>
    public static bool SendsOrientation(int common) => common >= Oriented;

    /// <summary>
    /// A picture may leave with this orientation code: an upright one always, a turned one only to receivers that read the
    /// code. Anything else was encoded for a roster that has since changed; it is dropped rather than shown on its side.
    /// </summary>
    public static bool CanSend(int common, int orientation) => orientation == 0 || SendsOrientation(common);

    /// <summary>The longest Opus packet every peer can play, in milliseconds.</summary>
    public static int MaxAudioFrameMs(int common) => common >= Compact ? 60 : 20;
}
