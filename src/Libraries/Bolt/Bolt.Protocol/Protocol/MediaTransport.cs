using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bolt.Protocol.Transport;

namespace Bolt.Protocol;

/// <summary>What a <see cref="FrameType.MediaTransport"/> frame carries.</summary>
public enum MediaTransportKind : byte
{
    /// <summary>Participant to relay: set up a datagram path for this connection (<see cref="MediaTransportRequest"/>).</summary>
    Request = 0x01,
    /// <summary>Relay to participant: ICE servers and limits for one session, or why there is none (<see cref="MediaTransportConfig"/>).</summary>
    Config = 0x02,
    /// <summary>Participant to relay: an SDP offer, initial or an ICE restart (<see cref="MediaTransportDescription"/>).</summary>
    Offer = 0x03,
    /// <summary>Relay to participant: the SDP answer (<see cref="MediaTransportDescription"/>).</summary>
    Answer = 0x04,
    /// <summary>Both ways: one trickled ICE candidate; an empty candidate ends gathering (<see cref="MediaTransportCandidate"/>).</summary>
    Candidate = 0x05,
    /// <summary>Relay to participant: the relay's side of the path opened, failed or closed (<see cref="MediaTransportStateMessage"/>).</summary>
    State = 0x06,
    /// <summary>Both ways, about once a second: loss seen on what arrived over the session (<see cref="MediaTransportReport"/>).</summary>
    Report = 0x07,
    /// <summary>Both ways: the session is over (<see cref="MediaTransportClose"/>).</summary>
    Close = 0x08,
}

/// <summary>A participant asks for a datagram path. <paramref name="MaxMessageBytes"/> is the largest message it will send.</summary>
public sealed record MediaTransportRequest(int Version, int MaxMessageBytes);

/// <summary>
/// The relay's answer to a request. When <paramref name="Unavailable"/> is set there is no session (no TURN
/// credentials, disabled, or too many attempts) and the participant stays on the stream transport.
/// <paramref name="IceServers"/> are short-lived credentials minted for this participant alone.
/// <paramref name="Features"/> lists what this relay does on the path beyond carrying media
/// (<see cref="MediaTransportFeatures"/>); an older relay sends none, and the participant then uses none.
/// </summary>
public sealed record MediaTransportConfig(
    string Session,
    RtcIceServer[] IceServers,
    string IceTransportPolicy,
    int MaxMessageBytes,
    long ExpiresAtUnixSeconds,
    string? Unavailable = null,
    string[]? Features = null);

/// <summary>What a relay announces in <see cref="MediaTransportConfig.Features"/>.</summary>
public static class MediaTransportFeatures
{
    /// <summary>
    /// The relay takes NackRequests on the data channel, resends video frames it still holds to that receiver alone,
    /// forwards the rest to the sender, and declines (<see cref="FrameType.NackDeclined"/>) frames it dropped on purpose.
    /// </summary>
    public const string Nack = "nack";

    public static bool Has(MediaTransportConfig? config, string feature) =>
        config?.Features is { } features && Array.IndexOf(features, feature) >= 0;
}

/// <summary>An SDP offer or answer for one session.</summary>
public sealed record MediaTransportDescription(string Session, string Sdp, bool IceRestart = false);

/// <summary>One trickled ICE candidate; an empty <paramref name="Candidate"/> ends gathering.</summary>
public sealed record MediaTransportCandidate(string Session, string Candidate, string? SdpMid, int? SdpMLineIndex);

/// <summary>The relay's side of a session: "open", "failed" or "closed", with the path it uses.</summary>
public sealed record MediaTransportStateMessage(string Session, string State, RtcPath? Path = null);

/// <summary>Loss, in thousandths, that the reporter saw on audio arriving over this session in the last interval.</summary>
public sealed record MediaTransportReport(string Session, int ReceiveLossPermille);

/// <summary>The session is over; the participant keeps (or returns to) the stream transport.</summary>
public sealed record MediaTransportClose(string Session, string Reason);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MediaTransportRequest))]
[JsonSerializable(typeof(MediaTransportConfig))]
[JsonSerializable(typeof(MediaTransportDescription))]
[JsonSerializable(typeof(MediaTransportCandidate))]
[JsonSerializable(typeof(MediaTransportStateMessage))]
[JsonSerializable(typeof(MediaTransportReport))]
[JsonSerializable(typeof(MediaTransportClose))]
[JsonSerializable(typeof(RtcIceServer[]))]
[JsonSerializable(typeof(RtcPath))]
public sealed partial class MediaTransportJson : JsonSerializerContext;

/// <summary>Encoding of <see cref="FrameType.MediaTransport"/> frames.</summary>
public static class MediaTransportCodec
{
    public const byte Version = 1;
    public const int HeaderSize = 1 + 1 + 1 + 4;
    /// <summary>Signalling is small: an SDP with a handful of candidates fits many times over.</summary>
    public const int MaxPayloadBytes = 16 * 1024;

    public static int Write(IBufferWriter<byte> writer, MediaTransportKind kind, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(payload), "Media transport signalling is limited to 16 KiB.");
        var total = HeaderSize + payload.Length;
        var span = writer.GetSpan(total);
        span[0] = (byte)FrameType.MediaTransport;
        span[1] = Version;
        span[2] = (byte)kind;
        BinaryPrimitives.WriteInt32LittleEndian(span[3..], payload.Length);
        payload.CopyTo(span[HeaderSize..]);
        writer.Advance(total);
        return total;
    }

    public static bool TryRead(ReadOnlySpan<byte> frame, out MediaTransportKind kind, out ReadOnlySpan<byte> payload)
    {
        kind = default;
        payload = default;
        if (frame.Length < HeaderSize || frame[0] != (byte)FrameType.MediaTransport || frame[1] != Version)
            return false;
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame[3..]);
        if (length < 0 || length > MaxPayloadBytes || frame.Length != HeaderSize + length)
            return false;
        kind = (MediaTransportKind)frame[2];
        payload = frame.Slice(HeaderSize, length);
        return kind is >= MediaTransportKind.Request and <= MediaTransportKind.Close;
    }

    public static byte[] Encode<T>(MediaTransportKind kind, T message)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, typeof(T), MediaTransportJson.Default);
        var writer = new ArrayBufferWriter<byte>(HeaderSize + json.Length);
        Write(writer, kind, json);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Decode a payload; null when it is not valid JSON of that shape.</summary>
    public static T? Decode<T>(ReadOnlySpan<byte> payload) where T : class
    {
        try { return JsonSerializer.Deserialize(payload, typeof(T), MediaTransportJson.Default) as T; }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// <see cref="FrameType.MediaBundle"/>: a few complete MediaFrames in one datagram. Used for audio
/// redundancy on a lossy datagram path (the previous frame rides along with the current one); the
/// receiver drops whichever copy arrives second. Inner frames are forwarded or decrypted exactly as if
/// they had arrived alone, so this adds nothing to what the relay can see or alter.
/// </summary>
public static class MediaBundleCodec
{
    public const int MaxFrames = 4;
    public const int HeaderSize = 2;

    /// <summary>Bytes a bundle of these frames takes.</summary>
    public static int Size(int firstLength, int secondLength) => HeaderSize + 2 + firstLength + 2 + secondLength;

    public static int Write(Span<byte> destination, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var total = Size(first.Length, second.Length);
        if (destination.Length < total || first.Length > ushort.MaxValue || second.Length > ushort.MaxValue)
            throw new ArgumentException("The bundle does not fit.", nameof(destination));
        destination[0] = (byte)FrameType.MediaBundle;
        destination[1] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], (ushort)first.Length);
        first.CopyTo(destination[4..]);
        var offset = 4 + first.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], (ushort)second.Length);
        second.CopyTo(destination[(offset + 2)..]);
        return total;
    }

    /// <summary>
    /// Split a bundle into its frames, oldest first. False when it is malformed or holds anything other
    /// than MediaFrames; nothing in a bad bundle is used.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> bundle, Span<Range> frames, out int count)
    {
        count = 0;
        if (bundle.Length < HeaderSize || bundle[0] != (byte)FrameType.MediaBundle)
            return false;
        var expected = bundle[1];
        if (expected is 0 or > MaxFrames || frames.Length < expected)
            return false;
        var offset = HeaderSize;
        for (var index = 0; index < expected; index++)
        {
            if (bundle.Length - offset < 2)
                return false;
            var length = BinaryPrimitives.ReadUInt16LittleEndian(bundle[offset..]);
            offset += 2;
            if (length < BoltCodec.MediaFrameHeaderSize || bundle.Length - offset < length || bundle[offset] != (byte)FrameType.MediaFrame)
                return false;
            frames[index] = new Range(offset, offset + length);
            offset += length;
        }
        if (offset != bundle.Length)
            return false;
        count = expected;
        return true;
    }
}
