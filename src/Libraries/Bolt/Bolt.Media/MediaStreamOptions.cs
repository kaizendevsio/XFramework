namespace Bolt.Media;

public enum MediaMode { Audio, Video }

public record MediaStreamOptions
{
    public MediaMode Mode { get; init; }
    public int JitterBufferMinDelayMs { get; init; }
    public int JitterBufferMaxDelayMs { get; init; }
    public bool FecEnabled { get; init; }
    public int FecGroupSize { get; init; }
    public int TargetThroughputKbps { get; init; }

    public static MediaStreamOptions ForAudio() => new()
    {
        Mode = MediaMode.Audio,
        JitterBufferMinDelayMs = 20,
        JitterBufferMaxDelayMs = 200,
        FecEnabled = true,
        FecGroupSize = 4,
        TargetThroughputKbps = 128,
    };

    public static MediaStreamOptions ForVideo() => new()
    {
        Mode = MediaMode.Video,
        JitterBufferMinDelayMs = 40,
        JitterBufferMaxDelayMs = 300,
        FecEnabled = true,
        FecGroupSize = 8,
        TargetThroughputKbps = 5000,
    };
}
