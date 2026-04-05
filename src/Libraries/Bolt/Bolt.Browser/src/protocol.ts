/**
 * Bolt wire protocol encoder/decoder for the browser.
 *
 * Matches the .NET BoltCodec exactly — same frame layouts, same byte offsets,
 * same little-endian encoding. Every constant and offset here is derived from
 * the authoritative Bolt.Protocol/Protocol/BoltCodec.cs.
 *
 * All multi-byte integers are little-endian to match .NET BinaryPrimitives.
 */

// ─── Frame type constants (first byte of every frame) ────────────────────────

export const FrameType = {
    Request:         0x01,
    Response:        0x02,
    Register:        0x03,
    RegisterAck:     0x04,
    Push:            0x05,
    StreamOpen:      0x10,
    StreamData:      0x11,
    StreamClose:     0x12,
    MediaConfig:     0x20,
    MediaFrame:      0x21,
    MediaFeedback:   0x22,
    MediaKeyRequest: 0x23,
    CallSignal:      0x24,
    FecFrame:        0x25,
    NackRequest:     0x26,
} as const;

export type FrameTypeValue = (typeof FrameType)[keyof typeof FrameType];

// ─── Media enums ─────────────────────────────────────────────────────────────

export const MediaType = {
    Audio:       0x01,
    Video:       0x02,
    ScreenShare: 0x03,
} as const;

export type MediaTypeValue = (typeof MediaType)[keyof typeof MediaType];

export const CodecId = {
    Opus: 0x01,
    H264: 0x02,
    VP9:  0x03,
    AV1:  0x04,
    H265: 0x05,
} as const;

export type CodecIdValue = (typeof CodecId)[keyof typeof CodecId];

export const SignalType = {
    Initiate:          0x01,
    Ring:              0x02,
    Answer:            0x03,
    Reject:            0x04,
    End:               0x05,
    Hold:              0x06,
    Unhold:            0x07,
    AddParticipant:    0x08,
    RemoveParticipant: 0x09,
    DirectOffer:       0x0A,
    DirectAnswer:      0x0B,
    KeyExchange:       0x0C,
} as const;

export type SignalTypeValue = (typeof SignalType)[keyof typeof SignalType];

export const QualityHint = {
    Maintain:       0x00,
    Increase:       0x01,
    Decrease:       0x02,
    KeyframeNeeded: 0x03,
} as const;

export type QualityHintValue = (typeof QualityHint)[keyof typeof QualityHint];

// ─── Media frame flags ───────────────────────────────────────────────────────

export const MediaFrameFlags = {
    Keyframe:      0x01,
    FecProtected:  0x08,
    Encrypted:     0x10,
    DropEligible:  0x40,
    Compressed:    0x80,
} as const;

// ─── Header sizes (must match BoltCodec.cs constants exactly) ────────────────

export const HEADER_SIZE = {
    Request:        33,  // 1 + 16 + 4 + 4 + 4 + 4 (added senderHash)
    Response:       23,  // 1 + 16 + 2 + 4
    MediaFrame:     30,  // 1 + 16 + 4 + 4 + 1 + 4
    MediaConfig:    52,  // 1 + 16 + 16 + 1 + 1 + 4 + 4 + 4 + 1 + 4
    MediaFeedback:  32,  // 1 + 16 + 4 + 4 + 4 + 2 + 1
    MediaKeyRequest:17,  // 1 + 16
    CallSignal:     22,  // 1 + 16 + 1 + 4
    FecFrame:       26,  // 1 + 16 + 4 + 1 + 4
    NackRequest:    19,  // 1 + 16 + 2 (+ nackCount * 4)
    StreamOpen:     25,  // 1 + 16 + 4 + 4
    StreamData:     21,  // 1 + 16 + 4
    StreamClose:    19,  // 1 + 16 + 2
    RegisterAck:     2,  // 1 + 1
} as const;

// ─── Decoded frame types ─────────────────────────────────────────────────────

export interface MediaFrameData {
    streamId: string;
    sequenceNumber: number;
    timestamp: number;
    flags: number;
    payload: Uint8Array;
}

export interface CallSignalData {
    callId: string;
    signalType: SignalTypeValue;
    payload: Uint8Array;
}

export interface MediaConfigData {
    streamId: string;
    callId: string;
    mediaType: MediaTypeValue;
    codecId: CodecIdValue;
    param1: number;       // sampleRate (audio) or width (video)
    param2: number;       // channelCount (audio) or height (video)
    bitrateKbps: number;
    flags: number;
    extension: Uint8Array;
}

export interface MediaFeedbackData {
    streamId: string;
    highestSeqReceived: number;
    cumulativeLost: number;
    jitterX100: number;
    rttMs: number;
    qualityHint: QualityHintValue;
}

export interface MediaKeyRequestData {
    streamId: string;
}

export interface FecFrameData {
    streamId: string;
    fecGroupStart: number;
    fecGroupSize: number;
    payload: Uint8Array;
}

export interface NackRequestData {
    streamId: string;
    missingSequences: number[];
}

// ─── RPC frame types ────────────────────────────────────────────────────────

export interface RequestFrameData {
    requestId: string;
    recipientHash: number;
    senderHash: number;
    commandHash: number;
    payload: Uint8Array;
}

export interface ResponseFrameData {
    requestId: string;
    statusCode: number;
    payload: Uint8Array;
}

export interface StreamOpenData {
    streamId: string;
    recipientHash: number;
    commandHash: number;
}

export interface StreamDataFrame {
    streamId: string;
    payload: Uint8Array;
}

export interface StreamCloseData {
    streamId: string;
    statusCode: number;
}

// ─── GUID helpers (little-endian, matches .NET Guid binary layout) ───────────

/**
 * Convert a UUID string (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx) to the 16-byte
 * little-endian layout that .NET uses for Guid.TryWriteBytes / new Guid(span).
 *
 * .NET Guid memory layout:
 *   bytes  0-3:  int32 LE  (first 8 hex chars, reversed)
 *   bytes  4-5:  int16 LE  (next 4 hex chars, reversed)
 *   bytes  6-7:  int16 LE  (next 4 hex chars, reversed)
 *   bytes  8-15: big-endian (remaining 16 hex chars, as-is)
 */
export function guidToBytes(uuid: string): Uint8Array {
    const hex = uuid.replace(/-/g, '');
    if (hex.length !== 32) {
        throw new Error(`Invalid UUID: ${uuid}`);
    }

    const bytes = new Uint8Array(16);
    // Parse all 16 bytes from the hex string in natural order first
    for (let i = 0; i < 16; i++) {
        bytes[i] = parseInt(hex.substring(i * 2, i * 2 + 2), 16);
    }

    // Reverse first 4 bytes (int32 LE)
    const a0 = bytes[0], a1 = bytes[1], a2 = bytes[2], a3 = bytes[3];
    bytes[0] = a3; bytes[1] = a2; bytes[2] = a1; bytes[3] = a0;

    // Reverse bytes 4-5 (int16 LE)
    const b0 = bytes[4], b1 = bytes[5];
    bytes[4] = b1; bytes[5] = b0;

    // Reverse bytes 6-7 (int16 LE)
    const c0 = bytes[6], c1 = bytes[7];
    bytes[6] = c1; bytes[7] = c0;

    // Bytes 8-15 stay as-is (big-endian)
    return bytes;
}

/**
 * Convert 16 bytes in .NET Guid binary layout back to a UUID string.
 */
export function bytesToGuid(data: Uint8Array, offset: number = 0): string {
    const b = data.subarray(offset, offset + 16);

    // Reverse the LE groups back to natural hex order
    const hex =
        byteToHex(b[3]) + byteToHex(b[2]) + byteToHex(b[1]) + byteToHex(b[0]) + '-' +
        byteToHex(b[5]) + byteToHex(b[4]) + '-' +
        byteToHex(b[7]) + byteToHex(b[6]) + '-' +
        byteToHex(b[8]) + byteToHex(b[9]) + '-' +
        byteToHex(b[10]) + byteToHex(b[11]) + byteToHex(b[12]) +
        byteToHex(b[13]) + byteToHex(b[14]) + byteToHex(b[15]);

    return hex;
}

const HEX_TABLE: string[] = [];
for (let i = 0; i < 256; i++) {
    HEX_TABLE[i] = i.toString(16).padStart(2, '0');
}

function byteToHex(b: number): string {
    return HEX_TABLE[b];
}

/**
 * Generate a random UUID v4 string.
 */
export function newGuid(): string {
    const bytes = crypto.getRandomValues(new Uint8Array(16));
    // Set version 4 (0100xxxx at byte 6)
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    // Set variant 1 (10xxxxxx at byte 8)
    bytes[8] = (bytes[8] & 0x3f) | 0x80;

    return (
        byteToHex(bytes[0]) + byteToHex(bytes[1]) +
        byteToHex(bytes[2]) + byteToHex(bytes[3]) + '-' +
        byteToHex(bytes[4]) + byteToHex(bytes[5]) + '-' +
        byteToHex(bytes[6]) + byteToHex(bytes[7]) + '-' +
        byteToHex(bytes[8]) + byteToHex(bytes[9]) + '-' +
        byteToHex(bytes[10]) + byteToHex(bytes[11]) +
        byteToHex(bytes[12]) + byteToHex(bytes[13]) +
        byteToHex(bytes[14]) + byteToHex(bytes[15])
    );
}

// ─── Low-level read/write helpers (little-endian DataView) ───────────────────

function writeUint8(view: DataView, offset: number, value: number): void {
    view.setUint8(offset, value);
}

function writeInt32LE(view: DataView, offset: number, value: number): void {
    view.setInt32(offset, value, true);
}

function writeUint32LE(view: DataView, offset: number, value: number): void {
    view.setUint32(offset, value >>> 0, true);
}

function writeUint16LE(view: DataView, offset: number, value: number): void {
    view.setUint16(offset, value, true);
}

function readUint8(view: DataView, offset: number): number {
    return view.getUint8(offset);
}

function readInt32LE(view: DataView, offset: number): number {
    return view.getInt32(offset, true);
}

function readUint32LE(view: DataView, offset: number): number {
    return view.getUint32(offset, true);
}

function readUint16LE(view: DataView, offset: number): number {
    return view.getUint16(offset, true);
}

function writeGuid(arr: Uint8Array, offset: number, uuid: string): void {
    const guidBytes = guidToBytes(uuid);
    arr.set(guidBytes, offset);
}

// ─── FNV-1a hash (matches BoltCodec.Fnv1aHash in C#) ────────────────────────

/**
 * FNV-1a hash for routing. Consistent with the .NET BoltCodec.Fnv1aHash.
 * Operates on UTF-16 char values (same as C# string iteration).
 */
export function fnv1aHash(value: string): number {
    let hash = 0x811C9DC5; // 2166136261
    for (let i = 0; i < value.length; i++) {
        hash ^= value.charCodeAt(i);
        hash = Math.imul(hash, 0x01000193); // 16777619
    }
    return hash | 0; // Force signed int32
}

// ─── Encoding (write frames) ────────────────────────────────────────────────

/**
 * Encode a Register frame.
 * Layout: [1:type] [4:clientIdLen] [clientId] [4:clientNameLen] [clientName]
 */
export function writeRegister(clientId: string, clientName: string): Uint8Array {
    const encoder = new TextEncoder();
    const idBytes = encoder.encode(clientId);
    const nameBytes = encoder.encode(clientName);
    const totalSize = 1 + 4 + idBytes.length + 4 + nameBytes.length;

    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.Register);
    writeInt32LE(view, 1, idBytes.length);
    buf.set(idBytes, 5);
    writeInt32LE(view, 5 + idBytes.length, nameBytes.length);
    buf.set(nameBytes, 9 + idBytes.length);

    return buf;
}

/**
 * Encode a MediaFrame.
 * Layout: [1:type] [16:streamId] [4:sequenceNumber] [4:timestamp] [1:flags] [4:payloadLen] [payload]
 */
export function writeMediaFrame(
    streamId: string,
    sequenceNumber: number,
    timestamp: number,
    flags: number,
    payload: Uint8Array,
): Uint8Array {
    const totalSize = HEADER_SIZE.MediaFrame + payload.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.MediaFrame);
    writeGuid(buf, 1, streamId);
    writeUint32LE(view, 17, sequenceNumber);
    writeUint32LE(view, 21, timestamp);
    writeUint8(view, 25, flags);
    writeInt32LE(view, 26, payload.length);
    buf.set(payload, 30);

    return buf;
}

/**
 * Encode a CallSignal frame.
 * Layout: [1:type] [16:callId] [1:signalType] [4:payloadLen] [payload]
 */
export function writeCallSignal(
    callId: string,
    signalType: SignalTypeValue,
    payload: Uint8Array = new Uint8Array(0),
): Uint8Array {
    const totalSize = HEADER_SIZE.CallSignal + payload.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.CallSignal);
    writeGuid(buf, 1, callId);
    writeUint8(view, 17, signalType);
    writeInt32LE(view, 18, payload.length);
    buf.set(payload, 22);

    return buf;
}

/**
 * Encode a MediaConfig frame.
 * Layout: [1:type] [16:streamId] [16:callId] [1:mediaType] [1:codecId]
 *         [4:param1] [4:param2] [4:bitrateKbps] [1:flags] [4:extensionLen] [extension]
 */
export function writeMediaConfig(
    streamId: string,
    callId: string,
    mediaType: MediaTypeValue,
    codecId: CodecIdValue,
    param1: number,
    param2: number,
    bitrateKbps: number,
    flags: number = 0,
    extension: Uint8Array = new Uint8Array(0),
): Uint8Array {
    const totalSize = HEADER_SIZE.MediaConfig + extension.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.MediaConfig);
    writeGuid(buf, 1, streamId);
    writeGuid(buf, 17, callId);
    writeUint8(view, 33, mediaType);
    writeUint8(view, 34, codecId);
    writeInt32LE(view, 35, param1);
    writeInt32LE(view, 39, param2);
    writeInt32LE(view, 43, bitrateKbps);
    writeUint8(view, 47, flags);
    writeInt32LE(view, 48, extension.length);
    buf.set(extension, 52);

    return buf;
}

/**
 * Encode a MediaFeedback frame.
 * Layout: [1:type] [16:streamId] [4:highestSeq] [4:cumulativeLost]
 *         [4:jitterMs_x100] [2:rttMs] [1:qualityHint]
 */
export function writeMediaFeedback(
    streamId: string,
    highestSeqReceived: number,
    cumulativeLost: number,
    jitterX100: number,
    rttMs: number,
    qualityHint: QualityHintValue,
): Uint8Array {
    const buf = new Uint8Array(HEADER_SIZE.MediaFeedback);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.MediaFeedback);
    writeGuid(buf, 1, streamId);
    writeUint32LE(view, 17, highestSeqReceived);
    writeUint32LE(view, 21, cumulativeLost);
    writeUint32LE(view, 25, jitterX100);
    writeUint16LE(view, 29, rttMs);
    writeUint8(view, 31, qualityHint);

    return buf;
}

/**
 * Encode a MediaKeyRequest frame.
 * Layout: [1:type] [16:streamId]
 */
export function writeMediaKeyRequest(streamId: string): Uint8Array {
    const buf = new Uint8Array(HEADER_SIZE.MediaKeyRequest);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.MediaKeyRequest);
    writeGuid(buf, 1, streamId);

    return buf;
}

/**
 * Encode a FecFrame.
 * Layout: [1:type] [16:streamId] [4:fecGroupStart] [1:fecGroupSize] [4:payloadLen] [payload]
 */
export function writeFecFrame(
    streamId: string,
    fecGroupStart: number,
    fecGroupSize: number,
    payload: Uint8Array,
): Uint8Array {
    const totalSize = HEADER_SIZE.FecFrame + payload.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.FecFrame);
    writeGuid(buf, 1, streamId);
    writeUint32LE(view, 17, fecGroupStart);
    writeUint8(view, 21, fecGroupSize);
    writeInt32LE(view, 22, payload.length);
    buf.set(payload, 26);

    return buf;
}

// ─── Decoding (read frames) ─────────────────────────────────────────────────

/**
 * Peek at the frame type byte without consuming the buffer.
 */
export function readFrameType(data: Uint8Array): FrameTypeValue {
    return data[0] as FrameTypeValue;
}

/**
 * Read a RegisterAck frame. Returns the success boolean.
 * Layout: [1:type] [1:success]
 */
export function readRegisterAck(data: Uint8Array): boolean {
    if (data.length < HEADER_SIZE.RegisterAck) return false;
    return data[0] === FrameType.RegisterAck && data[1] === 1;
}

/**
 * Read a MediaFrame.
 * Layout: [1:type] [16:streamId] [4:seq] [4:ts] [1:flags] [4:payloadLen] [payload]
 */
export function readMediaFrame(data: Uint8Array): MediaFrameData | null {
    if (data.length < HEADER_SIZE.MediaFrame) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 26);
    if (payloadLen < 0 || data.length < HEADER_SIZE.MediaFrame + payloadLen) return null;

    return {
        streamId: bytesToGuid(data, 1),
        sequenceNumber: readUint32LE(view, 17),
        timestamp: readUint32LE(view, 21),
        flags: readUint8(view, 25),
        payload: data.slice(30, 30 + payloadLen),
    };
}

/**
 * Read a CallSignal frame.
 * Layout: [1:type] [16:callId] [1:signalType] [4:payloadLen] [payload]
 */
export function readCallSignal(data: Uint8Array): CallSignalData | null {
    if (data.length < HEADER_SIZE.CallSignal) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 18);
    if (payloadLen < 0 || data.length < HEADER_SIZE.CallSignal + payloadLen) return null;

    return {
        callId: bytesToGuid(data, 1),
        signalType: readUint8(view, 17) as SignalTypeValue,
        payload: data.slice(22, 22 + payloadLen),
    };
}

/**
 * Read a MediaConfig frame.
 * Layout: [1:type] [16:streamId] [16:callId] [1:mediaType] [1:codecId]
 *         [4:param1] [4:param2] [4:bitrateKbps] [1:flags] [4:extensionLen] [extension]
 */
export function readMediaConfig(data: Uint8Array): MediaConfigData | null {
    if (data.length < HEADER_SIZE.MediaConfig) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const extensionLen = readInt32LE(view, 48);
    if (extensionLen < 0 || data.length < HEADER_SIZE.MediaConfig + extensionLen) return null;

    return {
        streamId: bytesToGuid(data, 1),
        callId: bytesToGuid(data, 17),
        mediaType: readUint8(view, 33) as MediaTypeValue,
        codecId: readUint8(view, 34) as CodecIdValue,
        param1: readInt32LE(view, 35),
        param2: readInt32LE(view, 39),
        bitrateKbps: readInt32LE(view, 43),
        flags: readUint8(view, 47),
        extension: data.slice(52, 52 + extensionLen),
    };
}

/**
 * Read a MediaFeedback frame.
 * Layout: [1:type] [16:streamId] [4:highestSeq] [4:cumulativeLost]
 *         [4:jitterX100] [2:rttMs] [1:qualityHint]
 */
export function readMediaFeedback(data: Uint8Array): MediaFeedbackData | null {
    if (data.length < HEADER_SIZE.MediaFeedback) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);

    return {
        streamId: bytesToGuid(data, 1),
        highestSeqReceived: readUint32LE(view, 17),
        cumulativeLost: readUint32LE(view, 21),
        jitterX100: readUint32LE(view, 25),
        rttMs: readUint16LE(view, 29),
        qualityHint: readUint8(view, 31) as QualityHintValue,
    };
}

/**
 * Read a MediaKeyRequest frame.
 * Layout: [1:type] [16:streamId]
 */
export function readMediaKeyRequest(data: Uint8Array): MediaKeyRequestData | null {
    if (data.length < HEADER_SIZE.MediaKeyRequest) return null;

    return {
        streamId: bytesToGuid(data, 1),
    };
}

/**
 * Read a FecFrame.
 * Layout: [1:type] [16:streamId] [4:fecGroupStart] [1:fecGroupSize] [4:payloadLen] [payload]
 */
export function readFecFrame(data: Uint8Array): FecFrameData | null {
    if (data.length < HEADER_SIZE.FecFrame) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 22);
    if (payloadLen < 0 || data.length < HEADER_SIZE.FecFrame + payloadLen) return null;

    return {
        streamId: bytesToGuid(data, 1),
        fecGroupStart: readUint32LE(view, 17),
        fecGroupSize: readUint8(view, 21),
        payload: data.slice(26, 26 + payloadLen),
    };
}

// ─── RPC frame encoding/decoding ────────────────────────────────────────────

/**
 * Encode a Request frame.
 * Layout: [1:type] [16:requestId] [4:recipientHash] [4:commandHash] [4:payloadLen] [payload]
 */
export function writeRequest(
    requestId: string,
    recipientHash: number,
    senderHash: number,
    commandHash: number,
    payload: Uint8Array,
): Uint8Array {
    const totalSize = HEADER_SIZE.Request + payload.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.Request);
    writeGuid(buf, 1, requestId);
    writeInt32LE(view, 17, recipientHash);
    writeInt32LE(view, 21, senderHash);
    writeInt32LE(view, 25, commandHash);
    writeInt32LE(view, 29, payload.length);
    buf.set(payload, 33);

    return buf;
}

/**
 * Read a Request frame.
 * Layout: [1:type][16:requestId][4:recipientHash][4:senderHash][4:commandHash][4:payloadLen][payload]
 */
export function readRequest(data: Uint8Array): RequestFrameData | null {
    if (data.length < HEADER_SIZE.Request) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 29);
    if (payloadLen < 0 || data.length < HEADER_SIZE.Request + payloadLen) return null;

    return {
        requestId: bytesToGuid(data, 1),
        recipientHash: readInt32LE(view, 17),
        senderHash: readInt32LE(view, 21),
        commandHash: readInt32LE(view, 25),
        payload: data.slice(33, 33 + payloadLen),
    };
}

/**
 * Encode a Response frame.
 * Layout: [1:type] [16:requestId] [2:statusCode] [4:payloadLen] [payload]
 */
export function writeResponse(
    requestId: string,
    statusCode: number,
    payload: Uint8Array,
): Uint8Array {
    const totalSize = HEADER_SIZE.Response + payload.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.Response);
    writeGuid(buf, 1, requestId);
    writeUint16LE(view, 17, statusCode & 0xFFFF); // int16 LE (same as .NET short)
    writeInt32LE(view, 19, payload.length);
    buf.set(payload, 23);

    return buf;
}

/**
 * Read a Response frame.
 */
export function readResponse(data: Uint8Array): ResponseFrameData | null {
    if (data.length < HEADER_SIZE.Response) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 19);
    if (payloadLen < 0 || data.length < HEADER_SIZE.Response + payloadLen) return null;

    return {
        requestId: bytesToGuid(data, 1),
        statusCode: view.getInt16(17, true),
        payload: data.slice(23, 23 + payloadLen),
    };
}

/**
 * Encode a Push frame (fire-and-forget, same layout as Request).
 * Layout: [1:type=0x05] [16:requestId] [4:recipientHash] [4:senderHash] [4:commandHash] [4:payloadLen] [payload]
 */
export function writePush(
    recipientHash: number,
    senderHash: number,
    commandHash: number,
    payload: Uint8Array,
): Uint8Array {
    const requestId = newGuid();
    const totalSize = HEADER_SIZE.Request + payload.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.Push);
    writeGuid(buf, 1, requestId);
    writeInt32LE(view, 17, recipientHash);
    writeInt32LE(view, 21, senderHash);
    writeInt32LE(view, 25, commandHash);
    writeInt32LE(view, 29, payload.length);
    buf.set(payload, 33);

    return buf;
}

// ─── Stream frame encoding/decoding ─────────────────────────────────────────

/**
 * Encode a StreamOpen frame.
 * Layout: [1:type] [16:streamId] [4:recipientHash] [4:commandHash]
 */
export function writeStreamOpen(streamId: string, recipientHash: number, commandHash: number): Uint8Array {
    const buf = new Uint8Array(HEADER_SIZE.StreamOpen);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.StreamOpen);
    writeGuid(buf, 1, streamId);
    writeInt32LE(view, 17, recipientHash);
    writeInt32LE(view, 21, commandHash);

    return buf;
}

/**
 * Encode a StreamData frame.
 * Layout: [1:type] [16:streamId] [4:payloadLen] [payload]
 */
export function writeStreamData(streamId: string, payload: Uint8Array): Uint8Array {
    const totalSize = HEADER_SIZE.StreamData + payload.length;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.StreamData);
    writeGuid(buf, 1, streamId);
    writeInt32LE(view, 17, payload.length);
    buf.set(payload, 21);

    return buf;
}

/**
 * Encode a StreamClose frame.
 * Layout: [1:type] [16:streamId] [2:statusCode]
 */
export function writeStreamClose(streamId: string, statusCode = 200): Uint8Array {
    const buf = new Uint8Array(HEADER_SIZE.StreamClose);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.StreamClose);
    writeGuid(buf, 1, streamId);
    writeUint16LE(view, 17, statusCode & 0xFFFF); // int16 LE (2 bytes, same as .NET short)

    return buf;
}

/**
 * Read a StreamOpen frame.
 */
export function readStreamOpen(data: Uint8Array): StreamOpenData | null {
    if (data.length < HEADER_SIZE.StreamOpen) return null;
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    return {
        streamId: bytesToGuid(data, 1),
        recipientHash: readInt32LE(view, 17),
        commandHash: readInt32LE(view, 21),
    };
}

/**
 * Read a StreamData frame.
 */
export function readStreamData(data: Uint8Array): StreamDataFrame | null {
    if (data.length < HEADER_SIZE.StreamData) return null;
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 17);
    if (payloadLen < 0 || data.length < HEADER_SIZE.StreamData + payloadLen) return null;
    return {
        streamId: bytesToGuid(data, 1),
        payload: data.slice(21, 21 + payloadLen),
    };
}

/**
 * Read a StreamClose frame.
 */
export function readStreamClose(data: Uint8Array): StreamCloseData | null {
    if (data.length < HEADER_SIZE.StreamClose) return null;
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    return {
        streamId: bytesToGuid(data, 1),
        statusCode: view.getInt16(17, true),
    };
}

/**
 * Encode a NackRequest frame.
 * Layout: [1:type] [16:streamId] [2:nackCount] [nackCount * 4:missingSeqs]
 */
export function writeNackRequest(streamId: string, missingSequences: number[]): Uint8Array {
    const totalSize = HEADER_SIZE.NackRequest + missingSequences.length * 4;
    const buf = new Uint8Array(totalSize);
    const view = new DataView(buf.buffer);

    writeUint8(view, 0, FrameType.NackRequest);
    writeGuid(buf, 1, streamId);
    writeUint16LE(view, 17, missingSequences.length);
    for (let i = 0; i < missingSequences.length; i++) {
        writeUint32LE(view, 19 + i * 4, missingSequences[i]);
    }
    return buf;
}

/**
 * Read a NackRequest frame.
 * Layout: [1:type] [16:streamId] [2:nackCount] [nackCount * 4:missingSeqs]
 */
export function readNackRequest(data: Uint8Array): NackRequestData | null {
    if (data.length < HEADER_SIZE.NackRequest) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const nackCount = readUint16LE(view, 17);
    const totalSize = HEADER_SIZE.NackRequest + nackCount * 4;
    if (data.length < totalSize) return null;

    const missingSequences: number[] = [];
    for (let i = 0; i < nackCount; i++) {
        missingSequences.push(readUint32LE(view, 19 + i * 4));
    }

    return {
        streamId: bytesToGuid(data, 1),
        missingSequences,
    };
}

// ─── Convenience aliases ────────────────────────────────────────────────────

export const FRAME = FrameType;
export const SIGNAL = SignalType;
