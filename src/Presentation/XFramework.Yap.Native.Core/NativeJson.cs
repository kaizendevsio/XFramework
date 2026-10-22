using System.Text.Json.Serialization;

namespace Yap.Native;

public sealed record NativeNotice(string Kind, string? Text = null, string? Invitation = null);
public sealed record NativeSample
{
    public string Kind { get; init; } = "yap-native-video-v1";
    public Guid Run { get; init; }
    public DateTimeOffset Time { get; init; }
    public double Seconds { get; init; }
    public VideoSettings Settings { get; init; } = new();
    public int Width { get; init; } = VideoSettings.Width;
    public int Height { get; init; } = VideoSettings.Height;
    public string Codec { get; init; } = "H264";
    public string Transport { get; init; } = "paired-lan-tls-tcp";
    public bool Audio { get; init; }
    public double CaptureFps { get; init; }
    public double EncodedFps { get; init; }
    public double ReceivedFps { get; init; }
    public double OutputFps { get; init; }
    public double CaptureMaxGapMs { get; init; }
    public double OutputMaxGapMs { get; init; }
    public double SentKbps { get; init; }
    public double ReceivedKbps { get; init; }
    public int Queue { get; init; }
    public long QueueDrops { get; init; }
    public double? CpuCorePercent { get; init; }
    public long? MemoryBytes { get; init; }
    public DeviceState? Device { get; init; }
    public string OutputMeaning { get; init; } = "native output callback/submission; not measured physical screen presentation";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(NativeCommand))]
[JsonSerializable(typeof(NativeNotice))]
[JsonSerializable(typeof(NativeSample))]
[JsonSerializable(typeof(PairingInvitation))]
[JsonSerializable(typeof(string))]
public partial class NativeJson : JsonSerializerContext;
