using System.Buffers.Binary;
using Concentus;
using Concentus.Structs;

namespace Bolt.Media.Browser;

/// <summary>One receiver's independent 48 kHz mono Opus history.</summary>
public sealed class ManagedOpusDecoder : IDisposable
{
    private const int SamplesPerFrame = 960;
    private readonly IOpusDecoder _decoder;

    public ManagedOpusDecoder()
    {
        // Never probe a native library from WASM.
#pragma warning disable CS0618
        _decoder = new OpusDecoder(48_000, 1);
#pragma warning restore CS0618
    }

    public byte[] Decode(ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty || packet.Length > 1275)
            throw new ArgumentException("Invalid Opus packet size.", nameof(packet));
        Span<short> samples = stackalloc short[SamplesPerFrame];
        var count = _decoder.Decode(packet, samples, SamplesPerFrame, false);
        var pcm = new byte[count * sizeof(short)];
        for (var i = 0; i < count; i++)
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), samples[i]);
        return pcm;
    }

    public void Dispose() => _decoder.Dispose();
}
