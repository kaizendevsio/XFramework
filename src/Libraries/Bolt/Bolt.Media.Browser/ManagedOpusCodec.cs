using System.Buffers.Binary;
using Concentus;
using Concentus.Enums;
using Concentus.Structs;

namespace Bolt.Media.Browser;

/// <summary>Portable 48 kHz mono Opus fallback for browsers without WebCodecs audio.</summary>
public sealed class ManagedOpusCodec : IDisposable
{
    private const int SamplesPerFrame = 960;
    private readonly IOpusEncoder _encoder;
    private readonly IOpusDecoder _decoder;

    public ManagedOpusCodec(int bitrateKbps = 64)
    {
        // Select managed implementations explicitly: WASM must never probe native DLLs.
#pragma warning disable CS0618
        _encoder = new OpusEncoder(48_000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        _decoder = new OpusDecoder(48_000, 1);
#pragma warning restore CS0618
        _encoder.Complexity = 3;
        SetBitrate(bitrateKbps);
    }

    public void SetBitrate(int bitrateKbps) => _encoder.Bitrate = Math.Clamp(bitrateKbps, 16, 128) * 1000;

    public byte[] Encode(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length != SamplesPerFrame * sizeof(short))
            throw new ArgumentException("Expected one 20 ms mono PCM frame.", nameof(pcm));
        Span<short> samples = stackalloc short[SamplesPerFrame];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm[(i * 2)..]);
        Span<byte> packet = stackalloc byte[1275];
        var length = _encoder.Encode(samples, SamplesPerFrame, packet, packet.Length);
        return packet[..length].ToArray();
    }

    public byte[] Decode(ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty || packet.Length > 1275)
            throw new ArgumentException("Invalid Opus packet size.", nameof(packet));
        // Browser voice negotiates 20 ms packets only. Never allocate peer-selected durations.
        Span<short> samples = stackalloc short[SamplesPerFrame];
        var count = _decoder.Decode(packet, samples, SamplesPerFrame, false);
        var pcm = new byte[count * sizeof(short)];
        for (var i = 0; i < count; i++)
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), samples[i]);
        return pcm;
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
    }
}
