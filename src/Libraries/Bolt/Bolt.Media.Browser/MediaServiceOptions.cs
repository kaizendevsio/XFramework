namespace Bolt.Media.Browser;

/// <summary>Explicit trust model for the experimental browser media transport.</summary>
public enum MediaSecurityMode
{
    /// <summary>Requires identity-bound end-to-end keys; currently unavailable.</summary>
    EndToEndEncrypted,
    /// <summary>Authenticated WSS to a trusted relay. The relay can access media.</summary>
    AuthenticatedTransport
}

public sealed class MediaServiceOptions
{
    public MediaSecurityMode SecurityMode { get; set; } = MediaSecurityMode.EndToEndEncrypted;
    public int AudioBitrateKbps { get; set; } = 128;
    public int AudioSampleRate { get; set; } = 48_000;
    public int AudioChannels { get; set; } = 1;

    public int VideoWidth { get; set; } = 1280;
    public int VideoHeight { get; set; } = 720;
    public int VideoBitrateKbps { get; set; } = 2_000;
    public int VideoFramerate { get; set; } = 30;
    public string VideoCodec { get; set; } = "h264";
    public int KeyframeIntervalFrames { get; set; } = 60;

    /// <summary>Legacy option retained for source compatibility. SecurityMode is authoritative;
    /// setting this to false cannot opt into transport-only security.</summary>
    public bool EnableEncryption { get; set; } = true;
    public bool EnableFec { get; set; } = true;
    public int FecAudioGroupSize { get; set; } = 4;
    public int FecVideoGroupSize { get; set; } = 8;
}
