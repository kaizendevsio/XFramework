/**
 * Bolt wire protocol encoder/decoder for the browser.
 *
 * Matches the .NET BoltCodec exactly — same frame layouts, same byte offsets,
 * same little-endian encoding. Every constant and offset here is derived from
 * the authoritative Bolt.Protocol/Protocol/BoltCodec.cs.
 *
 * All multi-byte integers are little-endian to match .NET BinaryPrimitives.
 * Decoded payload and extension fields are views over the input frame. Callers
 * that retain them must also retain the input frame or make their own copy.
 */

// ─── Frame type constants (first byte of every frame) ────────────────────────

export const FrameType = {
    Request:         0x01,
    Response:        0x02,
    Register:        0x03,
    RegisterAck:     0x04,
    Push:            0x05,
    Subscribe:       0x06,
    Unsubscribe:     0x07,
    Publish:         0x08,
    Event:           0x09,
    Ack:             0x0A,
    RequestCancel:   0x0B,
    Batch:           0x0C,
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

export const WIRE_VERSION = 2;
export const MAX_FRAME_SIZE = 8 * 1024 * 1024;
export const MAX_BATCH_FRAMES = 32;
export const MAX_BATCH_BYTES = 256 * 1024;
export const MAX_STRING_SIZE = 64 * 1024;
export const MAX_TOPIC_SIZE = 4096;
export const MAX_ACTOR_TOKEN_SIZE = 8192;

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
    Publish:        14,  // 1 + 4 + 1 + 4 + topic + 4 + payload
    Event:          22,  // 1 + 4 + 8 + 1 + 4 + subscriberId + 4 + payload
    RegisterAck:     4,  // 1 + 1 + 2
    RequestCancel:  17,  // 1 + 16
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

export interface PublishFrameData {
    topicHash: number;
    topic: string;
    durableEligible: boolean;
    payload: Uint8Array;
}

export interface EventFrameData {
    topicHash: number;
    sequenceNumber: bigint;
    isReplay: boolean;
    subscriberId: string;
    payload: Uint8Array;
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
    if (!/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(uuid)) {
        throw new Error(`Invalid UUID: ${uuid}`);
    }
    const hex = uuid.replace(/-/g, '');

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
    if (!Number.isSafeInteger(offset) || offset < 0 || offset + 16 > data.length) {
        throw new RangeError('A UUID requires 16 bytes at the requested offset.');
    }
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

function writeInt64LE(view: DataView, offset: number, value: bigint | number): void {
    if (typeof value === 'number' && !Number.isSafeInteger(value)) {
        throw new RangeError('64-bit integer numbers must be safe integers; use bigint for larger values.');
    }

    const bigintValue = BigInt(value);
    if (bigintValue < -(1n << 63n) || bigintValue > (1n << 63n) - 1n) {
        throw new RangeError('Value is outside the signed 64-bit integer range.');
    }

    view.setBigInt64(offset, bigintValue, true);
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

function readInt64LE(view: DataView, offset: number): bigint {
    return view.getBigInt64(offset, true);
}

function writeGuid(arr: Uint8Array, offset: number, uuid: string): void {
    const guidBytes = guidToBytes(uuid);
    arr.set(guidBytes, offset);
}

const UTF8_ENCODER = new TextEncoder();
const UTF8_DECODER = new TextDecoder();

function createFrame(size: number): [Uint8Array, DataView] {
    if (!Number.isSafeInteger(size) || size < 1 || size > MAX_FRAME_SIZE) {
        throw new RangeError(`Bolt frames must be between 1 and ${MAX_FRAME_SIZE} bytes.`);
    }

    const buffer = new Uint8Array(size);
    return [buffer, new DataView(buffer.buffer)];
}

function validateStringBytes(
    value: Uint8Array,
    maximumBytes: number,
    fieldName: string,
    allowEmpty: boolean,
): void {
    if ((!allowEmpty && value.length === 0) || value.length > maximumBytes) {
        throw new RangeError(
            `${fieldName} must contain ${allowEmpty ? `0 to ${maximumBytes}` : `1 to ${maximumBytes}`} UTF-8 bytes.`,
        );
    }
}

function isReadableFrame(data: Uint8Array, minimumSize: number): boolean {
    return data.length >= minimumSize && data.length <= MAX_FRAME_SIZE;
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
 * Layout: [1:type] [2:wireVersion] [4:clientIdLen] [clientId] [4:clientNameLen] [clientName]
 */
export function writeRegister(clientId: string, clientName: string): Uint8Array {
    const idBytes = UTF8_ENCODER.encode(clientId);
    const nameBytes = UTF8_ENCODER.encode(clientName);
    validateStringBytes(idBytes, MAX_STRING_SIZE, 'clientId', false);
    validateStringBytes(nameBytes, MAX_STRING_SIZE, 'clientName', false);
    const totalSize = 1 + 2 + 4 + idBytes.length + 4 + nameBytes.length;

    const [buf, view] = createFrame(totalSize);

    writeUint8(view, 0, FrameType.Register);
    writeUint16LE(view, 1, WIRE_VERSION);
    writeInt32LE(view, 3, idBytes.length);
    buf.set(idBytes, 7);
    writeInt32LE(view, 7 + idBytes.length, nameBytes.length);
    buf.set(nameBytes, 11 + idBytes.length);

    return buf;
}

/** Encode a batch of complete, non-media frames. */
export function writeBatch(frames: readonly Uint8Array[]): Uint8Array {
    if (frames.length < 2 || frames.length > MAX_BATCH_FRAMES) {
        throw new RangeError(`Bolt batches require 2 to ${MAX_BATCH_FRAMES} frames.`);
    }

    let totalSize = 1 + 4;
    for (const frame of frames) {
        if (frame.length === 0) throw new RangeError('Bolt batches cannot contain empty frames.');
        const frameType = frame[0] as FrameTypeValue;
        if (!isBatchableFrameType(frameType) || !isCompleteBatchInnerFrame(frame)) {
            throw new RangeError('Bolt batches cannot contain registration, nested batch, or media frames.');
        }
        totalSize += 4 + frame.length;
    }
    if (totalSize > MAX_BATCH_BYTES) {
        throw new RangeError(`Bolt batches cannot exceed ${MAX_BATCH_BYTES} bytes.`);
    }

    const [buf, view] = createFrame(totalSize);
    writeUint8(view, 0, FrameType.Batch);
    writeInt32LE(view, 1, frames.length);
    let offset = 5;
    for (const frame of frames) {
        writeInt32LE(view, offset, frame.length);
        offset += 4;
        buf.set(frame, offset);
        offset += frame.length;
    }
    return buf;
}

/** Encode a cancellation for an RPC request that has already been sent. */
export function writeRequestCancel(requestId: string): Uint8Array {
    const [buf, view] = createFrame(HEADER_SIZE.RequestCancel);
    writeUint8(view, 0, FrameType.RequestCancel);
    writeGuid(buf, 1, requestId);
    return buf;
}

/** Read an RPC cancellation frame. */
export function readRequestCancel(data: Uint8Array): string | null {
    if (!isReadableFrame(data, HEADER_SIZE.RequestCancel) || data.length !== HEADER_SIZE.RequestCancel)
        return null;
    if (data[0] !== FrameType.RequestCancel)
        return null;
    return bytesToGuid(data, 1);
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
    const [buf, view] = createFrame(totalSize);

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
    const [buf, view] = createFrame(totalSize);

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
    const [buf, view] = createFrame(totalSize);

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
    const [buf, view] = createFrame(totalSize);

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
    if (!isReadableFrame(data, 1)) {
        throw new RangeError(`Bolt frames must be between 1 and ${MAX_FRAME_SIZE} bytes.`);
    }
    return data[0] as FrameTypeValue;
}

export interface RegisterAckData {
    success: boolean;
    version: number;
}

/**
 * Read a RegisterAck frame.
 * Layout: [1:type] [1:success] [2:wireVersion]
 */
export function readRegisterAck(data: Uint8Array): boolean {
    return readRegisterAckDetails(data)?.success === true;
}

export function readRegisterAckDetails(data: Uint8Array): RegisterAckData | null {
    if (data.length !== HEADER_SIZE.RegisterAck || data[0] !== FrameType.RegisterAck) return null;
    if (data[1] !== 0 && data[1] !== 1) return null;
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    return { success: data[1] === 1, version: readUint16LE(view, 2) };
}

export function readBatch(data: Uint8Array): Uint8Array[] | null {
    if (data.length < 5 || data.length > MAX_BATCH_BYTES || data[0] !== FrameType.Batch) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const count = readInt32LE(view, 1);
    if (count < 2 || count > MAX_BATCH_FRAMES) return null;

    const frames: Uint8Array[] = [];
    let offset = 5;
    for (let i = 0; i < count; i++) {
        if (offset + 4 > data.length) return null;
        const length = readInt32LE(view, offset);
        offset += 4;
        if (length <= 0 || length > MAX_FRAME_SIZE || offset + length > data.length) return null;

        const frame = data.subarray(offset, offset + length);
        const frameType = frame[0] as FrameTypeValue;
        if (!isBatchableFrameType(frameType) || !isCompleteBatchInnerFrame(frame)) {
            return null;
        }
        frames.push(frame);
        offset += length;
    }

    return offset === data.length ? frames : null;
}

export function isMediaFrameType(frameType: FrameTypeValue): boolean {
    return frameType === FrameType.MediaConfig ||
        frameType === FrameType.MediaFrame ||
        frameType === FrameType.MediaFeedback ||
        frameType === FrameType.MediaKeyRequest ||
        frameType === FrameType.CallSignal ||
        frameType === FrameType.FecFrame ||
        frameType === FrameType.NackRequest;
}

function isBatchableFrameType(frameType: FrameTypeValue): boolean {
    return frameType === FrameType.Request ||
        frameType === FrameType.Response ||
        frameType === FrameType.Push ||
        frameType === FrameType.Subscribe ||
        frameType === FrameType.Unsubscribe ||
        frameType === FrameType.Publish ||
        frameType === FrameType.Event ||
        frameType === FrameType.Ack ||
        frameType === FrameType.RequestCancel ||
        frameType === FrameType.StreamOpen ||
        frameType === FrameType.StreamData ||
        frameType === FrameType.StreamClose;
}

function isCompleteBatchInnerFrame(frame: Uint8Array): boolean {
    const type = frame[0] as FrameTypeValue;
    const view = new DataView(frame.buffer, frame.byteOffset, frame.byteLength);
    const exactPayloadFrame = (headerSize: number, lengthOffset: number): boolean => {
        if (frame.length < headerSize) return false;
        const payloadLength = readInt32LE(view, lengthOffset);
        return payloadLength >= 0 && headerSize + payloadLength === frame.length;
    };

    switch (type) {
        case FrameType.Request:
        case FrameType.Push:
            return exactPayloadFrame(HEADER_SIZE.Request, 29);
        case FrameType.Response:
            return exactPayloadFrame(HEADER_SIZE.Response, 19);
        case FrameType.RequestCancel:
            return frame.length === HEADER_SIZE.RequestCancel;
        case FrameType.StreamOpen:
            return frame.length === HEADER_SIZE.StreamOpen;
        case FrameType.StreamData:
            return exactPayloadFrame(HEADER_SIZE.StreamData, 17);
        case FrameType.StreamClose:
            return frame.length === HEADER_SIZE.StreamClose;
        case FrameType.Subscribe:
            return hasExactSubscribeLength(frame, view);
        case FrameType.Unsubscribe:
            return hasExactUnsubscribeLength(frame, view);
        case FrameType.Publish:
            return hasExactPublishLength(frame, view);
        case FrameType.Event:
            return hasExactEventLength(frame, view);
        case FrameType.Ack:
            return hasExactAckLength(frame, view);
        default:
            return false;
    }
}

function hasExactSubscribeLength(frame: Uint8Array, view: DataView): boolean {
    if (frame.length < 14) return false;
    const idLength = readInt32LE(view, 6);
    if (idLength < 0 || idLength > MAX_TOPIC_SIZE || frame.length < 14 + idLength) return false;
    const topicLength = readInt32LE(view, 10 + idLength);
    if (topicLength < 0 || topicLength > MAX_TOPIC_SIZE) return false;
    const topicOffset = 14 + idLength;
    return hasMatchingTopicHash(frame, view, topicOffset, topicLength) &&
        hasExactOptionalTokenLength(frame, view, topicOffset + topicLength);
}

function hasExactUnsubscribeLength(frame: Uint8Array, view: DataView): boolean {
    if (frame.length < 13) return false;
    const topicLength = readInt32LE(view, 5);
    if (topicLength <= 0 || topicLength > MAX_TOPIC_SIZE || frame.length < 13 + topicLength) return false;
    if (!hasMatchingTopicHash(frame, view, 9, topicLength)) return false;
    const idLength = readInt32LE(view, 9 + topicLength);
    if (idLength < 0 || idLength > MAX_TOPIC_SIZE) return false;
    const legacyLength = 13 + topicLength + idLength;
    if (frame.length === legacyLength) return true;
    return hasExactOptionalTokenLength(frame, view, legacyLength + 1);
}

function hasExactPublishLength(frame: Uint8Array, view: DataView): boolean {
    if (frame.length < HEADER_SIZE.Publish) return false;
    const topicLength = readInt32LE(view, 6);
    if (topicLength <= 0 || topicLength > MAX_TOPIC_SIZE || frame.length < 14 + topicLength) return false;
    if (!hasMatchingTopicHash(frame, view, 10, topicLength)) return false;
    const payloadLength = readInt32LE(view, 10 + topicLength);
    return payloadLength >= 0 && 14 + topicLength + payloadLength === frame.length;
}

function hasExactEventLength(frame: Uint8Array, view: DataView): boolean {
    if (frame.length < HEADER_SIZE.Event) return false;
    const idLength = readInt32LE(view, 14);
    if (idLength < 0 || idLength > MAX_TOPIC_SIZE || frame.length < 22 + idLength) return false;
    const payloadLength = readInt32LE(view, 18 + idLength);
    return payloadLength >= 0 && 22 + idLength + payloadLength === frame.length;
}

function hasExactAckLength(frame: Uint8Array, view: DataView): boolean {
    if (frame.length < 13) return false;
    const topicLength = readInt32LE(view, 5);
    if (topicLength <= 0 || topicLength > MAX_TOPIC_SIZE || frame.length < 21 + topicLength) return false;
    if (!hasMatchingTopicHash(frame, view, 9, topicLength)) return false;
    const idLength = readInt32LE(view, 9 + topicLength);
    if (idLength < 0 || idLength > MAX_TOPIC_SIZE) return false;
    return hasExactOptionalTokenLength(frame, view, 21 + topicLength + idLength);
}

function hasExactOptionalTokenLength(frame: Uint8Array, view: DataView, offset: number): boolean {
    if (frame.length === offset) return true;
    if (offset + 4 > frame.length) return false;
    const tokenLength = readInt32LE(view, offset);
    return tokenLength >= 0 && tokenLength <= MAX_ACTOR_TOKEN_SIZE && offset + 4 + tokenLength === frame.length;
}

function hasMatchingTopicHash(
    frame: Uint8Array,
    view: DataView,
    topicOffset: number,
    topicLength: number,
): boolean {
    if (topicOffset + topicLength > frame.length) return false;
    const topic = UTF8_DECODER.decode(frame.subarray(topicOffset, topicOffset + topicLength));
    return fnv1aHash(topic) === readInt32LE(view, 1);
}

/**
 * Read a MediaFrame.
 * Layout: [1:type] [16:streamId] [4:seq] [4:ts] [1:flags] [4:payloadLen] [payload]
 */
export function readMediaFrame(data: Uint8Array): MediaFrameData | null {
    if (!isReadableFrame(data, HEADER_SIZE.MediaFrame)) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 26);
    if (payloadLen < 0 || data.length < HEADER_SIZE.MediaFrame + payloadLen) return null;

    return {
        streamId: bytesToGuid(data, 1),
        sequenceNumber: readUint32LE(view, 17),
        timestamp: readUint32LE(view, 21),
        flags: readUint8(view, 25),
        payload: data.subarray(30, 30 + payloadLen),
    };
}

/**
 * Read a CallSignal frame.
 * Layout: [1:type] [16:callId] [1:signalType] [4:payloadLen] [payload]
 */
export function readCallSignal(data: Uint8Array): CallSignalData | null {
    if (!isReadableFrame(data, HEADER_SIZE.CallSignal)) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 18);
    if (payloadLen < 0 || data.length < HEADER_SIZE.CallSignal + payloadLen) return null;

    return {
        callId: bytesToGuid(data, 1),
        signalType: readUint8(view, 17) as SignalTypeValue,
        payload: data.subarray(22, 22 + payloadLen),
    };
}

/**
 * Read a MediaConfig frame.
 * Layout: [1:type] [16:streamId] [16:callId] [1:mediaType] [1:codecId]
 *         [4:param1] [4:param2] [4:bitrateKbps] [1:flags] [4:extensionLen] [extension]
 */
export function readMediaConfig(data: Uint8Array): MediaConfigData | null {
    if (!isReadableFrame(data, HEADER_SIZE.MediaConfig)) return null;

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
        extension: data.subarray(52, 52 + extensionLen),
    };
}

/**
 * Read a MediaFeedback frame.
 * Layout: [1:type] [16:streamId] [4:highestSeq] [4:cumulativeLost]
 *         [4:jitterX100] [2:rttMs] [1:qualityHint]
 */
export function readMediaFeedback(data: Uint8Array): MediaFeedbackData | null {
    if (!isReadableFrame(data, HEADER_SIZE.MediaFeedback)) return null;

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
    if (!isReadableFrame(data, HEADER_SIZE.MediaKeyRequest)) return null;

    return {
        streamId: bytesToGuid(data, 1),
    };
}

/**
 * Read a FecFrame.
 * Layout: [1:type] [16:streamId] [4:fecGroupStart] [1:fecGroupSize] [4:payloadLen] [payload]
 */
export function readFecFrame(data: Uint8Array): FecFrameData | null {
    if (!isReadableFrame(data, HEADER_SIZE.FecFrame)) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 22);
    if (payloadLen < 0 || data.length < HEADER_SIZE.FecFrame + payloadLen) return null;

    return {
        streamId: bytesToGuid(data, 1),
        fecGroupStart: readUint32LE(view, 17),
        fecGroupSize: readUint8(view, 21),
        payload: data.subarray(26, 26 + payloadLen),
    };
}

// ─── RPC frame encoding/decoding ────────────────────────────────────────────

/**
 * Encode a Request frame.
 * Layout: [1:type] [16:requestId] [4:recipientHash] [4:senderHash] [4:commandHash] [4:payloadLen] [payload]
 */
export function writeRequest(
    requestId: string,
    recipientHash: number,
    senderHash: number,
    commandHash: number,
    payload: Uint8Array,
): Uint8Array {
    const totalSize = HEADER_SIZE.Request + payload.length;
    const [buf, view] = createFrame(totalSize);

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
    if (!isReadableFrame(data, HEADER_SIZE.Request)) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 29);
    if (payloadLen < 0 || data.length < HEADER_SIZE.Request + payloadLen) return null;

    return {
        requestId: bytesToGuid(data, 1),
        recipientHash: readInt32LE(view, 17),
        senderHash: readInt32LE(view, 21),
        commandHash: readInt32LE(view, 25),
        payload: data.subarray(33, 33 + payloadLen),
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
    const [buf, view] = createFrame(totalSize);

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
    if (!isReadableFrame(data, HEADER_SIZE.Response)) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 19);
    if (payloadLen < 0 || data.length < HEADER_SIZE.Response + payloadLen) return null;

    return {
        requestId: bytesToGuid(data, 1),
        statusCode: view.getInt16(17, true),
        payload: data.subarray(23, 23 + payloadLen),
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
    const [buf, view] = createFrame(totalSize);

    writeUint8(view, 0, FrameType.Push);
    writeGuid(buf, 1, requestId);
    writeInt32LE(view, 17, recipientHash);
    writeInt32LE(view, 21, senderHash);
    writeInt32LE(view, 25, commandHash);
    writeInt32LE(view, 29, payload.length);
    buf.set(payload, 33);

    return buf;
}

// ─── Pub/sub frame encoding/decoding ─────────────────────────────────────────

/**
 * Encode a Subscribe frame.
 * Layout: [1:type] [4:topicHash] [1:flags] [4:subscriberIdLen] [subscriberId]
 *         [4:topicLen] [topic] [4:actorTokenLen] [actorToken]
 */
export function writeSubscribe(
    topic: string,
    subscriberId = '',
    durable = false,
    actorAccessToken = '',
): Uint8Array {
    const topicBytes = UTF8_ENCODER.encode(topic);
    const idBytes = UTF8_ENCODER.encode(subscriberId);
    const tokenBytes = UTF8_ENCODER.encode(actorAccessToken);
    validateStringBytes(topicBytes, MAX_TOPIC_SIZE, 'topic', false);
    validateStringBytes(idBytes, MAX_TOPIC_SIZE, 'subscriberId', true);
    validateStringBytes(tokenBytes, MAX_ACTOR_TOKEN_SIZE, 'actorAccessToken', true);
    const totalSize = 1 + 4 + 1 + 4 + idBytes.length + 4 + topicBytes.length + 4 + tokenBytes.length;
    const [buf, view] = createFrame(totalSize);

    writeUint8(view, 0, FrameType.Subscribe);
    writeInt32LE(view, 1, fnv1aHash(topic));
    writeUint8(view, 5, durable ? 0x01 : 0x00);
    writeInt32LE(view, 6, idBytes.length);
    buf.set(idBytes, 10);
    writeInt32LE(view, 10 + idBytes.length, topicBytes.length);
    buf.set(topicBytes, 14 + idBytes.length);
    writeInt32LE(view, 14 + idBytes.length + topicBytes.length, tokenBytes.length);
    buf.set(tokenBytes, 18 + idBytes.length + topicBytes.length);

    return buf;
}

/**
 * Encode an Unsubscribe frame.
 * Layout: [1:type] [4:topicHash] [4:topicLen] [topic] [4:subscriberIdLen]
 *         [subscriberId] [1:permanent] [4:actorTokenLen] [actorToken]
 */
export function writeUnsubscribe(
    topic: string,
    subscriberId = '',
    permanent = true,
    actorAccessToken = '',
): Uint8Array {
    const topicBytes = UTF8_ENCODER.encode(topic);
    const idBytes = UTF8_ENCODER.encode(subscriberId);
    const tokenBytes = UTF8_ENCODER.encode(actorAccessToken);
    validateStringBytes(topicBytes, MAX_TOPIC_SIZE, 'topic', false);
    validateStringBytes(idBytes, MAX_TOPIC_SIZE, 'subscriberId', true);
    validateStringBytes(tokenBytes, MAX_ACTOR_TOKEN_SIZE, 'actorAccessToken', true);
    const totalSize = 1 + 4 + 4 + topicBytes.length + 4 + idBytes.length + 1 + 4 + tokenBytes.length;
    const [buf, view] = createFrame(totalSize);

    writeUint8(view, 0, FrameType.Unsubscribe);
    writeInt32LE(view, 1, fnv1aHash(topic));
    writeInt32LE(view, 5, topicBytes.length);
    buf.set(topicBytes, 9);
    writeInt32LE(view, 9 + topicBytes.length, idBytes.length);
    buf.set(idBytes, 13 + topicBytes.length);
    writeUint8(view, 13 + topicBytes.length + idBytes.length, permanent ? 0x01 : 0x00);
    writeInt32LE(view, 14 + topicBytes.length + idBytes.length, tokenBytes.length);
    buf.set(tokenBytes, 18 + topicBytes.length + idBytes.length);

    return buf;
}

/**
 * Encode a Publish frame.
 * Layout: [1:type] [4:topicHash] [1:flags] [4:topicLen] [topic] [4:payloadLen] [payload]
 */
export function writePublish(topic: string, durableEligible: boolean, payload: Uint8Array): Uint8Array {
    const topicBytes = UTF8_ENCODER.encode(topic);
    validateStringBytes(topicBytes, MAX_TOPIC_SIZE, 'topic', false);
    const totalSize = HEADER_SIZE.Publish + topicBytes.length + payload.length;
    const [buf, view] = createFrame(totalSize);

    writeUint8(view, 0, FrameType.Publish);
    writeInt32LE(view, 1, fnv1aHash(topic));
    writeUint8(view, 5, durableEligible ? 0x01 : 0x00);
    writeInt32LE(view, 6, topicBytes.length);
    buf.set(topicBytes, 10);
    writeInt32LE(view, 10 + topicBytes.length, payload.length);
    buf.set(payload, 14 + topicBytes.length);

    return buf;
}

/**
 * Encode an Ack frame.
 * Layout: [1:type] [4:topicHash] [4:topicLen] [topic] [4:subscriberIdLen]
 *         [subscriberId] [8:upToSequence] [4:actorTokenLen] [actorToken]
 */
export function writeAck(
    topic: string,
    subscriberId: string,
    upToSequenceNumber: bigint | number,
    actorAccessToken = '',
): Uint8Array {
    const topicBytes = UTF8_ENCODER.encode(topic);
    const idBytes = UTF8_ENCODER.encode(subscriberId);
    const tokenBytes = UTF8_ENCODER.encode(actorAccessToken);
    validateStringBytes(topicBytes, MAX_TOPIC_SIZE, 'topic', false);
    validateStringBytes(idBytes, MAX_TOPIC_SIZE, 'subscriberId', true);
    validateStringBytes(tokenBytes, MAX_ACTOR_TOKEN_SIZE, 'actorAccessToken', true);
    const totalSize = 1 + 4 + 4 + topicBytes.length + 4 + idBytes.length + 8 + 4 + tokenBytes.length;
    const [buf, view] = createFrame(totalSize);

    writeUint8(view, 0, FrameType.Ack);
    writeInt32LE(view, 1, fnv1aHash(topic));
    writeInt32LE(view, 5, topicBytes.length);
    buf.set(topicBytes, 9);
    writeInt32LE(view, 9 + topicBytes.length, idBytes.length);
    buf.set(idBytes, 13 + topicBytes.length);
    writeInt64LE(view, 13 + topicBytes.length + idBytes.length, upToSequenceNumber);
    writeInt32LE(view, 21 + topicBytes.length + idBytes.length, tokenBytes.length);
    buf.set(tokenBytes, 25 + topicBytes.length + idBytes.length);

    return buf;
}

/**
 * Read a Publish frame.
 */
export function readPublish(data: Uint8Array): PublishFrameData | null {
    if (!isReadableFrame(data, HEADER_SIZE.Publish)) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const topicHash = readInt32LE(view, 1);
    const durableEligible = (readUint8(view, 5) & 0x01) !== 0;
    const topicLen = readInt32LE(view, 6);
    if (topicLen <= 0 || topicLen > 4096 || data.length < 10 + topicLen + 4) return null;

    const topic = UTF8_DECODER.decode(data.subarray(10, 10 + topicLen));
    if (fnv1aHash(topic) !== topicHash) return null;

    const payloadLen = readInt32LE(view, 10 + topicLen);
    const payloadOffset = HEADER_SIZE.Publish + topicLen;
    if (payloadLen < 0 || data.length < payloadOffset + payloadLen) return null;

    return {
        topicHash,
        topic,
        durableEligible,
        payload: data.subarray(payloadOffset, payloadOffset + payloadLen),
    };
}

/**
 * Read an Event frame.
 */
export function readEvent(data: Uint8Array): EventFrameData | null {
    if (!isReadableFrame(data, HEADER_SIZE.Event)) return null;

    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const topicHash = readInt32LE(view, 1);
    const sequenceNumber = readInt64LE(view, 5);
    const isReplay = (readUint8(view, 13) & 0x01) !== 0;
    const subscriberIdLen = readInt32LE(view, 14);
    if (subscriberIdLen < 0 || subscriberIdLen > 4096 || data.length < 18 + subscriberIdLen + 4) return null;

    const subscriberId = UTF8_DECODER.decode(data.subarray(18, 18 + subscriberIdLen));
    const payloadLen = readInt32LE(view, 18 + subscriberIdLen);
    const payloadOffset = HEADER_SIZE.Event + subscriberIdLen;
    if (payloadLen < 0 || data.length < payloadOffset + payloadLen) return null;

    return {
        topicHash,
        sequenceNumber,
        isReplay,
        subscriberId,
        payload: data.subarray(payloadOffset, payloadOffset + payloadLen),
    };
}

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
    const [buf, view] = createFrame(totalSize);

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
    if (!isReadableFrame(data, HEADER_SIZE.StreamOpen)) return null;
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
    if (!isReadableFrame(data, HEADER_SIZE.StreamData)) return null;
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const payloadLen = readInt32LE(view, 17);
    if (payloadLen < 0 || data.length < HEADER_SIZE.StreamData + payloadLen) return null;
    return {
        streamId: bytesToGuid(data, 1),
        payload: data.subarray(21, 21 + payloadLen),
    };
}

/**
 * Read a StreamClose frame.
 */
export function readStreamClose(data: Uint8Array): StreamCloseData | null {
    if (!isReadableFrame(data, HEADER_SIZE.StreamClose)) return null;
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
    if (missingSequences.length > 0xFFFF) {
        throw new RangeError('NACK requests cannot contain more than 65535 sequence numbers.');
    }
    const totalSize = HEADER_SIZE.NackRequest + missingSequences.length * 4;
    const [buf, view] = createFrame(totalSize);

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
    if (!isReadableFrame(data, HEADER_SIZE.NackRequest)) return null;

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
