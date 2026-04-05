namespace Bolt.Media.Browser;

public sealed class MediaServiceOptions
{
    public int AudioBitrateKbps { get; set; } = 64;
    public int AudioSampleRate { get; set; } = 48_000;
    public int AudioChannels { get; set; } = 1;

    public int VideoWidth { get; set; } = 1280;
    public int VideoHeight { get; set; } = 720;
    public int VideoBitrateKbps { get; set; } = 2_000;
    public int VideoFramerate { get; set; } = 30;
    public string VideoCodec { get; set; } = "h264";
    public int KeyframeIntervalFrames { get; set; } = 60;

    public bool EnableEncryption { get; set; } = true;
    public bool EnableFec { get; set; } = true;
    public int FecAudioGroupSize { get; set; } = 4;
    public int FecVideoGroupSize { get; set; } = 8;
}
