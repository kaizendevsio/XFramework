import { writeMediaFrame, FRAME } from './protocol';

export interface MediaFrameEvent {
  sequenceNumber: number;
  timestamp: number;
  data: Uint8Array;
  isKeyframe: boolean;
}

/**
 * Media stream for sending/receiving encoded audio/video frames.
 */
export class BoltBrowserMediaStream {
  private ws: WebSocket;
  private nextSeq = 0;
  private timestampCounter = 0;
  private readonly timestampIncrement: number;

  public readonly streamId: string;
  public readonly callId: string;
  public readonly isAudio: boolean;

  /** Callback for received frames */
  public onFrame?: (event: MediaFrameEvent) => void;

  constructor(ws: WebSocket, streamId: string, callId: string, isAudio: boolean) {
    this.ws = ws;
    this.streamId = streamId;
    this.callId = callId;
    this.isAudio = isAudio;
    this.timestampIncrement = isAudio ? 960 : 3000; // Opus 48kHz/20ms or 30fps at 90kHz
  }

  /**
   * Send an encoded media frame to the remote peer.
   * Sequence numbers and timestamps auto-increment.
   */
  sendFrame(encodedData: Uint8Array, isKeyframe = false): void {
    const seq = this.nextSeq++;
    const ts = this.timestampCounter;
    this.timestampCounter += this.timestampIncrement;

    let flags = 0;
    if (isKeyframe) flags |= 0x01;

    const frame = writeMediaFrame(this.streamId, seq, ts, flags, encodedData);
    this.ws.send(frame);
  }

  /**
   * Called internally when a MediaFrame arrives from the receive loop.
   */
  enqueueFrame(seq: number, timestamp: number, data: Uint8Array, flags: number): void {
    const isKeyframe = (flags & 0x01) !== 0;
    this.onFrame?.({ sequenceNumber: seq, timestamp, data, isKeyframe });
  }
}
