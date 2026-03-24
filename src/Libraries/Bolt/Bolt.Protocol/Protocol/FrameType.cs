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
    /// <summary>Open bidirectional stream: [1:type] [16:streamId] [4:recipientHash] [4:commandHash]</summary>
    StreamOpen = 0x10,
    /// <summary>Stream data chunk: [1:type] [16:streamId] [4:payloadLen] [payload]</summary>
    StreamData = 0x11,
    /// <summary>Close stream: [1:type] [16:streamId] [2:statusCode]</summary>
    StreamClose = 0x12,
}
