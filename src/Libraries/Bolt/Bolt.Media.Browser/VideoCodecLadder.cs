using Bolt.Protocol;

namespace Bolt.Media.Browser;

/// <summary>One negotiable video codec, ordered best-compression-first.</summary>
public enum VideoCodec { None = 0, Av1 = 1, Vp9 = 2, H264 = 3 }

/// <summary>What one device reported for one codec after probing WebCodecs.</summary>
/// <param name="Codec">The codec family.</param>
/// <param name="Encode">The encoder accepted a configuration at <paramref name="MaxHeight"/>.</param>
/// <param name="Decode">The decoder accepted a configuration at <paramref name="MaxHeight"/>.</param>
/// <param name="Hardware">Whether hardware encoding is known. A WebCodecs preference hint alone cannot establish this.</param>
/// <param name="MaxHeight">Tallest probed frame the encoder accepted, 0 when it accepted none.</param>
public sealed record VideoCodecSupport(VideoCodec Codec, bool Encode, bool Decode, bool Hardware, int MaxHeight);

/// <summary>
/// Per-device codec probing results plus the peer intersection that picks the wire codec.
///
/// The compression order is AV1 &gt; VP9 &gt; H.264, but AV1 and VP9 software encoders cannot hold
/// 1080p30 on a phone, so a codec is only preferred over the next one when this device reported
/// hardware encoding for it. Software AV1 is accepted only for the small tiers, where its
/// bitrate advantage is worth more than the CPU it costs.
/// </summary>
public sealed class VideoCodecLadder
{
    /// <summary>Compression order. Earlier entries need fewer bits for the same picture.</summary>
    public static readonly VideoCodec[] Preference = [VideoCodec.Av1, VideoCodec.Vp9, VideoCodec.H264];

    /// <summary>Tallest frame a software encoder of this codec may be asked to produce.</summary>
    internal static int SoftwareCeiling(VideoCodec codec) => codec switch
    {
        VideoCodec.Av1 => 360,
        VideoCodec.Vp9 => 540,
        _ => 2160 // H.264 prefers the native hardware encoder; measured pressure lowers the tier.
    };

    private readonly Dictionary<VideoCodec, VideoCodecSupport> support = [];

    public IReadOnlyCollection<VideoCodecSupport> Probed => support.Values;

    public void Record(VideoCodecSupport value) => support[value.Codec] = value;

    public VideoCodecSupport? For(VideoCodec codec) => support.GetValueOrDefault(codec);

    /// <summary>Codecs this device can decode, in compression order. This is what peers are told.</summary>
    public VideoCodec[] Decodable => Preference.Where(codec => support.GetValueOrDefault(codec)?.Decode == true).ToArray();

    /// <summary>Maximum safe height for the selected encoder, including software limits.</summary>
    public int EncodingCeiling(VideoCodec codec) => support.GetValueOrDefault(codec) is { Encode: true } local
        ? Math.Min(local.MaxHeight, local.Hardware ? 2160 : SoftwareCeiling(codec)) : 0;

    /// <summary>
    /// Pick the codec to encode with: the first in compression order that this device can encode
    /// at <paramref name="height"/> without a software stall, and that every peer can decode.
    /// Peers that advertised nothing are treated as H.264-only, which is the universal baseline.
    /// </summary>
    public VideoCodec Negotiate(IEnumerable<VideoCodec[]> peerDecoders, int height)
    {
        var peers = peerDecoders.Select(x => x is { Length: > 0 } ? x : [VideoCodec.H264]).ToArray();
        foreach (var codec in Preference)
        {
            if (support.GetValueOrDefault(codec) is not { Encode: true } local || local.MaxHeight < height) continue;
            if (!local.Hardware && height > SoftwareCeiling(codec)) continue;
            if (peers.All(peer => peer.Contains(codec))) return codec;
        }
        return VideoCodec.None;
    }

    /// <summary>Wire-format name understood by <c>bolt-media.js</c>.</summary>
    public static string Name(VideoCodec codec) => codec switch
    {
        VideoCodec.Av1 => "av1", VideoCodec.Vp9 => "vp9", VideoCodec.H264 => "h264", _ => ""
    };

    public static VideoCodec Parse(string? name) => name switch
    {
        "av1" => VideoCodec.Av1, "vp9" => VideoCodec.Vp9, "h264" => VideoCodec.H264, _ => VideoCodec.None
    };

    /// <summary>Compact advertisement carried inside the end-to-end encrypted epoch control envelope.</summary>
    public static string Advertise(IEnumerable<VideoCodec> codecs) => string.Join(',', codecs.Select(Name).Where(x => x.Length != 0));

    /// <summary>Reads a peer advertisement. Unknown names are ignored rather than failing the call.</summary>
    public static VideoCodec[] ReadAdvertisement(string? value) => string.IsNullOrEmpty(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Parse).Where(x => x != VideoCodec.None).Distinct().ToArray();

    public static CodecId ToCodecId(VideoCodec codec) => codec switch
    {
        VideoCodec.Av1 => CodecId.AV1, VideoCodec.Vp9 => CodecId.VP9, _ => CodecId.H264
    };

    public static VideoCodec FromCodecId(CodecId codec) => codec switch
    {
        CodecId.AV1 => VideoCodec.Av1, CodecId.VP9 => VideoCodec.Vp9, CodecId.H264 => VideoCodec.H264, _ => VideoCodec.None
    };
}
