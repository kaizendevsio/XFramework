using System.Buffers.Binary;
using System.Diagnostics;

namespace Yap.Native;

public sealed record VideoSettings(int Fps = 30, int Bitrate = 6_000_000, bool Rear = false)
{
    public const int Width = 1920, Height = 1080;
    public void Validate()
    {
        if (Fps is not (30 or 60) || Bitrate is < 1_000_000 or > 12_000_000)
            throw new ArgumentException("Choose 1080p at 30 or 60 FPS and 1–12 Mbps.");
    }
}

// H.264 Annex B, including SPS/PPS on every IDR. Dimensions are sensor dimensions;
// rotation describes clockwise display orientation, never a CPU pixel conversion.
public sealed record VideoPicture(byte[] Data, long TimestampUs, bool Keyframe, int Rotation);

public interface INativeVideo : IDisposable
{
    event Action<VideoPicture>? Encoded;
    event Action<string>? Failed;
    VideoCounters Counters { get; }
    Task StartAsync(VideoSettings settings);
    void Decode(VideoPicture picture);
    void RequestKeyframe();
    DeviceState DeviceState();
}

public sealed record DeviceState(string Thermal, double? BatteryPercent, double? BatteryTemperatureC,
    string Encoder, string Decoder, bool? HardwareEncoder, bool? HardwareDecoder);

public sealed class VideoCounters
{
    public long Captured, Encoded, Received, Output, QueueDrops, BytesSent, BytesReceived;
    private long lastCapture, lastOutput, captureGap, outputGap;
    public void Capture()
    {
        Interlocked.Increment(ref Captured);
        Gap(ref lastCapture, ref captureGap);
    }
    public void Presented()
    {
        Interlocked.Increment(ref Output);
        Gap(ref lastOutput, ref outputGap);
    }
    private static void Gap(ref long previous, ref long maximum)
    {
        var now = Stopwatch.GetTimestamp();
        var last = Interlocked.Exchange(ref previous, now);
        if (last == 0) return;
        var gap = now - last;
        long seen;
        do { seen = Volatile.Read(ref maximum); if (seen >= gap) return; }
        while (Interlocked.CompareExchange(ref maximum, gap, seen) != seen);
    }
    public double CaptureGapMs() => Interlocked.Exchange(ref captureGap, 0) * 1000d / Stopwatch.Frequency;
    public double OutputGapMs() => Interlocked.Exchange(ref outputGap, 0) * 1000d / Stopwatch.Frequency;
}

public static class PictureWire
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static byte[] Pack(VideoPicture picture)
    {
        if (picture.Data.Length is < 1 or > MaxBytes || picture.Rotation is not (0 or 90 or 180 or 270))
            throw new InvalidDataException("Invalid picture.");
        var bytes = new byte[picture.Data.Length + 12];
        bytes[0] = 1; bytes[1] = picture.Keyframe ? (byte)1 : (byte)0;
        bytes[2] = (byte)(picture.Rotation / 90);
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(4), picture.TimestampUs);
        picture.Data.CopyTo(bytes, 12);
        return bytes;
    }
    public static VideoPicture Unpack(byte[] bytes)
    {
        if (bytes.Length is < 13 or > MaxBytes + 12 || bytes[0] != 1 || bytes[1] > 1 || bytes[2] > 3 || bytes[3] != 0)
            throw new InvalidDataException("Unsupported picture envelope.");
        return new(bytes[12..], BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(4)), bytes[1] == 1, bytes[2] * 90);
    }
    public static async Task WriteAsync(Stream stream, byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length is < 1 or > MaxBytes + 12) throw new InvalidDataException("Frame exceeds limit.");
        var header = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(header, bytes.Length);
        await stream.WriteAsync(header, ct); await stream.WriteAsync(bytes, ct);
    }
    public static async Task<byte[]> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 1 or > MaxBytes + 12) throw new InvalidDataException("Frame exceeds limit.");
        var bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, ct); return bytes;
    }
}
