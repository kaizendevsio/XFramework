import {
  writeMediaFrame, writeMediaFeedback, writeMediaKeyRequest, writeNackRequest,
  MediaFrameFlags, QualityHint,
  type QualityHintValue,
} from './protocol';
import type { MediaCrypto } from './encryption';

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
 * - E2E encryption (AES-256-GCM via MediaCrypto)
 * - FEC (XOR parity receive + recovery)
 * - Adaptive bitrate feedback (send MediaFeedback every 250ms)
 * - NACK retransmission (gap detection + retransmit buffer)
 */
export class BoltBrowserMediaStream {
  private ws: WebSocket;
  private nextSeq = 0;
  private timestampCounter = 0;
  private readonly timestampIncrement: number;

  public readonly streamId: string;
  public readonly callId: string;
  public readonly isAudio: boolean;

  // Encryption
  private encryption: MediaCrypto | null = null;

  // Feedback (receiver → sender ABR)
  private highestSeqReceived = 0;
  private cumulativeLost = 0;
  private lastArrivalTime = 0;
  private jitterSmoothed = 0;
  private feedbackInterval: ReturnType<typeof setInterval> | null = null;

  // FEC decoder
  private fecGroups = new Map<number, { frames: Map<number, Uint8Array>; parity?: Uint8Array; groupSize: number; lengths?: number[] }>();

  // NACK
  private missingSeqs = new Set<number>();
  private nackedSeqs = new Set<number>();
  private retransmitBuffer = new Map<number, { ts: number; flags: number; payload: Uint8Array }>();
  private nackInterval: ReturnType<typeof setInterval> | null = null;
  private lastReceivedSeq = -1;

  /** Callback for received frames */
  public onFrame?: (event: MediaFrameEvent) => void;
  /** Callback when ABR feedback suggests bitrate change */
  public onBitrateChange?: (qualityHint: QualityHintValue) => void;

  constructor(ws: WebSocket, streamId: string, callId: string, isAudio: boolean) {
    this.ws = ws;
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
  }

  /**
   * Send an encoded media frame to the remote peer.
   * Sequence numbers and timestamps auto-increment.
   * Encrypts payload if encryption is active.
   */
  async sendFrame(encodedData: Uint8Array, isKeyframe = false): Promise<void> {
    const seq = this.nextSeq++;
    const ts = this.timestampCounter;
    this.timestampCounter += this.timestampIncrement;

    let flags = 0;
    if (isKeyframe) flags |= MediaFrameFlags.Keyframe;

    let payload = encodedData;
    if (this.encryption?.isReady) {
      flags |= MediaFrameFlags.Encrypted;
      payload = await this.encryption.encrypt(encodedData, seq, this.streamId);
    }

    // Store in retransmit buffer (keep last 256 frames)
    this.retransmitBuffer.set(seq, { ts, flags, payload });
    if (this.retransmitBuffer.size > 256) {
      const oldest = seq - 256;
      this.retransmitBuffer.delete(oldest);
    }

    const frame = writeMediaFrame(this.streamId, seq, ts, flags, payload);
    this.ws.send(frame);
  }

  /**
   * Called internally when a MediaFrame arrives from the receive loop.
   * Handles decryption, gap detection, and FEC registration.
   */
  async enqueueFrame(seq: number, timestamp: number, data: Uint8Array, flags: number): Promise<void> {
    const isKeyframe = (flags & MediaFrameFlags.Keyframe) !== 0;
    const isEncrypted = (flags & MediaFrameFlags.Encrypted) !== 0;

    // Track for ABR feedback
    this.trackReceived(seq);

    // Decrypt if needed
    let frameData = data;
    if (isEncrypted && this.encryption?.isReady) {
      try {
        frameData = await this.encryption.decrypt(data, seq, this.streamId);
      } catch {
        return; // Corrupted/tampered frame
      }
    }

    // Deliver to consumer
    this.onFrame?.({ sequenceNumber: seq, timestamp, data: frameData, isKeyframe });

    // Register with FEC for potential recovery
    this.registerFecFrame(seq, data);

    // Detect gaps for NACK
    if (this.lastReceivedSeq >= 0) {
      for (let missing = this.lastReceivedSeq + 1; missing < seq; missing++) {
        this.missingSeqs.add(missing);
      }
    }
    this.lastReceivedSeq = seq;
    this.missingSeqs.delete(seq);
    this.nackedSeqs.delete(seq);
  }

  /** Called when FEC parity frame arrives. */
  enqueueFecFrame(groupStart: number, groupSize: number, payload: Uint8Array): void {
    // Parse lengths from payload prefix
    const lengths: number[] = [];
    const view = new DataView(payload.buffer, payload.byteOffset, payload.byteLength);
    for (let i = 0; i < groupSize; i++) {
      lengths.push(view.getInt32(i * 4, true));
    }
    const parityData = payload.slice(groupSize * 4);

    let group = this.fecGroups.get(groupStart);
    if (!group) {
      group = { frames: new Map(), groupSize, lengths };
      this.fecGroups.set(groupStart, group);
    }
    group.parity = parityData;
    group.lengths = lengths;

    // Try recovery
    this.tryFecRecovery(groupStart);
  }

  /** Handle incoming ABR feedback (sender side). */
  handleFeedback(qualityHint: QualityHintValue): void {
    this.onBitrateChange?.(qualityHint);
  }

  /** Handle NACK request — resend missing frames from retransmit buffer. */
  handleNackRequest(missingSequences: number[]): void {
    for (const seq of missingSequences) {
      const entry = this.retransmitBuffer.get(seq);
      if (entry) {
        const frame = writeMediaFrame(this.streamId, seq, entry.ts, entry.flags, entry.payload);
        this.ws.send(frame);
      }
    }
  }

  /** Clean up timers. */
  dispose(): void {
    if (this.feedbackInterval) clearInterval(this.feedbackInterval);
    if (this.nackInterval) clearInterval(this.nackInterval);
    this.feedbackInterval = null;
    this.nackInterval = null;
  }

  // ── Private: ABR feedback ──

  private trackReceived(seq: number): void {
    const now = performance.now();

    if (seq > this.highestSeqReceived) {
      // Detect gaps
      if (this.highestSeqReceived > 0 && seq > this.highestSeqReceived + 1) {
        this.cumulativeLost += seq - this.highestSeqReceived - 1;
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
    if (this.highestSeqReceived === 0) return;

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
    this.ws.send(frame);
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
    const pending: number[] = [];
    for (const seq of this.missingSeqs) {
      if (!this.nackedSeqs.has(seq)) pending.push(seq);
    }
    if (pending.length === 0) return;

    pending.sort((a, b) => a - b);
    const toSend = pending.slice(0, 64); // Max 64 per request

    for (const seq of toSend) {
      this.nackedSeqs.add(seq);
    }

    const frame = writeNackRequest(this.streamId, toSend);
    this.ws.send(frame);

    // Prune old entries
    const cutoff = this.highestSeqReceived - 512;
    for (const seq of this.missingSeqs) {
      if (seq < cutoff) this.missingSeqs.delete(seq);
    }
    for (const seq of this.nackedSeqs) {
      if (seq < cutoff) this.nackedSeqs.delete(seq);
    }
  }

  // ── Private: FEC recovery ──

  private registerFecFrame(seq: number, data: Uint8Array): void {
    // Find which FEC group this frame belongs to
    for (const [groupStart, group] of this.fecGroups) {
      if (seq >= groupStart && seq < groupStart + group.groupSize) {
        group.frames.set(seq, data);
        this.tryFecRecovery(groupStart);
        return;
      }
    }
    // No group yet — store for later matching
    // Create a provisional group entry (will be completed when parity arrives)
  }

  private tryFecRecovery(groupStart: number): void {
    const group = this.fecGroups.get(groupStart);
    if (!group?.parity || !group.lengths) return;

    // Need exactly groupSize-1 frames to recover 1 missing
    const missing: number[] = [];
    for (let i = 0; i < group.groupSize; i++) {
      if (!group.frames.has(groupStart + i)) missing.push(groupStart + i);
    }

    if (missing.length !== 1) return; // Can only recover exactly 1 frame

    const missingSeq = missing[0];
    const missingIdx = missingSeq - groupStart;

    // XOR all present frames + parity to recover
    const maxLen = group.parity.length;
    const recovered = new Uint8Array(maxLen);
    recovered.set(group.parity);

    for (const [seq, data] of group.frames) {
      for (let i = 0; i < Math.min(data.length, maxLen); i++) {
        recovered[i] ^= data[i];
      }
    }

    // Truncate to original length
    const originalLen = group.lengths[missingIdx];
    const result = recovered.slice(0, originalLen);

    // Deliver recovered frame
    this.missingSeqs.delete(missingSeq);
    this.nackedSeqs.delete(missingSeq);
    this.onFrame?.({ sequenceNumber: missingSeq, timestamp: 0, data: result, isKeyframe: false });

    // Cleanup group
    this.fecGroups.delete(groupStart);
  }
}
