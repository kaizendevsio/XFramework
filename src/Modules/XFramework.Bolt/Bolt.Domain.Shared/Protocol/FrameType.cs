namespace Bolt.Domain.Shared.Protocol;

/// <summary>
/// Frame types for the Bolt binary protocol.
/// First byte of every frame.
/// </summary>
public enum FrameType : byte
{
    /// <summary>
    /// RPC request: client → server → recipient.
    /// Header: [1:type] [16:requestId] [4:recipientHash] [4:commandHash] [4:payloadLen]
    /// </summary>
    Request = 0x01,

    /// <summary>
    /// RPC response: recipient → server → original caller.
    /// Header: [1:type] [16:requestId] [2:statusCode] [4:payloadLen]
    /// </summary>
    Response = 0x02,

    /// <summary>
    /// Client registration with the server.
    /// Header: [1:type] [4:clientIdLen] [clientId] [4:clientNameLen] [clientName]
    /// </summary>
    Register = 0x03,

    /// <summary>
    /// Registration acknowledgement from server.
    /// Header: [1:type] [1:success]
    /// </summary>
    RegisterAck = 0x04,

    /// <summary>
    /// Fire-and-forget push (no response expected).
    /// Header: [1:type] [16:requestId] [4:recipientHash] [4:commandHash] [4:payloadLen]
    /// </summary>
    Push = 0x05,

    /// <summary>
    /// Open a bidirectional byte stream.
    /// Header: [1:type] [16:streamId] [4:recipientHash] [4:commandHash]
    /// </summary>
    StreamOpen = 0x10,

    /// <summary>
    /// Stream data chunk. Sent continuously in either direction on an open stream.
    /// Header: [1:type] [16:streamId] [4:payloadLen] [payload]
    /// </summary>
    StreamData = 0x11,

    /// <summary>
    /// Close a stream. Sent by either side when done.
    /// Header: [1:type] [16:streamId] [2:statusCode]
    /// </summary>
    StreamClose = 0x12,

    // -- Media --

    /// <summary>Media config/negotiation.</summary>
    MediaConfig = 0x20,
    /// <summary>Encoded media frame (audio/video).</summary>
    MediaFrame = 0x21,
    /// <summary>Receiver feedback for adaptive bitrate.</summary>
    MediaFeedback = 0x22,
    /// <summary>Keyframe request.</summary>
    MediaKeyRequest = 0x23,
    /// <summary>Call signaling (initiate, answer, reject, end, hold).</summary>
    CallSignal = 0x24,
    /// <summary>FEC parity frame.</summary>
    FecFrame = 0x25,
}
