namespace Bolt.Media.Browser;

/// <summary>Explicit trust model for the experimental browser media transport.</summary>
public enum MediaSecurityMode
{
    /// <summary>Requires identity-bound end-to-end keys; currently unavailable.</summary>
    EndToEndEncrypted,
    /// <summary>Authenticated WSS to a trusted relay. The relay can access media.</summary>
    AuthenticatedTransport,
    /// <summary>RFC9605 frames; keys and epochs must be provisioned through authenticated envelopes.</summary>
    AuthenticatedSFrame
}

public sealed class MediaServiceOptions
{
    public MediaSecurityMode SecurityMode { get; set; } = MediaSecurityMode.EndToEndEncrypted;
    /// <summary>
    /// Opus voice bitrate. 32 kbps is full-quality wideband speech and leaves a 512 kbps mobile link
    /// room for video; 128 kbps cost a third of that link on its own.
    /// </summary>
    public int AudioBitrateKbps { get; set; } = 32;
    /// <summary>Ask Opus for in-band FEC where the encoder supports it (WebCodecs <c>useinbandfec</c>).</summary>
    public bool AudioInbandFec { get; set; } = true;
    /// <summary>Expected loss the FEC is tuned for, in percent (WebCodecs <c>packetlossperc</c>).</summary>
    public int AudioPacketLossPercent { get; set; } = 5;
    /// <summary>Discontinuous transmission: silence costs (almost) nothing on the wire.</summary>
    public bool AudioDtx { get; set; } = true;
    public int AudioSampleRate { get; set; } = 48_000;
    public int AudioChannels { get; set; } = 1;

    /// <summary>Tallest picture this build will ever ask an encoder for. Devices cap themselves below it.</summary>
    public int VideoMaxHeight { get; set; } = 2160;
    /// <summary>
    /// The lowest rung a resumed path restarts from (240p15, which fits a mobile link). A new picture starts where the
    /// start probe and hints put it (see <see cref="StartProbe"/>); the user's preference is its ceiling.
    /// </summary>
    public int VideoStartTier { get; set; } = 0;
    /// <summary>
    /// Measure the link when the data channel opens (while the call rings or connects): a second of paced padding through
    /// the relay at rising rates, timed by its transport feedback and echo (see Bolt.Media.Congestion.LinkProbe). The
    /// camera then starts at the picture the link and the worst receiver carry, not at the bottom of the ladder.
    /// </summary>
    public bool StartProbe { get; set; } = true;
    /// <summary>Rates the start probe offers, lowest first; it stops at the first the link does not carry.</summary>
    public int[] StartProbeStepsKbps { get; set; } = [.. Bolt.Media.Congestion.LinkProbe.DefaultStepsKbps];
    /// <summary>
    /// How long a camera turned on while the data channel still opens, or its start probe still runs, waits for the
    /// measurement before starting on hints (a callee turning the camera on as it connects; a caller's probe ran while
    /// the call rang).
    /// </summary>
    public int StartProbeWaitMs { get; set; } = 2_000;
    /// <summary>The most a call sends, audio and video together, however much the path would carry.</summary>
    public int MaxSendKbps { get; set; } = 40_000;
    /// <summary>
    /// Safety interval between unrequested keyframes. Keyframes are otherwise sent on demand: a new
    /// receiver, a decoder reset or a relay that dropped pictures asks for one.
    /// </summary>
    public int KeyframeIntervalSeconds { get; set; } = 10;
    /// <summary>How often the send rate loop reads its signals and moves the encoders.</summary>
    public int AdaptationIntervalMs { get; set; } = 250;
    /// <summary>
    /// Encode temporal layers (L1T2/L1T3) where the browser's encoder accepts them, so the relay can shed
    /// enhancement pictures for one slow receiver instead of every picture until a keyframe.
    /// </summary>
    public bool TemporalLayers { get; set; } = true;
    /// <summary>Let the rate loop move Opus between its low, normal and high rates. Off keeps <see cref="AudioBitrateKbps"/>.</summary>
    public bool AdaptiveAudioBitrate { get; set; } = true;

    /// <summary>
    /// Move call media onto a WebRTC data channel (UDP through TURN) when the relay offers one, keeping the
    /// WebSocket for signalling and as the fallback. Off keeps every call on the WebSocket.
    /// </summary>
    public bool DatagramTransport { get; set; } = true;

    /// <summary>Legacy option retained for source compatibility. SecurityMode is authoritative;
    /// setting this to false cannot opt into transport-only security.</summary>
    public bool EnableEncryption { get; set; } = true;
    public bool EnableFec { get; set; } = true;
    public int FecAudioGroupSize { get; set; } = 4;
    public int FecVideoGroupSize { get; set; } = 8;
}
