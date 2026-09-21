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
    public int AudioBitrateKbps { get; set; } = 128;
    public int AudioSampleRate { get; set; } = 48_000;
    public int AudioChannels { get; set; } = 1;

    /// <summary>Tallest picture this build will ever ask an encoder for. Devices cap themselves below it.</summary>
    public int VideoMaxHeight { get; set; } = 2160;
    /// <summary>Default to 1080p30; measured pressure lowers quality automatically.</summary>
    public int VideoStartTier { get; set; } = 5;
    /// <summary>Seconds between forced keyframes. Short enough for a late joiner, long enough not to flood.</summary>
    public int KeyframeIntervalSeconds { get; set; } = 2;
    /// <summary>How often the send ladder looks at measured conditions.</summary>
    public int AdaptationIntervalMs { get; set; } = 1_000;

    /// <summary>Legacy option retained for source compatibility. SecurityMode is authoritative;
    /// setting this to false cannot opt into transport-only security.</summary>
    public bool EnableEncryption { get; set; } = true;
    public bool EnableFec { get; set; } = true;
    public int FecAudioGroupSize { get; set; } = 4;
    public int FecVideoGroupSize { get; set; } = 8;
}
