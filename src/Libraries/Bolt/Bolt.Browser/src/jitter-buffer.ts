/**
 * Adaptive jitter buffer for ordered frame playback in the browser.
 *
 * Reorders out-of-order frames and adds adaptive delay to smooth
 * network jitter. Matches the .NET MediaJitterBuffer behavior.
 *
 * - Holds frames in a sorted buffer until their playout time
 * - Adapts target delay based on observed inter-arrival jitter (EWA)
 * - Drops frames that arrive too late (older than buffer window)
 * - Configurable for audio (20ms frames, tighter delay) or video (33ms)
 */

export interface BufferedFrame {
  sequenceNumber: number;
  timestamp: number;
  data: Uint8Array;
  isKeyframe: boolean;
  arrivalTime: number;
}

export interface JitterBufferOptions {
  /** true for audio (20ms frame interval), false for video (33ms) */
  isAudio: boolean;
  /** Maximum number of frames to buffer (default 10) */
  maxBufferSize?: number;
  /** Minimum target delay in ms (default: audio=20, video=40) */
  minDelayMs?: number;
  /** Maximum target delay in ms (default: audio=200, video=300) */
  maxDelayMs?: number;
}

export class JitterBuffer {
  private buffer: BufferedFrame[] = [];
  private readonly maxBufferSize: number;
  private readonly minDelayMs: number;
  private readonly maxDelayMs: number;
  private readonly frameIntervalMs: number;

  private targetDelayMs: number;
  private jitterSmoothed = 0;
  private lastArrivalTime = 0;
  private lastExpectedInterval: number;

  private playbackTimer: ReturnType<typeof setInterval> | null = null;

  /** Called when a frame is ready for playback (in order, after jitter delay). */
  public onFrame?: (frame: BufferedFrame) => void;

  constructor(options: JitterBufferOptions) {
    this.maxBufferSize = options.maxBufferSize ?? 10;
    this.frameIntervalMs = options.isAudio ? 20 : 33;
    this.minDelayMs = options.minDelayMs ?? (options.isAudio ? 20 : 40);
    this.maxDelayMs = options.maxDelayMs ?? (options.isAudio ? 200 : 300);
    this.targetDelayMs = this.minDelayMs * 2;
    this.lastExpectedInterval = this.frameIntervalMs;
  }

  /** Start the playback timer. Frames are delivered via onFrame callback. */
  start(): void {
    if (this.playbackTimer) return;
    this.playbackTimer = setInterval(() => this.playNext(), this.frameIntervalMs);
  }

  /** Stop the playback timer. */
  stop(): void {
    if (this.playbackTimer) {
      clearInterval(this.playbackTimer);
      this.playbackTimer = null;
    }
  }

  /**
   * Enqueue a received frame. The buffer sorts by sequence number
   * and delivers frames in order after the adaptive jitter delay.
   */
  enqueue(frame: BufferedFrame): void {
    const now = performance.now();
    frame.arrivalTime = now;

    // Update jitter estimate
    if (this.lastArrivalTime > 0) {
      const interArrival = now - this.lastArrivalTime;
      const deviation = Math.abs(interArrival - this.lastExpectedInterval);
      // EWA smoothing (alpha = 1/16 per RFC 3550)
      this.jitterSmoothed += (deviation - this.jitterSmoothed) / 16;

      // Adapt target delay: 2x smoothed jitter + base, clamped
      this.targetDelayMs = Math.max(
        this.minDelayMs,
        Math.min(this.maxDelayMs, this.jitterSmoothed * 2 + this.minDelayMs)
      );
    }
    this.lastArrivalTime = now;

    // Insert sorted by sequence number
    let inserted = false;
    for (let i = 0; i < this.buffer.length; i++) {
      if (frame.sequenceNumber < this.buffer[i].sequenceNumber) {
        this.buffer.splice(i, 0, frame);
        inserted = true;
        break;
      }
      // Duplicate — drop
      if (frame.sequenceNumber === this.buffer[i].sequenceNumber) return;
    }
    if (!inserted) {
      this.buffer.push(frame);
    }

    // Drop oldest if buffer full
    while (this.buffer.length > this.maxBufferSize) {
      this.buffer.shift();
    }
  }

  /** Get current buffer depth (frames waiting). */
  get depth(): number {
    return this.buffer.length;
  }

  /** Get current adaptive target delay in ms. */
  get currentDelayMs(): number {
    return this.targetDelayMs;
  }

  /** Get smoothed jitter estimate in ms. */
  get jitterMs(): number {
    return this.jitterSmoothed;
  }

  private playNext(): void {
    if (this.buffer.length === 0) return;

    const now = performance.now();
    const oldest = this.buffer[0];

    // Check if the oldest frame has waited long enough (jitter delay)
    const waitTime = now - oldest.arrivalTime;
    if (waitTime >= this.targetDelayMs) {
      this.buffer.shift();
      this.onFrame?.(oldest);
    }
  }

  /** Flush all buffered frames immediately (e.g., on stream close). */
  flush(): void {
    while (this.buffer.length > 0) {
      const frame = this.buffer.shift()!;
      this.onFrame?.(frame);
    }
  }

  /** Clear all buffered frames without delivering them. */
  clear(): void {
    this.buffer.length = 0;
  }

  dispose(): void {
    this.stop();
    this.clear();
  }
}
