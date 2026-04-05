using System.Buffers.Binary;
using Bolt.Protocol;

namespace Bolt.Media;

/// <summary>
/// Codec capability negotiation for Bolt media calls.
///
/// During call setup, both sides exchange their supported codecs via the
/// CallSignal Initiate/Answer payload extension. The agreed codec set is
/// the intersection of both sides' capabilities.
///
/// Payload format (appended to Initiate/Answer signal):
/// [1:capabilityCount] [capabilityCount * CodecCapability]
///
/// CodecCapability: [1:mediaType] [1:codecId] [4:maxBitrateKbps] [2:flags]
///   flags: bit 0 = encode, bit 1 = decode, bit 2 = hardware accelerated
///
/// After negotiation, the caller picks from the agreed set.
/// </summary>
public sealed class CodecNegotiator
{
    private readonly List<CodecCapability> _localCapabilities = [];
    private List<CodecCapability>? _remoteCapabilities;
    private List<CodecCapability>? _agreedCapabilities;

    /// <summary>The capabilities agreed upon by both sides (intersection).</summary>
    public IReadOnlyList<CodecCapability>? AgreedCapabilities => _agreedCapabilities;

    /// <summary>True if negotiation is complete.</summary>
    public bool IsNegotiated => _agreedCapabilities != null;

    /// <summary>
    /// Add a local codec capability.
    /// </summary>
    public void AddCapability(MediaType mediaType, CodecId codecId, int maxBitrateKbps,
        bool canEncode = true, bool canDecode = true, bool hardwareAccelerated = false)
    {
        byte flags = 0;
        if (canEncode) flags |= 0x01;
        if (canDecode) flags |= 0x02;
        if (hardwareAccelerated) flags |= 0x04;

        _localCapabilities.Add(new CodecCapability
        {
            MediaType = mediaType,
            CodecId = codecId,
            MaxBitrateKbps = maxBitrateKbps,
            Flags = flags,
        });
    }

    /// <summary>
    /// Add default capabilities (Opus audio, H.264/H.265 video).
    /// </summary>
    public void AddDefaultCapabilities()
    {
        AddCapability(MediaType.Audio, CodecId.Opus, 256);
        AddCapability(MediaType.Video, CodecId.H264, 5000);
        AddCapability(MediaType.Video, CodecId.H265, 5000);
        AddCapability(MediaType.Video, CodecId.VP9, 5000);
    }

    /// <summary>
    /// Serialize local capabilities into a payload for CallSignal.
    /// </summary>
    public byte[] SerializeCapabilities()
    {
        var size = 1 + _localCapabilities.Count * CodecCapability.WireSize;
        var payload = new byte[size];
        payload[0] = (byte)_localCapabilities.Count;

        for (int i = 0; i < _localCapabilities.Count; i++)
        {
            var offset = 1 + i * CodecCapability.WireSize;
            _localCapabilities[i].WriteTo(payload.AsSpan(offset));
        }

        return payload;
    }

    /// <summary>
    /// Process remote capabilities from a received CallSignal payload.
    /// Computes the agreed codec set (intersection).
    /// </summary>
    public void ProcessRemoteCapabilities(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 1) return;

        var count = payload[0];
        _remoteCapabilities = new List<CodecCapability>(count);

        for (int i = 0; i < count && 1 + (i + 1) * CodecCapability.WireSize <= payload.Length; i++)
        {
            var offset = 1 + i * CodecCapability.WireSize;
            _remoteCapabilities.Add(CodecCapability.ReadFrom(payload.Slice(offset)));
        }

        // Compute intersection: both sides must support the codec
        _agreedCapabilities = [];
        foreach (var local in _localCapabilities)
        {
            foreach (var remote in _remoteCapabilities)
            {
                if (local.MediaType == remote.MediaType && local.CodecId == remote.CodecId)
                {
                    _agreedCapabilities.Add(new CodecCapability
                    {
                        MediaType = local.MediaType,
                        CodecId = local.CodecId,
                        MaxBitrateKbps = Math.Min(local.MaxBitrateKbps, remote.MaxBitrateKbps),
                        Flags = (byte)(local.Flags & remote.Flags), // Both must support encode/decode
                    });
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Get the best agreed codec for a given media type.
    /// Preference: hardware-accelerated > highest bitrate.
    /// </summary>
    public CodecCapability? GetPreferredCodec(MediaType mediaType)
    {
        if (_agreedCapabilities == null) return null;

        CodecCapability? best = null;
        foreach (var cap in _agreedCapabilities)
        {
            if (cap.MediaType != mediaType) continue;
            if (best == null
                || (cap.IsHardwareAccelerated && !best.Value.IsHardwareAccelerated)
                || cap.MaxBitrateKbps > best.Value.MaxBitrateKbps)
            {
                best = cap;
            }
        }
        return best;
    }
}

/// <summary>
/// A codec capability entry describing what a peer can encode/decode.
/// </summary>
public struct CodecCapability
{
    public const int WireSize = 8; // 1 + 1 + 4 + 2

    public MediaType MediaType;
    public CodecId CodecId;
    public int MaxBitrateKbps;
    public byte Flags;

    public bool CanEncode => (Flags & 0x01) != 0;
    public bool CanDecode => (Flags & 0x02) != 0;
    public bool IsHardwareAccelerated => (Flags & 0x04) != 0;

    public void WriteTo(Span<byte> dest)
    {
        dest[0] = (byte)MediaType;
        dest[1] = (byte)CodecId;
        BinaryPrimitives.WriteInt32LittleEndian(dest[2..], MaxBitrateKbps);
        BinaryPrimitives.WriteUInt16LittleEndian(dest[6..], Flags);
    }

    public static CodecCapability ReadFrom(ReadOnlySpan<byte> src)
    {
        return new CodecCapability
        {
            MediaType = (MediaType)src[0],
            CodecId = (CodecId)src[1],
            MaxBitrateKbps = BinaryPrimitives.ReadInt32LittleEndian(src[2..]),
            Flags = (byte)BinaryPrimitives.ReadUInt16LittleEndian(src[6..]),
        };
    }
}
