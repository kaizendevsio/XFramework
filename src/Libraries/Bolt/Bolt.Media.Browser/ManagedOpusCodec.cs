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
    private readonly ManagedOpusDecoder _decoder;

    public ManagedOpusCodec(int bitrateKbps = 128, OpusEncoderSettings? opus = null)
    {
        // Select managed implementations explicitly: WASM must never probe native DLLs.
#pragma warning disable CS0618
        _encoder = new OpusEncoder(48_000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        _decoder = new ManagedOpusDecoder();
#pragma warning restore CS0618
        _encoder.Complexity = 3;
        if (opus is not null)
        {
            _encoder.UseInbandFEC = opus.InbandFec;
            _encoder.PacketLossPercent = Math.Clamp(opus.PacketLossPercent, 0, 100);
            _encoder.UseDTX = opus.Dtx;
        }
        SetBitrate(bitrateKbps);
    }

    public void SetBitrate(int bitrateKbps) => _encoder.Bitrate = Math.Clamp(bitrateKbps, 16, 128) * 1000;

    public int BitrateKbps => _encoder.Bitrate / 1000;

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

    public byte[] Decode(ReadOnlySpan<byte> packet) => _decoder.Decode(packet);

    private readonly short[] _pending = new short[SamplesPerFrame * 3];
    private int _pendingFrames;
    private double _pendingCapture;
    private int _frameMs = 20;

    /// <summary>
    /// Opus packet length: 20, 40 or 60 ms. Longer packets carry several 20 ms captures each, so the framing below
    /// them is paid less often on a scarce link. A change applies from the next packet; a partly filled one is dropped.
    /// </summary>
    public int FrameMs
    {
        get => _frameMs;
        set
        {
            var frameMs = value is 40 or 60 ? value : 20;
            if (frameMs == _frameMs) return;
            _frameMs = frameMs;
            _pendingFrames = 0;
        }
    }

    /// <summary>
    /// Add one 20 ms capture. Returns a packet once <see cref="FrameMs"/> of audio is in, with the capture time of its
    /// first 20 ms; otherwise false.
    /// </summary>
    public bool TryEncode(ReadOnlySpan<byte> pcm, double captureMicroseconds, out byte[] packet, out double packetCapture)
    {
        if (pcm.Length != SamplesPerFrame * sizeof(short))
            throw new ArgumentException("Expected one 20 ms mono PCM frame.", nameof(pcm));
        if (_pendingFrames == 0) _pendingCapture = captureMicroseconds;
        var target = _pending.AsSpan(_pendingFrames * SamplesPerFrame, SamplesPerFrame);
        for (var i = 0; i < SamplesPerFrame; i++)
            target[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm[(i * 2)..]);
        _pendingFrames++;
        packetCapture = _pendingCapture;
        if (_pendingFrames < _frameMs / 20) { packet = []; return false; }
        var samples = _pendingFrames * SamplesPerFrame;
        _pendingFrames = 0;
        Span<byte> buffer = stackalloc byte[1275];
        var length = _encoder.Encode(_pending.AsSpan(0, samples), samples, buffer, buffer.Length);
        packet = buffer[..length].ToArray();
        return true;
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
    }
}
