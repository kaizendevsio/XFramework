using Bolt.Protocol;

namespace Bolt.Media.Browser;

/// <summary>One negotiable video codec, ordered best-compression-first.</summary>
public enum VideoCodec { None = 0, Av1 = 1, Vp9 = 2, H264 = 3, Hevc = 4 }

/// <summary>What one device reported for one codec after probing WebCodecs.</summary>
/// <param name="Codec">The codec family.</param>
/// <param name="Encode">The encoder accepted a configuration at <paramref name="MaxHeight"/>.</param>
/// <param name="Decode">The decoder accepted a configuration at <paramref name="MaxHeight"/>.</param>
/// <param name="Hardware">
/// Power-efficient encoding was reported: Media Capabilities ('webrtc') reported the encoder power efficient at the probed size. This is not proof of GPU use. A WebCodecs
/// preference hint cannot establish this; WebKit ignores it for encoders.
/// </param>
/// <param name="MaxHeight">Tallest probed frame the encoder accepted, 0 when it accepted none.</param>
/// <param name="DecodeHardware">The same signal for the decoder.</param>
public sealed record VideoCodecSupport(VideoCodec Codec, bool Encode, bool Decode, bool Hardware, int MaxHeight, bool DecodeHardware = false);

/// <summary>
/// Per-device codec probing results plus the peer intersection that picks the wire codec.
///
/// The compression order is AV1 &gt; HEVC &gt; VP9 &gt; H.264, but AV1 and VP9 software encoders cannot hold
/// 1080p30 on a phone, so a codec is only preferred over the next one when this device reported
/// hardware encoding for it. Software AV1 is accepted only for the small tiers, where its
/// bitrate advantage is worth more than the CPU it costs.
/// </summary>
public sealed class VideoCodecLadder
{
    /// <summary>Compression order. Earlier entries need fewer bits for the same picture.</summary>
    // HEVC requires power-efficient encode and decode probes; unadvertised peers stay on H.264.
    public static readonly VideoCodec[] Preference = [VideoCodec.Av1, VideoCodec.Hevc, VideoCodec.Vp9, VideoCodec.H264];

    /// <summary>Tallest frame a software encoder of this codec may be asked to produce.</summary>
    internal static int SoftwareCeiling(VideoCodec codec) => codec switch
    {
        VideoCodec.Hevc => 0, // HEVC is offered only with a positive power-efficient hardware signal.
        VideoCodec.Av1 => 360,
        VideoCodec.Vp9 => 540,
        _ => 2160 // H.264 prefers the native hardware encoder; measured pressure lowers the tier.
    };

    private readonly Dictionary<VideoCodec, VideoCodecSupport> support = [];

    public IReadOnlyCollection<VideoCodecSupport> Probed => support.Values;

    public void Record(VideoCodecSupport value) => support[value.Codec] = value;

    public VideoCodecSupport? For(VideoCodec codec) => support.GetValueOrDefault(codec);

    /// <summary>
    /// Codecs this device can decode, in compression order. This is what peers are told.
    ///
    /// Where H.264 decodes in hardware, a codec this device would decode in software is left out: a sender that
    /// picks it saves some bits and costs this device a CPU core for the whole call (an iPhone without a VP9 decoder
    /// runs libvpx in the web process). A device with no hardware H.264 decoder keeps every decoder it has.
    /// </summary>
    public VideoCodec[] Decodable => Preference.Where(codec => support.GetValueOrDefault(codec) is { Decode: true } local &&
        (codec != VideoCodec.Hevc || local.DecodeHardware) &&
        (local.DecodeHardware || codec == VideoCodec.H264 || support.GetValueOrDefault(VideoCodec.H264)?.DecodeHardware != true)).ToArray();

    /// <summary>Maximum safe height for the selected encoder, including software limits.</summary>
    public int EncodingCeiling(VideoCodec codec) => support.GetValueOrDefault(codec) is { Encode: true } local
        ? Math.Min(local.MaxHeight, local.Hardware ? 2160 : SoftwareCeiling(codec)) : 0;

    /// <summary>
    /// Pick the codec to encode with: the first in compression order that this device can encode
    /// at <paramref name="height"/> without a software stall, and that every peer can decode.
    /// A software encoder is never chosen over a hardware one further down the order: on an iPhone VP9 is libvpx
    /// and H.264 is VideoToolbox, and the bits VP9 saves are not worth a phone's CPU for a whole call.
    /// Peers that advertised nothing are treated as H.264-only, which is the universal baseline.
    /// </summary>
    public VideoCodec Negotiate(IEnumerable<VideoCodec[]> peerDecoders, int height)
    {
        var peers = peerDecoders.Select(x => x is { Length: > 0 } ? x : [VideoCodec.H264]).ToArray();
        bool Usable(VideoCodec codec) => support.GetValueOrDefault(codec) is { Encode: true } local && local.MaxHeight >= height &&
            (local.Hardware || height <= SoftwareCeiling(codec)) && peers.All(peer => peer.Contains(codec));
        var candidates = Preference.Where(Usable).ToArray();
        foreach (var codec in candidates)
        {
            if (support[codec].Hardware || !candidates.Any(other => support[other].Hardware)) return codec;
        }
        return VideoCodec.None;
    }

    /// <summary>Wire-format name understood by <c>bolt-media.js</c>.</summary>
    public static string Name(VideoCodec codec) => codec switch
    {
        VideoCodec.Av1 => "av1", VideoCodec.Vp9 => "vp9", VideoCodec.H264 => "h264", VideoCodec.Hevc => "hevc", _ => ""
    };

    public static VideoCodec Parse(string? name) => name switch
    {
        "av1" => VideoCodec.Av1, "vp9" => VideoCodec.Vp9, "h264" => VideoCodec.H264, "hevc" => VideoCodec.Hevc, _ => VideoCodec.None
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
        VideoCodec.Av1 => CodecId.AV1, VideoCodec.Vp9 => CodecId.VP9, VideoCodec.Hevc => CodecId.H265, _ => CodecId.H264
    };

    public static VideoCodec FromCodecId(CodecId codec) => codec switch
    {
        CodecId.AV1 => VideoCodec.Av1, CodecId.VP9 => VideoCodec.Vp9, CodecId.H264 => VideoCodec.H264, CodecId.H265 => VideoCodec.Hevc, _ => VideoCodec.None
    };
}
