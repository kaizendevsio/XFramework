import {
  FRAME, SIGNAL,
  writeRegister, writeCallSignal, writeMediaConfig, writeMediaFeedback,
  writeMediaKeyRequest, writeNackRequest,
  writeRequest, writeResponse, writePush,
  writeStreamOpen, writeStreamData, writeStreamClose,
  readFrameType, readCallSignal, readMediaFrame, readMediaConfig,
  readMediaFeedback, readMediaKeyRequest, readFecFrame, readNackRequest,
  readRequest, readResponse, readStreamOpen, readStreamData, readStreamClose,
  readRegisterAck, guidToBytes, newGuid, fnv1aHash,
  type QualityHintValue, QualityHint,
} from './protocol';
import { BoltBrowserMediaStream } from './media-stream';
import { MediaCrypto } from './encryption';

export interface CallInfo {
  callId: string;
  isOutgoing: boolean;
  remoteClientId: string;
  audioStreamId?: string;
  videoStreamId?: string;
  keySent?: boolean;
}

/** Pending RPC call awaiting a Response frame. */
interface PendingRpc {
  resolve: (result: { statusCode: number; payload: Uint8Array }) => void;
  reject: (error: Error) => void;
  timer: ReturnType<typeof setTimeout>;
}

/** Browser-side byte stream (file transfer, etc.). */
export interface BoltBrowserStream {
  streamId: string;
  send(data: Uint8Array): void;
  close(statusCode?: number): void;
  onData?: (data: Uint8Array) => void;
  onClose?: (statusCode: number) => void;
}

/**
 * Browser WebSocket client for the Bolt protocol.
 *
 * Full-featured client supporting:
 * - RPC messaging (invoke/respond for text chat, delivery confirmation)
 * - Push (fire-and-forget for typing indicators, presence)
 * - Bidirectional streaming (file/media sharing)
 * - Voice/video calls with E2E encryption, ABR, FEC, NACK
 * - Auto-reconnection with exponential backoff
 */
export class BoltBrowserClient {
  private ws: WebSocket | null = null;
  private readonly serverUrl: string;
  private readonly clientId: string;
  private readonly clientName: string;

  private mediaStreams = new Map<string, BoltBrowserMediaStream>();
  private activeCalls = new Map<string, CallInfo>();
  private callEncryption = new Map<string, MediaCrypto>();
  private connected = false;
  private disposed = false;

  // RPC
  private pendingRpcs = new Map<string, PendingRpc>();
  private hashCache = new Map<string, number>();
  private rpcTimeoutMs = 30_000;

  // Streaming
  private activeStreams = new Map<string, BoltBrowserStream>();

  // Reconnection
  private reconnectAttempt = 0;
  private maxReconnectAttempts = 50;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private autoReconnect = true;

  // Handler registry — maps command hash to handler
  private handlers = new Map<number, (payload: Uint8Array, requestId: string) => Promise<{ statusCode: number; payload: Uint8Array }>>();
  private streamHandlers = new Map<number, (stream: BoltBrowserStream) => void>();

  // Events — media
  public onIncomingCall?: (callId: string, callerClientId: string) => void;
  public onCallAnswered?: (callId: string) => void;
  public onCallRejected?: (callId: string) => void;
  public onCallEnded?: (callId: string) => void;
  public onKeyframeRequested?: (streamId: string) => void;
  public onCallHold?: (callId: string) => void;
  public onCallUnhold?: (callId: string) => void;
  public onParticipantAdded?: (callId: string, payload: Uint8Array) => void;
  public onParticipantRemoved?: (callId: string, payload: Uint8Array) => void;

  // Events — connection
  public onConnected?: () => void;
  public onDisconnected?: () => void;
  public onReconnecting?: (attempt: number) => void;

  // Events — messaging
  public onPush?: (commandHash: number, payload: Uint8Array) => void;

  get isConnected(): boolean { return this.connected; }

  constructor(serverUrl: string, clientId: string, clientName: string) {
    this.serverUrl = serverUrl;
    this.clientId = clientId;
    this.clientName = clientName;
  }

  // ── Connection ─────────────────────────────────────────────

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

        if (!this.connected) {
          if (readRegisterAck(data)) {
            this.connected = true;
            this.reconnectAttempt = 0;
            this.onConnected?.();
            resolve();
          } else {
            reject(new Error('Registration failed'));
          }
          return;
        }

        this.handleMessage(data);
      };

      this.ws.onerror = () => {
        if (!this.connected) reject(new Error('WebSocket error'));
      };

      this.ws.onclose = () => {
        const wasConnected = this.connected;
        this.connected = false;
        this.onDisconnected?.();

        // Auto-reconnect if was connected and not intentionally disposed
        if (wasConnected && this.autoReconnect && !this.disposed) {
          this.scheduleReconnect();
        }
      };
    });
  }

  /** Connect with automatic retry and exponential backoff. */
  async connectWithRetry(): Promise<void> {
    for (let attempt = 0; attempt < this.maxReconnectAttempts; attempt++) {
      try {
        await this.connect();
        return;
      } catch {
        if (attempt >= this.maxReconnectAttempts - 1) throw new Error('Max reconnection attempts reached');
        const delay = Math.min(500 * Math.pow(2, attempt), 30_000);
        const jitter = Math.random() * delay * 0.3;
        await new Promise(r => setTimeout(r, delay + jitter));
      }
    }
  }

  private scheduleReconnect(): void {
    if (this.reconnectAttempt >= this.maxReconnectAttempts) return;

    this.reconnectAttempt++;
    const delay = Math.min(500 * Math.pow(2, this.reconnectAttempt - 1), 30_000);
    const jitter = Math.random() * delay * 0.3;

    this.onReconnecting?.(this.reconnectAttempt);

    this.reconnectTimer = setTimeout(async () => {
      try {
        await this.connect();
      } catch {
        // Will trigger onclose → scheduleReconnect again
      }
    }, delay + jitter);
  }

  /** Disconnect from the hub. */
  disconnect(): void {
    this.disposed = true;
    this.autoReconnect = false;
    if (this.reconnectTimer) clearTimeout(this.reconnectTimer);

    // Reject all pending RPCs
    for (const [, rpc] of this.pendingRpcs) {
      clearTimeout(rpc.timer);
      rpc.reject(new Error('Disconnected'));
    }
    this.pendingRpcs.clear();

    this.ws?.close();
    this.ws = null;
    this.connected = false;
    this.mediaStreams.clear();
    this.activeCalls.clear();
    this.callEncryption.clear();
    this.activeStreams.clear();
  }

  // ── RPC Messaging ──────────────────────────────────────────

  /**
   * Invoke a method on a remote service and wait for the response.
   * This is how you send chat messages, request delivery confirmations, etc.
   *
   * Usage: const result = await client.invoke('IdentityServer', 'AuthenticateIdentity', payload);
   */
  async invoke(recipientId: string, commandName: string, payload: Uint8Array): Promise<{ statusCode: number; payload: Uint8Array }> {
    if (!this.connected || !this.ws) throw new Error('Not connected');

    const requestId = newGuid();
    const recipientHash = this.getHash(recipientId);
    const commandHash = this.getHash(commandName);

    const frame = writeRequest(requestId, recipientHash, commandHash, payload);

    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pendingRpcs.delete(requestId);
        reject(new Error(`RPC timeout after ${this.rpcTimeoutMs}ms`));
      }, this.rpcTimeoutMs);

      this.pendingRpcs.set(requestId, { resolve, reject, timer });
      this.ws!.send(frame);
    });
  }

  /**
   * Register a handler for incoming RPC requests.
   * When a remote client sends a Request with this command name, the handler is called.
   *
   * Usage: client.registerHandler('SendMessage', async (payload, requestId) => {
   *   return { statusCode: 200, payload: new Uint8Array(0) };
   * });
   */
  registerHandler(commandName: string, handler: (payload: Uint8Array, requestId: string) => Promise<{ statusCode: number; payload: Uint8Array }>): void {
    const hash = this.getHash(commandName);
    this.handlers.set(hash, handler);
  }

  /**
   * Send a fire-and-forget push message (no response expected).
   * Use for typing indicators, presence updates, read receipts, etc.
   *
   * Usage: client.push('user_123', 'TypingIndicator', payload);
   */
  push(recipientId: string, commandName: string, payload: Uint8Array): void {
    if (!this.connected || !this.ws) return;

    const recipientHash = this.getHash(recipientId);
    const commandHash = this.getHash(commandName);
    const frame = writePush(recipientHash, commandHash, payload);
    this.ws.send(frame);
  }

  // ── Streaming (file transfer) ──────────────────────────────

  /**
   * Open a bidirectional stream to a remote service for file/data transfer.
   *
   * Usage:
   *   const stream = client.openStream('FileService', 'UploadFile');
   *   stream.send(chunk1);
   *   stream.send(chunk2);
   *   stream.close();
   */
  openStream(recipientId: string, commandName: string): BoltBrowserStream {
    if (!this.connected || !this.ws) throw new Error('Not connected');

    const streamId = newGuid();
    const recipientHash = this.getHash(recipientId);
    const commandHash = this.getHash(commandName);

    const ws = this.ws;
    const stream: BoltBrowserStream = {
      streamId,
      send(data: Uint8Array) {
        ws.send(writeStreamData(streamId, data));
      },
      close(statusCode = 200) {
        ws.send(writeStreamClose(streamId, statusCode));
      },
    };

    this.activeStreams.set(streamId, stream);

    // Send StreamOpen
    ws.send(writeStreamOpen(streamId, recipientHash, commandHash));

    return stream;
  }

  /**
   * Register a handler for incoming streams.
   *
   * Usage: client.registerStreamHandler('UploadFile', (stream) => {
   *   stream.onData = (chunk) => { ... };
   *   stream.onClose = (status) => { ... };
   * });
   */
  registerStreamHandler(commandName: string, handler: (stream: BoltBrowserStream) => void): void {
    const hash = this.getHash(commandName);
    this.streamHandlers.set(hash, handler);
  }

  // ── Call API (unchanged) ───────────────────────────────────

  startCall(recipientId: string, video = false, encrypted = true): string {
    const callId = crypto.randomUUID();
    this.activeCalls.set(callId, { callId, isOutgoing: true, remoteClientId: recipientId });

    if (encrypted) {
      const enc = new MediaCrypto();
      enc.init().then(() => this.callEncryption.set(callId, enc));
    }

    const hash = fnv1aHash(recipientId);
    const payload = new Uint8Array(4);
    new DataView(payload.buffer).setInt32(0, hash, true);
    this.ws?.send(writeCallSignal(callId, SIGNAL.Initiate, payload));
    return callId;
  }

  answerCall(callId: string): void {
    const call = this.activeCalls.get(callId);
    if (call) call.isOutgoing = false;
    this.ws?.send(writeCallSignal(callId, SIGNAL.Answer, new Uint8Array(0)));
  }

  rejectCall(callId: string): void {
    this.activeCalls.delete(callId);
    this.callEncryption.delete(callId);
    this.ws?.send(writeCallSignal(callId, SIGNAL.Reject, new Uint8Array(0)));
  }

  endCall(callId: string): void {
    this.activeCalls.delete(callId);
    this.callEncryption.delete(callId);
    for (const [streamId, stream] of this.mediaStreams) {
      if (stream.callId === callId) this.mediaStreams.delete(streamId);
    }
    this.ws?.send(writeCallSignal(callId, SIGNAL.End, new Uint8Array(0)));
  }

  getMediaStream(streamId: string): BoltBrowserMediaStream | undefined {
    return this.mediaStreams.get(streamId);
  }

  sendMediaConfig(streamId: string, callId: string, isAudio: boolean, bitrateKbps: number): BoltBrowserMediaStream {
    this.ws?.send(writeMediaConfig(streamId, callId,
      isAudio ? 0x01 : 0x02, isAudio ? 0x01 : 0x02,
      isAudio ? 48000 : 1280, isAudio ? 1 : 720,
      bitrateKbps, 0, new Uint8Array(0)));
    const stream = new BoltBrowserMediaStream(this.ws!, streamId, callId, isAudio);
    this.mediaStreams.set(streamId, stream);
    const enc = this.callEncryption.get(callId);
    if (enc?.isReady) stream.setEncryption(enc);
    return stream;
  }

  sendScreenShareConfig(streamId: string, callId: string, width = 1920, height = 1080, bitrateKbps = 3000): BoltBrowserMediaStream {
    this.ws?.send(writeMediaConfig(streamId, callId, 0x03, 0x02, width, height, bitrateKbps, 0, new Uint8Array(0)));
    const stream = new BoltBrowserMediaStream(this.ws!, streamId, callId, false);
    this.mediaStreams.set(streamId, stream);
    const enc = this.callEncryption.get(callId);
    if (enc?.isReady) stream.setEncryption(enc);
    return stream;
  }

  // ── Message dispatcher ─────────────────────────────────────

  private handleMessage(data: Uint8Array): void {
    const frameType = readFrameType(data);

    switch (frameType) {
      // RPC
      case FRAME.Response: {
        const resp = readResponse(data);
        if (resp) {
          const rpc = this.pendingRpcs.get(resp.requestId);
          if (rpc) {
            clearTimeout(rpc.timer);
            this.pendingRpcs.delete(resp.requestId);
            rpc.resolve({ statusCode: resp.statusCode, payload: resp.payload });
          }
        }
        break;
      }
      case FRAME.Request: {
        const req = readRequest(data);
        if (req) this.handleIncomingRequest(req.commandHash, req.payload, req.requestId);
        break;
      }
      case FRAME.Push: {
        const req = readRequest(data); // Push has same layout as Request
        if (req) {
          const handler = this.handlers.get(req.commandHash);
          if (handler) handler(req.payload, req.requestId);
          else this.onPush?.(req.commandHash, req.payload);
        }
        break;
      }

      // Streaming
      case FRAME.StreamOpen: {
        const so = readStreamOpen(data);
        if (so) this.handleStreamOpen(so.streamId, so.commandHash);
        break;
      }
      case FRAME.StreamData: {
        const sd = readStreamData(data);
        if (sd) {
          const stream = this.activeStreams.get(sd.streamId);
          stream?.onData?.(sd.payload);
        }
        break;
      }
      case FRAME.StreamClose: {
        const sc = readStreamClose(data);
        if (sc) {
          const stream = this.activeStreams.get(sc.streamId);
          stream?.onClose?.(sc.statusCode);
          this.activeStreams.delete(sc.streamId);
        }
        break;
      }

      // Media
      case FRAME.MediaFrame: {
        const mf = readMediaFrame(data);
        if (mf) this.mediaStreams.get(mf.streamId)?.enqueueFrame(mf.sequenceNumber, mf.timestamp, mf.payload, mf.flags);
        break;
      }
      case FRAME.MediaConfig: {
        const mc = readMediaConfig(data);
        if (mc) {
          const isAudio = mc.mediaType === 0x01;
          const stream = new BoltBrowserMediaStream(this.ws!, mc.streamId, mc.callId, isAudio);
          this.mediaStreams.set(mc.streamId, stream);
          const enc = this.callEncryption.get(mc.callId);
          if (enc?.isReady) stream.setEncryption(enc);
        }
        break;
      }
      case FRAME.MediaFeedback: {
        const fb = readMediaFeedback(data);
        if (fb) this.mediaStreams.get(fb.streamId)?.handleFeedback(fb.qualityHint);
        break;
      }
      case FRAME.MediaKeyRequest: {
        const kr = readMediaKeyRequest(data);
        if (kr) this.onKeyframeRequested?.(kr.streamId);
        break;
      }
      case FRAME.FecFrame: {
        const fec = readFecFrame(data);
        if (fec) this.mediaStreams.get(fec.streamId)?.enqueueFecFrame(fec.fecGroupStart, fec.fecGroupSize, fec.payload);
        break;
      }
      case FRAME.NackRequest: {
        const nack = readNackRequest(data);
        if (nack) this.mediaStreams.get(nack.streamId)?.handleNackRequest(nack.missingSequences);
        break;
      }
      case FRAME.CallSignal: {
        const cs = readCallSignal(data);
        if (cs) this.handleCallSignal(cs.callId, cs.signalType, cs.payload);
        break;
      }
    }
  }

  // ── RPC handling ───────────────────────────────────────────

  private async handleIncomingRequest(commandHash: number, payload: Uint8Array, requestId: string): Promise<void> {
    const handler = this.handlers.get(commandHash);
    if (handler) {
      try {
        const result = await handler(payload, requestId);
        const frame = writeResponse(requestId, result.statusCode, result.payload);
        this.ws?.send(frame);
      } catch {
        const frame = writeResponse(requestId, 500, new Uint8Array(0));
        this.ws?.send(frame);
      }
    } else {
      const frame = writeResponse(requestId, 501, new Uint8Array(0));
      this.ws?.send(frame);
    }
  }

  // ── Stream handling ────────────────────────────────────────

  private handleStreamOpen(streamId: string, commandHash: number): void {
    const ws = this.ws!;
    const stream: BoltBrowserStream = {
      streamId,
      send(data: Uint8Array) { ws.send(writeStreamData(streamId, data)); },
      close(statusCode = 200) { ws.send(writeStreamClose(streamId, statusCode)); },
    };
    this.activeStreams.set(streamId, stream);

    const handler = this.streamHandlers.get(commandHash);
    if (handler) handler(stream);
  }

  // ── Call signaling ─────────────────────────────────────────

  private handleCallSignal(callId: string, signalType: number, payload: Uint8Array): void {
    switch (signalType) {
      case SIGNAL.Initiate:
        this.activeCalls.set(callId, { callId, isOutgoing: false, remoteClientId: '' });
        this.onIncomingCall?.(callId, '');
        break;
      case SIGNAL.Ring: break;
      case SIGNAL.Answer:
        this.onCallAnswered?.(callId);
        this.sendKeyExchange(callId);
        break;
      case SIGNAL.Reject:
        this.activeCalls.delete(callId);
        this.callEncryption.delete(callId);
        this.onCallRejected?.(callId);
        break;
      case SIGNAL.End:
        this.activeCalls.delete(callId);
        this.callEncryption.delete(callId);
        for (const [streamId, stream] of this.mediaStreams) {
          if (stream.callId === callId) this.mediaStreams.delete(streamId);
        }
        this.onCallEnded?.(callId);
        break;
      case SIGNAL.Hold: this.onCallHold?.(callId); break;
      case SIGNAL.Unhold: this.onCallUnhold?.(callId); break;
      case SIGNAL.AddParticipant: this.onParticipantAdded?.(callId, payload); break;
      case SIGNAL.RemoveParticipant: this.onParticipantRemoved?.(callId, payload); break;
      case SIGNAL.KeyExchange: this.handleKeyExchange(callId, payload); break;
    }
  }

  private async sendKeyExchange(callId: string): Promise<void> {
    let enc = this.callEncryption.get(callId);
    if (!enc) {
      enc = new MediaCrypto();
      await enc.init();
      this.callEncryption.set(callId, enc);
    }
    this.ws?.send(writeCallSignal(callId, SIGNAL.KeyExchange, enc.getPublicKey()));
  }

  private async handleKeyExchange(callId: string, remotePublicKey: Uint8Array): Promise<void> {
    let enc = this.callEncryption.get(callId);
    if (!enc) {
      enc = new MediaCrypto();
      await enc.init();
      this.callEncryption.set(callId, enc);
    }
    await enc.deriveKey(remotePublicKey, callId);

    const call = this.activeCalls.get(callId);
    if (call && !call.isOutgoing && !call.keySent) {
      call.keySent = true;
      await this.sendKeyExchange(callId);
    }

    for (const [, stream] of this.mediaStreams) {
      if (stream.callId === callId) stream.setEncryption(enc);
    }
  }

  // ── Helpers ────────────────────────────────────────────────

  private getHash(value: string): number {
    let hash = this.hashCache.get(value);
    if (hash === undefined) {
      hash = fnv1aHash(value);
      this.hashCache.set(value, hash);
    }
    return hash;
  }
}
