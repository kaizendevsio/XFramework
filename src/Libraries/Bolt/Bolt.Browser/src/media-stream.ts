import {
  writeMediaFrame, writeMediaFeedback, writeMediaKeyRequest, writeNackRequest,
  MediaFrameFlags, QualityHint,
  type QualityHintValue,
} from './protocol.js';
import type { MediaCrypto } from './encryption.js';

const MAX_NACKS_PER_REQUEST = 64;
const MAX_TRACKED_GAP = 512;
const MAX_RETRANSMIT_FRAMES = 256;
const MAX_FEC_GROUP_SIZE = 32;
const MAX_FEC_GROUPS = 32;
const MAX_FEC_FRAMES = 512;
const FEC_RETENTION_MS = 2_000;
const UINT32_HALF_RANGE = 0x80000000;

function forwardDistance(from: number, to: number): number {
  return (to - from) >>> 0;
}

function isNewer(candidate: number, reference: number): boolean {
  const distance = forwardDistance(reference, candidate);
  return distance > 0 && distance < UINT32_HALF_RANGE;
}

export interface MediaFrameEvent {
  sequenceNumber: number;
  timestamp: number;
  data: Uint8Array;
  isKeyframe: boolean;
}

/**
 * Media stream for sending/receiving encoded audio/video frames.
 *
 * Supports:
 * - Optional payload encryption when supplied with authenticated key material
 * - FEC (XOR parity receive + recovery)
 * - Adaptive bitrate feedback (send MediaFeedback every 250ms)
 * - NACK retransmission (gap detection + retransmit buffer)
 */
export class BoltBrowserMediaStream {
  private ws: WebSocket;
  private readonly sendWireFrame: (frame: Uint8Array) => void;
  private nextSeq = 0;
  private timestampCounter = 0;
  private readonly timestampIncrement: number;

  public readonly streamId: string;
  public readonly callId: string;
  public readonly isAudio: boolean;

  // Encryption
  private encryption: MediaCrypto | null = null;
  private encryptionRequired = false;

  // Feedback (receiver → sender ABR)
  private highestSeqReceived = 0;
  private cumulativeLost = 0;
  private lastArrivalTime = 0;
  private jitterSmoothed = 0;
  private feedbackInterval: ReturnType<typeof setInterval> | null = null;

  // FEC decoder
  private fecGroups = new Map<number, { parity: Uint8Array; groupSize: number; lengths: number[]; createdAt: number }>();
  private fecFrames = new Map<number, { data: Uint8Array; timestamp: number; flags: number; createdAt: number }>();

  // NACK
  private missingSeqs = new Set<number>();
  private nackedSeqs = new Set<number>();
  private retransmitBuffer = new Map<number, { ts: number; flags: number; payload: Uint8Array }>();
  private nackInterval: ReturnType<typeof setInterval> | null = null;
  private lastReceivedSeq = -1;
  private disposed = false;

  /** Callback for received frames */
  public onFrame?: (event: MediaFrameEvent) => void;
  /** Callback when ABR feedback suggests bitrate change */
  public onBitrateChange?: (qualityHint: QualityHintValue) => void;

  constructor(
    ws: WebSocket,
    streamId: string,
    callId: string,
    isAudio: boolean,
    sendWireFrame?: (frame: Uint8Array) => void,
  ) {
    this.ws = ws;
    this.sendWireFrame = sendWireFrame ?? (frame => ws.send(frame));
    this.streamId = streamId;
    this.callId = callId;
    this.isAudio = isAudio;
    this.timestampIncrement = isAudio ? 960 : 3000;

    // Start ABR feedback loop (250ms)
    this.feedbackInterval = setInterval(() => this.sendFeedback(), 250);

    // Start NACK check loop (50ms)
    this.nackInterval = setInterval(() => this.sendNacks(), 50);
  }

  /** Set encryption for this stream. */
  setEncryption(enc: MediaCrypto): void {
    this.encryption = enc;
    this.encryptionRequired = true;
  }

  /**
   * Send an encoded media frame to the remote peer.
   * Sequence numbers and timestamps auto-increment.
   * Encrypts payload if encryption is active.
   */
  async sendFrame(encodedData: Uint8Array, isKeyframe = false): Promise<void> {
    if (this.disposed || this.ws.readyState !== WebSocket.OPEN) throw new Error('Media stream is closed');

    const seq = this.nextSeq;
    this.nextSeq = (this.nextSeq + 1) >>> 0;
    const ts = this.timestampCounter;
    this.timestampCounter = (this.timestampCounter + this.timestampIncrement) >>> 0;

    let flags = 0;
    if (isKeyframe) flags |= MediaFrameFlags.Keyframe;

    let payload = encodedData;
    if (this.encryptionRequired) {
      if (!this.encryption?.isReady) {
        throw new Error('Media encryption is required but no ready authenticated key is configured');
      }
      flags |= MediaFrameFlags.Encrypted;
      payload = await this.encryption.encrypt(encodedData, seq, this.streamId);
    }

    // Store in retransmit buffer (keep last 256 frames)
    this.retransmitBuffer.set(seq, { ts, flags, payload });
    if (this.retransmitBuffer.size > MAX_RETRANSMIT_FRAMES) {
      const oldest = this.retransmitBuffer.keys().next().value as number | undefined;
      if (oldest !== undefined) this.retransmitBuffer.delete(oldest);
    }

    const frame = writeMediaFrame(this.streamId, seq, ts, flags, payload);
    this.sendWireFrame(frame);
  }

  /**
   * Called internally when a MediaFrame arrives from the receive loop.
   * Handles decryption, gap detection, and FEC registration.
   */
  async enqueueFrame(seq: number, timestamp: number, data: Uint8Array, flags: number): Promise<void> {
    if (this.disposed) return;

    seq >>>= 0;
    timestamp >>>= 0;
    const isKeyframe = (flags & MediaFrameFlags.Keyframe) !== 0;
    const isEncrypted = (flags & MediaFrameFlags.Encrypted) !== 0;

    if (this.encryptionRequired && !isEncrypted) return;
    if (isEncrypted && !this.encryption?.isReady) return;

    // Track for ABR feedback
    this.trackReceived(seq);

    // Decrypt if needed
    let frameData = data;
    if (isEncrypted) {
      try {
        frameData = await this.encryption!.decrypt(data, seq, this.streamId);
      } catch {
        return; // Corrupted/tampered frame
      }
    }

    // Deliver to consumer
    this.onFrame?.({ sequenceNumber: seq, timestamp, data: frameData, isKeyframe });

    // Register with FEC for potential recovery
    this.registerFecFrame(seq, timestamp, flags, data);

    // Detect gaps for NACK
    if (this.lastReceivedSeq >= 0 && isNewer(seq, this.lastReceivedSeq)) {
      const gap = forwardDistance(this.lastReceivedSeq, seq);
      if (gap <= MAX_TRACKED_GAP + 1) {
        for (let offset = 1; offset < gap; offset++) {
          this.missingSeqs.add((this.lastReceivedSeq + offset) >>> 0);
        }
      }
      this.lastReceivedSeq = seq;
    } else if (this.lastReceivedSeq < 0) {
      this.lastReceivedSeq = seq;
    }
    this.missingSeqs.delete(seq);
    this.nackedSeqs.delete(seq);
  }

  /** Called when FEC parity frame arrives. */
  enqueueFecFrame(groupStart: number, groupSize: number, payload: Uint8Array): void {
    if (this.disposed || groupSize < 2 || groupSize > MAX_FEC_GROUP_SIZE) return;
    groupStart >>>= 0;

    const lengthsSize = groupSize * 4;
    if (payload.byteLength < lengthsSize) return;

    // Parse lengths from payload prefix
    const lengths: number[] = [];
    const view = new DataView(payload.buffer, payload.byteOffset, payload.byteLength);
    for (let i = 0; i < groupSize; i++) {
      const length = view.getInt32(i * 4, true);
      if (length < 0 || length > payload.byteLength - lengthsSize) return;
      lengths.push(length);
    }
    const parityData = payload.slice(lengthsSize);

    this.pruneFecState();
    this.fecGroups.set(groupStart, { parity: parityData, groupSize, lengths, createdAt: performance.now() });
    while (this.fecGroups.size > MAX_FEC_GROUPS) {
      const oldest = this.fecGroups.keys().next().value as number | undefined;
      if (oldest === undefined) break;
      this.fecGroups.delete(oldest);
    }

    // Try recovery
    void this.tryFecRecovery(groupStart);
  }

  /** Handle incoming ABR feedback (sender side). */
  handleFeedback(qualityHint: QualityHintValue): void {
    this.onBitrateChange?.(qualityHint);
  }

  /** Handle NACK request — resend missing frames from retransmit buffer. */
  handleNackRequest(missingSequences: number[]): void {
    if (this.disposed || this.ws.readyState !== WebSocket.OPEN) return;

    const bounded = [...new Set(missingSequences
      .filter(seq => Number.isInteger(seq) && seq >= 0 && seq <= 0xffffffff)
      .map(seq => seq >>> 0))]
      .slice(0, MAX_NACKS_PER_REQUEST);
    for (const seq of bounded) {
      const entry = this.retransmitBuffer.get(seq);
      if (entry) {
        const frame = writeMediaFrame(this.streamId, seq, entry.ts, entry.flags, entry.payload);
        this.sendWireFrame(frame);
      }
    }
  }

  /** Clean up timers. */
  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    if (this.feedbackInterval) clearInterval(this.feedbackInterval);
    if (this.nackInterval) clearInterval(this.nackInterval);
    this.feedbackInterval = null;
    this.nackInterval = null;
    this.fecGroups.clear();
    this.fecFrames.clear();
    this.missingSeqs.clear();
    this.nackedSeqs.clear();
    this.retransmitBuffer.clear();
    this.onFrame = undefined;
    this.onBitrateChange = undefined;
  }

  // ── Private: ABR feedback ──

  private trackReceived(seq: number): void {
    const now = performance.now();

    if (isNewer(seq, this.highestSeqReceived)) {
      // Detect gaps
      const gap = forwardDistance(this.highestSeqReceived, seq);
      if (this.highestSeqReceived > 0 && gap > 1 && gap <= MAX_TRACKED_GAP + 1) {
        this.cumulativeLost += gap - 1;
      }
      this.highestSeqReceived = seq;
    }

    // Jitter (RFC 3550 smoothing)
    if (this.lastArrivalTime > 0) {
      const interval = now - this.lastArrivalTime;
      const expected = this.isAudio ? 20 : 33;
      const deviation = Math.abs(interval - expected);
      this.jitterSmoothed += (deviation - this.jitterSmoothed) / 16;
    }
    this.lastArrivalTime = now;
  }

  private sendFeedback(): void {
    if (this.disposed || this.highestSeqReceived === 0 || this.ws.readyState !== WebSocket.OPEN) return;

    const jitterX100 = Math.round(this.jitterSmoothed * 100);
    const hint = this.determineQualityHint();

    const frame = writeMediaFeedback(
      this.streamId,
      this.highestSeqReceived,
      this.cumulativeLost,
      jitterX100,
      0, // RTT not measured client-side
      hint
    );
    this.sendWireFrame(frame);
  }

  private determineQualityHint(): QualityHintValue {
    if (this.highestSeqReceived > 0) {
      const lossRate = this.cumulativeLost / (this.highestSeqReceived + 1);
      if (lossRate > 0.10) return QualityHint.KeyframeNeeded;
      if (lossRate > 0.03 || this.jitterSmoothed > 50) return QualityHint.Decrease;
    }
    if (this.jitterSmoothed < 10 && this.cumulativeLost === 0 && this.highestSeqReceived > 100) {
      return QualityHint.Increase;
    }
    return QualityHint.Maintain;
  }

  // ── Private: NACK ──

  private sendNacks(): void {
    if (this.disposed || this.ws.readyState !== WebSocket.OPEN) return;

    for (const seq of this.missingSeqs) {
      const age = forwardDistance(seq, this.highestSeqReceived);
      if (age > MAX_TRACKED_GAP && age < UINT32_HALF_RANGE) {
        this.missingSeqs.delete(seq);
        this.nackedSeqs.delete(seq);
      }
    }

    const pending: number[] = [];
    for (const seq of this.missingSeqs) {
      if (!this.nackedSeqs.has(seq)) pending.push(seq);
    }
    if (pending.length === 0) return;

    pending.sort((a, b) => forwardDistance(b, this.highestSeqReceived) - forwardDistance(a, this.highestSeqReceived));
    const toSend = pending.slice(0, MAX_NACKS_PER_REQUEST);

    for (const seq of toSend) {
      this.nackedSeqs.add(seq);
    }

    const frame = writeNackRequest(this.streamId, toSend);
    this.sendWireFrame(frame);

  }

  // ── Private: FEC recovery ──

  private registerFecFrame(seq: number, timestamp: number, flags: number, data: Uint8Array): void {
    this.pruneFecState();
    this.fecFrames.set(seq, { data: data.slice(), timestamp, flags, createdAt: performance.now() });
    while (this.fecFrames.size > MAX_FEC_FRAMES) {
      const oldest = this.fecFrames.keys().next().value as number | undefined;
      if (oldest === undefined) break;
      this.fecFrames.delete(oldest);
    }

    for (const [groupStart, group] of this.fecGroups) {
      if (forwardDistance(groupStart, seq) < group.groupSize) {
        void this.tryFecRecovery(groupStart);
      }
    }
  }

  private async tryFecRecovery(groupStart: number): Promise<void> {
    const group = this.fecGroups.get(groupStart);
    if (!group) return;

    // Need exactly groupSize-1 frames to recover 1 missing
    const missing: number[] = [];
    for (let i = 0; i < group.groupSize; i++) {
      const sequence = (groupStart + i) >>> 0;
      if (!this.fecFrames.has(sequence)) missing.push(sequence);
    }

    if (missing.length !== 1) return; // Can only recover exactly 1 frame

    const missingSeq = missing[0];
    const missingIdx = forwardDistance(groupStart, missingSeq);

    // XOR all present frames + parity to recover
    const maxLen = group.parity.length;
    const recovered = new Uint8Array(maxLen);
    recovered.set(group.parity);

    let reference: { sequence: number; timestamp: number } | undefined;
    for (let i = 0; i < group.groupSize; i++) {
      const sequence = (groupStart + i) >>> 0;
      if (sequence === missingSeq) continue;
      const frame = this.fecFrames.get(sequence);
      if (!frame) return;
      reference ??= { sequence, timestamp: frame.timestamp };
      for (let index = 0; index < Math.min(frame.data.length, maxLen); index++) {
        recovered[index] ^= frame.data[index];
      }
    }

    // Truncate to original length
    const originalLen = group.lengths[missingIdx];
    const result = recovered.slice(0, originalLen);

    let frameData: Uint8Array<ArrayBufferLike> = result;
    if (this.encryptionRequired) {
      if (!this.encryption?.isReady) return;
      try {
        frameData = await this.encryption.decrypt(result, missingSeq, this.streamId);
      } catch {
        return;
      }
    }

    this.missingSeqs.delete(missingSeq);
    this.nackedSeqs.delete(missingSeq);
    const timestamp = reference
      ? (reference.timestamp + ((missingSeq - reference.sequence) | 0) * this.timestampIncrement) >>> 0
      : 0;
    this.onFrame?.({ sequenceNumber: missingSeq, timestamp, data: frameData, isKeyframe: false });

    this.fecGroups.delete(groupStart);
    for (let i = 0; i < group.groupSize; i++) {
      this.fecFrames.delete((groupStart + i) >>> 0);
    }
  }

  private pruneFecState(): void {
    const cutoff = performance.now() - FEC_RETENTION_MS;
    for (const [sequence, frame] of this.fecFrames) {
      if (frame.createdAt <= cutoff) this.fecFrames.delete(sequence);
    }
    for (const [groupStart, group] of this.fecGroups) {
      if (group.createdAt <= cutoff) this.fecGroups.delete(groupStart);
    }
  }
}
