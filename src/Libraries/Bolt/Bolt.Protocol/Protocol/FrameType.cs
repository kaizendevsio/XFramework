namespace Bolt.Protocol;

/// <summary>
/// Bolt wire protocol frame types. First byte of every frame.
/// </summary>
public enum FrameType : byte
{
    /// <summary>RPC request: [1:type] [16:requestId] [4:recipientHash] [4:commandHash] [4:payloadLen] [payload]</summary>
    Request = 0x01,
    /// <summary>RPC response: [1:type] [16:requestId] [2:statusCode] [4:payloadLen] [payload]</summary>
    Response = 0x02,
    /// <summary>Client registration: [1:type] [4:clientIdLen] [clientId] [4:clientNameLen] [clientName]</summary>
    Register = 0x03,
    /// <summary>Registration ack: [1:type] [1:success]</summary>
    RegisterAck = 0x04,
    /// <summary>Fire-and-forget push: same header as Request</summary>
    Push = 0x05,
    /// <summary>Subscribe to a topic: [1:type] [4:topicHash] [1:flags] [4:subscriberIdLen] [subscriberId] [4:topicLen] [topic]</summary>
    Subscribe = 0x06,
    /// <summary>Unsubscribe from a topic: [1:type] [4:topicHash] [4:subscriberIdLen] [subscriberId]</summary>
    Unsubscribe = 0x07,
    /// <summary>Publish to a topic: [1:type] [4:topicHash] [1:flags] [4:payloadLen] [payload]</summary>
    Publish = 0x08,
    /// <summary>Hub-delivered event: [1:type] [4:topicHash] [8:sequenceNumber] [1:flags] [4:payloadLen] [payload]</summary>
    Event = 0x09,
    /// <summary>Acknowledge durable messages: [1:type] [4:topicHash] [4:subscriberIdLen] [subscriberId] [8:upToSequenceNumber]</summary>
    Ack = 0x0A,
    /// <summary>Hub-side query execution (transitional shim): [1:type] [16:requestId] [4:payloadLen] [payload]</summary>
    ExecuteQuery = 0x0B,
    /// <summary>Hub-side change execution (transitional shim): [1:type] [16:requestId] [4:payloadLen] [payload]</summary>
    ExecuteChanges = 0x0C,
    /// <summary>Open bidirectional stream: [1:type] [16:streamId] [4:recipientHash] [4:commandHash]</summary>
    StreamOpen = 0x10,
    /// <summary>Stream data chunk: [1:type] [16:streamId] [4:payloadLen] [payload]</summary>
    StreamData = 0x11,
    /// <summary>Close stream: [1:type] [16:streamId] [2:statusCode]</summary>
    StreamClose = 0x12,

    // -- Media --

    /// <summary>Media config/negotiation: [1:type] [16:streamId] [16:callId] [1:mediaType] [1:codecId] [4:sampleRateOrWidth] [4:channelCountOrHeight] [4:bitrateKbps] [1:flags] [4:extensionLen] [extension]</summary>
    MediaConfig = 0x20,
    /// <summary>Encoded media frame: [1:type] [16:streamId] [4:sequenceNumber] [4:timestamp] [1:flags] [4:payloadLen] [payload]</summary>
    MediaFrame = 0x21,
    /// <summary>Receiver feedback: [1:type] [16:streamId] [4:highestSeqReceived] [4:cumulativeLost] [4:jitterMs_x100] [2:rttMs] [1:qualityHint]</summary>
    MediaFeedback = 0x22,
    /// <summary>Keyframe request: [1:type] [16:streamId]</summary>
    MediaKeyRequest = 0x23,
    /// <summary>Call signaling: [1:type] [16:callId] [1:signalType] [4:payloadLen] [payload]</summary>
    CallSignal = 0x24,
    /// <summary>FEC parity frame: [1:type] [16:streamId] [4:fecGroupStart] [1:fecGroupSize] [4:payloadLen] [payload]</summary>
    FecFrame = 0x25,
    /// <summary>NACK retransmission request: [1:type] [16:streamId] [2:nackCount] [nackCount * 4:missingSeqs]</summary>
    NackRequest = 0x26,
}

/// <summary>Media type identifier.</summary>
public enum MediaType : byte
{
    Audio = 0x01,
    Video = 0x02,
    ScreenShare = 0x03,
}

/// <summary>Codec identifier for media streams.</summary>
public enum CodecId : byte
{
    Opus = 0x01,
    H264 = 0x02,
    VP9 = 0x03,
    AV1 = 0x04,
    H265 = 0x05,
}

/// <summary>Call signal type.</summary>
public enum SignalType : byte
{
    Initiate = 0x01,
    Ring = 0x02,
    Answer = 0x03,
    Reject = 0x04,
    End = 0x05,
    Hold = 0x06,
    Unhold = 0x07,
    AddParticipant = 0x08,
    RemoveParticipant = 0x09,
    DirectOffer = 0x0A,
    DirectAnswer = 0x0B,
    KeyExchange = 0x0C,
}

/// <summary>Quality hint from receiver to sender.</summary>
public enum QualityHint : byte
{
    Maintain = 0x00,
    Increase = 0x01,
    Decrease = 0x02,
    KeyframeNeeded = 0x03,
}
