import {
  FRAME, SIGNAL,
  writeRegister, writeCallSignal, writeMediaConfig,
  readFrameType, readCallSignal, readMediaFrame, readMediaConfig, readRegisterAck,
  guidToBytes
} from './protocol';
import { BoltBrowserMediaStream } from './media-stream';

export interface CallInfo {
  callId: string;
  isOutgoing: boolean;
  remoteClientId: string;
  audioStreamId?: string;
  videoStreamId?: string;
}

/**
 * Browser WebSocket client for the Bolt protocol.
 * Connects to a Bolt hub and supports voice/video calls.
 */
export class BoltBrowserClient {
  private ws: WebSocket | null = null;
  private readonly serverUrl: string;
  private readonly clientId: string;
  private readonly clientName: string;

  private mediaStreams = new Map<string, BoltBrowserMediaStream>();
  private activeCalls = new Map<string, CallInfo>();
  private connected = false;

  // Events
  public onIncomingCall?: (callId: string, callerClientId: string) => void;
  public onCallAnswered?: (callId: string) => void;
  public onCallRejected?: (callId: string) => void;
  public onCallEnded?: (callId: string) => void;
  public onConnected?: () => void;
  public onDisconnected?: () => void;

  constructor(serverUrl: string, clientId: string, clientName: string) {
    this.serverUrl = serverUrl;
    this.clientId = clientId;
    this.clientName = clientName;
  }

  /** Connect to the Bolt hub and register. */
  async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.ws = new WebSocket(this.serverUrl);
      this.ws.binaryType = 'arraybuffer';

      this.ws.onopen = () => {
        const registerFrame = writeRegister(this.clientId, this.clientName);
        this.ws!.send(registerFrame);
      };

      this.ws.onmessage = (event) => {
        const data = new Uint8Array(event.data as ArrayBuffer);

        // First message should be RegisterAck
        if (!this.connected) {
          if (readRegisterAck(data)) {
            this.connected = true;
            this.onConnected?.();
            resolve();
          } else {
            reject(new Error('Registration failed'));
          }
          return;
        }

        this.handleMessage(data);
      };

      this.ws.onerror = (err) => {
        if (!this.connected) reject(new Error('WebSocket error'));
      };

      this.ws.onclose = () => {
        this.connected = false;
        this.onDisconnected?.();
      };
    });
  }

  /** Initiate a call to a recipient. Returns the call ID. */
  startCall(recipientId: string, video = false): string {
    const callId = crypto.randomUUID();
    this.activeCalls.set(callId, {
      callId, isOutgoing: true, remoteClientId: recipientId
    });

    // Payload: 4-byte recipientHash (FNV-1a)
    const hash = fnv1aHash(recipientId);
    const payload = new Uint8Array(4);
    new DataView(payload.buffer).setInt32(0, hash, true);

    const frame = writeCallSignal(callId, SIGNAL.Initiate, payload);
    this.ws?.send(frame);
    return callId;
  }

  /** Answer an incoming call. */
  answerCall(callId: string): void {
    const call = this.activeCalls.get(callId);
    if (call) call.isOutgoing = false;
    const frame = writeCallSignal(callId, SIGNAL.Answer, new Uint8Array(0));
    this.ws?.send(frame);
  }

  /** Reject an incoming call. */
  rejectCall(callId: string): void {
    this.activeCalls.delete(callId);
    const frame = writeCallSignal(callId, SIGNAL.Reject, new Uint8Array(0));
    this.ws?.send(frame);
  }

  /** End an active call. */
  endCall(callId: string): void {
    this.activeCalls.delete(callId);
    // Clean up media streams for this call
    for (const [streamId, stream] of this.mediaStreams) {
      if (stream.callId === callId) {
        this.mediaStreams.delete(streamId);
      }
    }
    const frame = writeCallSignal(callId, SIGNAL.End, new Uint8Array(0));
    this.ws?.send(frame);
  }

  /** Get a media stream by ID. */
  getMediaStream(streamId: string): BoltBrowserMediaStream | undefined {
    return this.mediaStreams.get(streamId);
  }

  /** Send a media config to set up a track. */
  sendMediaConfig(streamId: string, callId: string, isAudio: boolean, bitrateKbps: number): BoltBrowserMediaStream {
    const frame = writeMediaConfig(
      streamId, callId,
      isAudio ? 0x01 : 0x02,  // mediaType
      isAudio ? 0x01 : 0x02,  // codecId (Opus / H264)
      isAudio ? 48000 : 1280, // sampleRate / width
      isAudio ? 1 : 720,      // channels / height
      bitrateKbps, 0, new Uint8Array(0)
    );
    this.ws?.send(frame);

    const stream = new BoltBrowserMediaStream(this.ws!, streamId, callId, isAudio);
    this.mediaStreams.set(streamId, stream);
    return stream;
  }

  /** Disconnect from the hub. */
  disconnect(): void {
    this.ws?.close();
    this.ws = null;
    this.connected = false;
    this.mediaStreams.clear();
    this.activeCalls.clear();
  }

  private handleMessage(data: Uint8Array): void {
    const frameType = readFrameType(data);

    switch (frameType) {
      case FRAME.MediaFrame: {
        const mf = readMediaFrame(data);
        if (mf) {
          const stream = this.mediaStreams.get(mf.streamId);
          stream?.enqueueFrame(mf.sequenceNumber, mf.timestamp, mf.payload, mf.flags);
        }
        break;
      }
      case FRAME.MediaConfig: {
        const mc = readMediaConfig(data);
        if (mc) {
          const isAudio = mc.mediaType === 0x01;
          const stream = new BoltBrowserMediaStream(this.ws!, mc.streamId, mc.callId, isAudio);
          this.mediaStreams.set(mc.streamId, stream);
        }
        break;
      }
      case FRAME.CallSignal: {
        const cs = readCallSignal(data);
        if (cs) this.handleCallSignal(cs.callId, cs.signalType, cs.payload);
        break;
      }
    }
  }

  private handleCallSignal(callId: string, signalType: number, _payload: Uint8Array): void {
    switch (signalType) {
      case SIGNAL.Initiate:
        this.activeCalls.set(callId, { callId, isOutgoing: false, remoteClientId: '' });
        this.onIncomingCall?.(callId, '');
        break;
      case SIGNAL.Ring:
        // Call is ringing on the other end
        break;
      case SIGNAL.Answer:
        this.onCallAnswered?.(callId);
        break;
      case SIGNAL.Reject:
        this.activeCalls.delete(callId);
        this.onCallRejected?.(callId);
        break;
      case SIGNAL.End:
        this.activeCalls.delete(callId);
        for (const [streamId, stream] of this.mediaStreams) {
          if (stream.callId === callId) this.mediaStreams.delete(streamId);
        }
        this.onCallEnded?.(callId);
        break;
    }
  }
}

/** FNV-1a hash matching BoltCodec.Fnv1aHash */
function fnv1aHash(value: string): number {
  let hash = 0x811c9dc5 >>> 0;
  for (let i = 0; i < value.length; i++) {
    hash ^= value.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash | 0; // Convert to signed int32
}
