import {
  FRAME, SIGNAL,
  writeRegister, writeCallSignal, writeMediaConfig, writeMediaFeedback,
  writeMediaKeyRequest, writeNackRequest,
  writeRequest, writeResponse, writePush,
  writeRequestCancel,
  writeSubscribe, writeUnsubscribe, writePublish, writeAck,
  writeStreamOpen, writeStreamData, writeStreamClose,
  readFrameType, readCallSignal, readMediaFrame, readMediaConfig,
  readMediaFeedback, readMediaKeyRequest, readFecFrame, readNackRequest,
  readEvent,
  readRequest, readResponse, readStreamOpen, readStreamData, readStreamClose,
  readRequestCancel,
  readRegisterAckDetails, readBatch, guidToBytes, newGuid, fnv1aHash,
  WIRE_VERSION, writeBatch,
  type QualityHintValue, QualityHint, MAX_FRAME_SIZE, MAX_BATCH_FRAMES, MAX_BATCH_BYTES,
} from './protocol.js';
import { BoltBrowserMediaStream } from './media-stream.js';

const NON_CANCELABLE_SIGNAL = new AbortController().signal;

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
  cleanup?: () => void;
}

/** Browser-side byte stream (file transfer, etc.). */
export interface BoltBrowserStream {
  streamId: string;
  send(data: Uint8Array): void;
  close(statusCode?: number): void;
  onData?: (data: Uint8Array) => void;
  onClose?: (statusCode: number) => void;
}

export interface BoltBrowserEvent {
  topicHash: number;
  sequenceNumber: bigint;
  isReplay: boolean;
  subscriberId: string;
  payload: Uint8Array;
}

/**
 * Browser WebSocket client for the Bolt protocol.
 *
 * Full-featured client supporting:
 * - RPC request/response frames for service calls and delivery confirmation
 * - Push (fire-and-forget for typing indicators, presence)
 * - Bidirectional streaming (file/media sharing)
 * - Experimental voice/video APIs with ABR, FEC, and NACK support
 * - Auto-reconnection with exponential backoff
 */
export class BoltBrowserClient {
  private ws: WebSocket | null = null;
  private readonly serverUrl: string;
  private readonly clientId: string;
  private readonly clientName: string;

  private mediaStreams = new Map<string, BoltBrowserMediaStream>();
  private activeCalls = new Map<string, CallInfo>();
  private connected = false;
  private disposed = false;

  // RPC
  private pendingRpcs = new Map<string, PendingRpc>();
  private inboundRequestControllers = new Map<string, AbortController>();
  private hashCache = new Map<string, number>();
  private rpcTimeoutMs = 30_000;
  private registrationTimeoutMs = 10_000;

  // Streaming
  private activeStreams = new Map<string, BoltBrowserStream>();

  // Non-media frames are coalesced within one microtask. Media always flushes
  // this queue first so its wire order stays identical to call order.
  private outboundFrames: Uint8Array[] = [];
  private outboundBytes = 0;
  private outboundFlushScheduled = false;

  // Reconnection
  private reconnectAttempt = 0;
  private maxReconnectAttempts = 50;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private autoReconnect = true;

  // Handler registry — maps command hash to handler
  private handlers = new Map<number, (payload: Uint8Array, requestId: string, signal: AbortSignal) => Promise<{ statusCode: number; payload: Uint8Array }>>();
  private streamHandlers = new Map<number, (stream: BoltBrowserStream) => void>();

  // Events — media
  public onIncomingCall?: (callId: string, callerClientId: string) => void;
  public onCallAnswered?: (callId: string) => void;
  public onCallRejected?: (callId: string) => void;
  public onCallEnded?: (callId: string) => void;
  public onKeyframeRequested?: (streamId: string) => void;
  public onMediaStreamConfigured?: (stream: BoltBrowserMediaStream) => void;
  public onCallHold?: (callId: string) => void;
  public onCallUnhold?: (callId: string) => void;
  public onParticipantAdded?: (callId: string, payload: Uint8Array) => void;
  public onParticipantRemoved?: (callId: string, payload: Uint8Array) => void;

  // Events — connection
  public onConnected?: () => void;
  public onDisconnected?: () => void;
  public onReconnecting?: (attempt: number) => void;

  // Events — push frames
  public onPush?: (commandHash: number, payload: Uint8Array) => void;

  // Events — pub/sub frames
  public onEvent?: (event: BoltBrowserEvent) => void;

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
      const registrationTimer = setTimeout(() => {
        this.ws?.close(1002, 'Bolt registration timeout');
        reject(new Error(`Bolt registration timed out after ${this.registrationTimeoutMs}ms`));
      }, this.registrationTimeoutMs);
      const clearRegistrationTimer = () => clearTimeout(registrationTimer);

      this.ws.onopen = () => {
        const registerFrame = writeRegister(this.clientId, this.clientName);
        this.ws!.send(registerFrame);
      };

      this.ws.onmessage = (event) => {
        const data = new Uint8Array(event.data as ArrayBuffer);

        try {
          if (!this.connected) {
            const ack = readRegisterAckDetails(data);
            if (data[0] === FRAME.RegisterAck && !ack) {
              clearRegistrationTimer();
              this.ws?.close(1002, 'Invalid Bolt registration response');
              reject(new Error('Invalid Bolt registration response'));
            } else if (ack && ack.version !== WIRE_VERSION) {
              clearRegistrationTimer();
              this.ws?.close(1002, 'Bolt wire version mismatch');
              reject(new Error(`Bolt wire version mismatch: expected ${WIRE_VERSION}, received ${ack.version}`));
            } else if (ack?.success) {
              clearRegistrationTimer();
              this.connected = true;
              this.reconnectAttempt = 0;
              this.onConnected?.();
              resolve();
            } else {
              clearRegistrationTimer();
              this.ws?.close(1002, 'Invalid Bolt registration response');
              reject(new Error('Registration failed'));
            }
            return;
          }

          if (!this.handleMessage(data)) {
            const error = new Error('Malformed Bolt frame');
            this.failActiveWork(error);
            this.disposeMediaState();
            this.ws?.close(1002, 'Malformed Bolt frame');
          }
        } catch (error) {
          clearRegistrationTimer();
          this.ws?.close(data.length > MAX_FRAME_SIZE ? 1009 : 1002, 'Invalid Bolt frame');
          this.failActiveWork(error instanceof Error ? error : new Error('Invalid Bolt frame'));
          this.disposeMediaState();
        }
      };

      this.ws.onerror = () => {
        if (!this.connected) {
          clearRegistrationTimer();
          reject(new Error('WebSocket error'));
        }
      };

      this.ws.onclose = () => {
        clearRegistrationTimer();
        const wasConnected = this.connected;
        this.connected = false;
        this.failActiveWork(new Error('Bolt connection closed'));
        this.disposeMediaState();
        this.onDisconnected?.();
        if (!wasConnected) reject(new Error('Bolt connection closed before registration'));

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

    this.failActiveWork(new Error('Disconnected'));

    this.ws?.close();
    this.ws = null;
    this.connected = false;
    this.disposeMediaState();
  }

  private failActiveWork(error: Error): void {
    this.clearOutboundFrames();
    for (const rpc of this.pendingRpcs.values()) {
      clearTimeout(rpc.timer);
      rpc.cleanup?.();
      rpc.reject(error);
    }
    this.pendingRpcs.clear();
    for (const controller of this.inboundRequestControllers.values()) controller.abort();
    this.inboundRequestControllers.clear();
    for (const stream of this.activeStreams.values()) stream.onClose?.(503);
    this.activeStreams.clear();
  }

  private clearOutboundFrames(): void {
    this.outboundFrames.length = 0;
    this.outboundBytes = 0;
    this.outboundFlushScheduled = false;
  }

  private sendFrame(frame: Uint8Array, media = false): void {
    const ws = this.ws;
    if (!ws || ws.readyState !== WebSocket.OPEN) return;

    if (media) {
      this.flushOutboundFrames();
      ws.send(frame);
      return;
    }

    this.outboundFrames.push(frame);
    this.outboundBytes += frame.length + 4;
    if (!this.outboundFlushScheduled) {
      this.outboundFlushScheduled = true;
      queueMicrotask(() => {
        this.outboundFlushScheduled = false;
        this.flushOutboundFrames();
      });
    }
  }

  private flushOutboundFrames(): void {
    const ws = this.ws;
    if (!ws || ws.readyState !== WebSocket.OPEN) {
      this.clearOutboundFrames();
      return;
    }

    while (this.outboundFrames.length > 0) {
      if (this.outboundFrames.length === 1) {
        const frame = this.outboundFrames.shift()!;
        ws.send(frame);
        this.outboundBytes -= frame.length + 4;
        continue;
      }

      let count = 0;
      let batchBytes = 1 + 4;
      while (count < this.outboundFrames.length && count < MAX_BATCH_FRAMES) {
        const nextBytes = batchBytes + 4 + this.outboundFrames[count].length;
        if (nextBytes > MAX_BATCH_BYTES) break;
        batchBytes = nextBytes;
        count++;
      }

      if (count >= 2) {
        ws.send(writeBatch(this.outboundFrames.splice(0, count)));
        this.outboundBytes -= batchBytes - 5;
      } else {
        const frame = this.outboundFrames.shift()!;
        ws.send(frame);
        this.outboundBytes -= frame.length + 4;
      }
    }
    this.outboundBytes = 0;
  }

  private disposeMediaState(): void {
    for (const stream of this.mediaStreams.values()) stream.dispose();
    this.mediaStreams.clear();
    this.activeCalls.clear();
  }

  // ── RPC request/response ───────────────────────────────────

  /**
   * Invoke a method on a remote service and wait for the response.
   * This is how you send chat messages, request delivery confirmations, etc.
   *
   * Usage: const result = await client.invoke('IdentityServer', 'AuthenticateIdentity', payload);
   */
  async invoke(
    recipientId: string,
    commandName: string,
    payload: Uint8Array,
    signal?: AbortSignal,
  ): Promise<{ statusCode: number; payload: Uint8Array }> {
    if (!this.connected || !this.ws) throw new Error('Not connected');
    if (signal?.aborted) throw this.createAbortError();

    const requestId = newGuid();
    const recipientHash = this.getHash(recipientId);
    const commandHash = this.getHash(commandName);

    const senderHash = this.getHash(this.clientId);
    const frame = writeRequest(requestId, recipientHash, senderHash, commandHash, payload);

    return new Promise((resolve, reject) => {
      const cleanup = () => signal?.removeEventListener('abort', abort);
      const cancelRemote = () => {
        if (this.ws?.readyState === WebSocket.OPEN) this.sendFrame(writeRequestCancel(requestId));
      };
      const timer = setTimeout(() => {
        this.pendingRpcs.delete(requestId);
        cleanup();
        cancelRemote();
        reject(new Error(`RPC timeout after ${this.rpcTimeoutMs}ms`));
      }, this.rpcTimeoutMs);
      const abort = () => {
        if (!this.pendingRpcs.delete(requestId)) return;
        clearTimeout(timer);
        cleanup();
        cancelRemote();
        reject(this.createAbortError());
      };

      signal?.addEventListener('abort', abort, { once: true });
      this.pendingRpcs.set(requestId, { resolve, reject, timer, cleanup });
      try {
        this.sendFrame(frame);
      } catch (error) {
        this.pendingRpcs.delete(requestId);
        clearTimeout(timer);
        cleanup();
        reject(error instanceof Error ? error : new Error('Bolt send failed'));
      }
    });
  }

  private createAbortError(): Error {
    const error = new Error('Bolt RPC canceled');
    error.name = 'AbortError';
    return error;
  }

  /**
   * Register a handler for incoming RPC requests.
   * When a remote client sends a Request with this command name, the handler is called.
   *
   * Usage: client.registerHandler('SendMessage', async (payload, requestId) => {
   *   return { statusCode: 200, payload: new Uint8Array(0) };
   * });
   */
  registerHandler(commandName: string, handler: (payload: Uint8Array, requestId: string, signal: AbortSignal) => Promise<{ statusCode: number; payload: Uint8Array }>): void {
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
    if (!recipientId || recipientId.trim().length === 0) {
      throw new TypeError('Bolt Push requires a nonblank recipientId.');
    }
    if (!this.connected || !this.ws) return;

    const recipientHash = this.getHash(recipientId);
    const senderHash = this.getHash(this.clientId);
    const commandHash = this.getHash(commandName);
    const frame = writePush(recipientHash, senderHash, commandHash, payload);
    this.sendFrame(frame);
  }

  // ── Pub/sub ──────────────────────────────────────────────

  subscribe(topic: string, subscriberId = '', durable = false, actorAccessToken = ''): void {
    if (!this.connected || !this.ws) return;
    this.sendFrame(writeSubscribe(topic, subscriberId, durable, actorAccessToken));
  }

  unsubscribe(topic: string, subscriberId = '', permanent = true, actorAccessToken = ''): void {
    if (!this.connected || !this.ws) return;
    this.sendFrame(writeUnsubscribe(topic, subscriberId, permanent, actorAccessToken));
  }

  publish(topic: string, payload: Uint8Array, durableEligible = false): void {
    if (!this.connected || !this.ws) return;
    this.sendFrame(writePublish(topic, durableEligible, payload));
  }

  ack(topic: string, subscriberId: string, upToSequenceNumber: bigint | number, actorAccessToken = ''): void {
    if (!this.connected || !this.ws) return;
    this.sendFrame(writeAck(topic, subscriberId, upToSequenceNumber, actorAccessToken));
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

    const activeStreams = this.activeStreams;
    const sendFrame = (frame: Uint8Array) => this.sendFrame(frame);
    const stream: BoltBrowserStream = {
      streamId,
      send(data: Uint8Array) {
        sendFrame(writeStreamData(streamId, data));
      },
      close(statusCode = 200) {
        sendFrame(writeStreamClose(streamId, statusCode));
        activeStreams.delete(streamId);
      },
    };

    this.activeStreams.set(streamId, stream);

    // Send StreamOpen
    this.sendFrame(writeStreamOpen(streamId, recipientHash, commandHash));

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

  startCall(recipientId: string, video = false, encrypted = false): string {
    if (encrypted) {
      throw new Error('Encrypted Bolt Media calls are disabled until key exchange is bound to authenticated peer identities');
    }

    const callId = crypto.randomUUID();
    this.activeCalls.set(callId, { callId, isOutgoing: true, remoteClientId: recipientId });

    const hash = fnv1aHash(recipientId);
    const payload = new Uint8Array(4);
    new DataView(payload.buffer).setInt32(0, hash, true);
    this.sendFrame(writeCallSignal(callId, SIGNAL.Initiate, payload), true);
    return callId;
  }

  answerCall(callId: string, encrypted = false): void {
    if (encrypted) {
      throw new Error('Encrypted Bolt Media calls are disabled until key exchange is bound to authenticated peer identities');
    }
    const call = this.activeCalls.get(callId);
    if (call) call.isOutgoing = false;
    this.sendFrame(writeCallSignal(callId, SIGNAL.Answer, new Uint8Array(0)), true);
  }

  rejectCall(callId: string): void {
    this.activeCalls.delete(callId);
    this.cleanupMediaStreams(callId);
    this.sendFrame(writeCallSignal(callId, SIGNAL.Reject, new Uint8Array(0)), true);
  }

  endCall(callId: string): void {
    this.activeCalls.delete(callId);
    this.cleanupMediaStreams(callId);
    this.sendFrame(writeCallSignal(callId, SIGNAL.End, new Uint8Array(0)), true);
  }

  getMediaStream(streamId: string): BoltBrowserMediaStream | undefined {
    return this.mediaStreams.get(streamId);
  }

  sendMediaConfig(streamId: string, callId: string, isAudio: boolean, bitrateKbps: number): BoltBrowserMediaStream {
    if (!this.ws || !this.connected) throw new Error('Not connected');
    this.sendFrame(writeMediaConfig(streamId, callId,
      isAudio ? 0x01 : 0x02, isAudio ? 0x01 : 0x02,
      isAudio ? 48000 : 1280, isAudio ? 1 : 720,
      bitrateKbps, 0, new Uint8Array(0)), true);
    this.mediaStreams.get(streamId)?.dispose();
    const stream = new BoltBrowserMediaStream(this.ws, streamId, callId, isAudio,
      frame => this.sendFrame(frame, true));
    this.mediaStreams.set(streamId, stream);
    return stream;
  }

  sendScreenShareConfig(streamId: string, callId: string, width = 1920, height = 1080, bitrateKbps = 3000): BoltBrowserMediaStream {
    if (!this.ws || !this.connected) throw new Error('Not connected');
    this.sendFrame(writeMediaConfig(streamId, callId, 0x03, 0x02, width, height, bitrateKbps, 0, new Uint8Array(0)), true);
    this.mediaStreams.get(streamId)?.dispose();
    const stream = new BoltBrowserMediaStream(this.ws, streamId, callId, false,
      frame => this.sendFrame(frame, true));
    this.mediaStreams.set(streamId, stream);
    return stream;
  }

  // ── Message dispatcher ─────────────────────────────────────

  private handleMessage(data: Uint8Array): boolean {
    const frameType = readFrameType(data);

    if (frameType === FRAME.Batch) {
      const frames = readBatch(data);
      if (!frames) return false;
      for (const frame of frames) {
        if (!this.handleMessage(frame)) return false;
      }
      return true;
    }

    switch (frameType) {
      // RPC
      case FRAME.Response: {
        const resp = readResponse(data);
        if (!resp) return false;
        const rpc = this.pendingRpcs.get(resp.requestId);
        if (rpc) {
          clearTimeout(rpc.timer);
          rpc.cleanup?.();
          this.pendingRpcs.delete(resp.requestId);
          rpc.resolve({ statusCode: resp.statusCode, payload: resp.payload });
        }
        return true;
      }
      case FRAME.Request: {
        const req = readRequest(data);
        if (!req) return false;
        this.handleIncomingRequest(req.commandHash, req.payload, req.requestId);
        return true;
      }
      case FRAME.RequestCancel: {
        const requestId = readRequestCancel(data);
        if (!requestId) return false;
        this.inboundRequestControllers.get(requestId)?.abort();
        return true;
      }
      case FRAME.Push: {
        const req = readRequest(data); // Push has same layout as Request
        if (!req) return false;
        const handler = this.handlers.get(req.commandHash);
        if (handler) handler(req.payload, req.requestId, NON_CANCELABLE_SIGNAL);
        else this.onPush?.(req.commandHash, req.payload);
        return true;
      }
      case FRAME.Event: {
        const evt = readEvent(data);
        if (!evt) return false;
        this.onEvent?.(evt);
        return true;
      }

      // Streaming
      case FRAME.StreamOpen: {
        const so = readStreamOpen(data);
        if (!so) return false;
        this.handleStreamOpen(so.streamId, so.commandHash);
        return true;
      }
      case FRAME.StreamData: {
        const sd = readStreamData(data);
        if (!sd) return false;
        const stream = this.activeStreams.get(sd.streamId);
        stream?.onData?.(sd.payload);
        return true;
      }
      case FRAME.StreamClose: {
        const sc = readStreamClose(data);
        if (!sc) return false;
        const stream = this.activeStreams.get(sc.streamId);
        stream?.onClose?.(sc.statusCode);
        this.activeStreams.delete(sc.streamId);
        return true;
      }

      // Media
      case FRAME.MediaFrame: {
        const mf = readMediaFrame(data);
        if (!mf) return false;
        this.mediaStreams.get(mf.streamId)?.enqueueFrame(mf.sequenceNumber, mf.timestamp, mf.payload, mf.flags);
        return true;
      }
      case FRAME.MediaConfig: {
        const mc = readMediaConfig(data);
        if (!mc) return false;
        const isAudio = mc.mediaType === 0x01;
        this.mediaStreams.get(mc.streamId)?.dispose();
        const stream = new BoltBrowserMediaStream(this.ws!, mc.streamId, mc.callId, isAudio,
          frame => this.sendFrame(frame, true));
        this.mediaStreams.set(mc.streamId, stream);
        this.onMediaStreamConfigured?.(stream);
        return true;
      }
      case FRAME.MediaFeedback: {
        const fb = readMediaFeedback(data);
        if (!fb) return false;
        this.mediaStreams.get(fb.streamId)?.handleFeedback(fb.qualityHint);
        return true;
      }
      case FRAME.MediaKeyRequest: {
        const kr = readMediaKeyRequest(data);
        if (!kr) return false;
        this.onKeyframeRequested?.(kr.streamId);
        return true;
      }
      case FRAME.FecFrame: {
        const fec = readFecFrame(data);
        if (!fec) return false;
        this.mediaStreams.get(fec.streamId)?.enqueueFecFrame(fec.fecGroupStart, fec.fecGroupSize, fec.payload);
        return true;
      }
      case FRAME.NackRequest: {
        const nack = readNackRequest(data);
        if (!nack) return false;
        this.mediaStreams.get(nack.streamId)?.handleNackRequest(nack.missingSequences);
        return true;
      }
      case FRAME.CallSignal: {
        const cs = readCallSignal(data);
        if (!cs) return false;
        this.handleCallSignal(cs.callId, cs.signalType, cs.payload);
        return true;
      }
      default:
        return false;
    }
  }

  // ── RPC handling ───────────────────────────────────────────

  private async handleIncomingRequest(commandHash: number, payload: Uint8Array, requestId: string): Promise<void> {
    const handler = this.handlers.get(commandHash);
    if (handler) {
      const controller = new AbortController();
      this.inboundRequestControllers.set(requestId, controller);
      try {
        const result = await handler(payload, requestId, controller.signal);
        if (controller.signal.aborted) return;
        const frame = writeResponse(requestId, result.statusCode, result.payload);
        this.sendFrame(frame);
      } catch {
        if (controller.signal.aborted) return;
        const frame = writeResponse(requestId, 500, new Uint8Array(0));
        this.sendFrame(frame);
      } finally {
        this.inboundRequestControllers.delete(requestId);
      }
    } else {
      const frame = writeResponse(requestId, 501, new Uint8Array(0));
      this.sendFrame(frame);
    }
  }

  // ── Stream handling ────────────────────────────────────────

  private handleStreamOpen(streamId: string, commandHash: number): void {
    const activeStreams = this.activeStreams;
    const sendFrame = (frame: Uint8Array) => this.sendFrame(frame);
    const stream: BoltBrowserStream = {
      streamId,
      send(data: Uint8Array) { sendFrame(writeStreamData(streamId, data)); },
      close(statusCode = 200) {
        sendFrame(writeStreamClose(streamId, statusCode));
        activeStreams.delete(streamId);
      },
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
        break;
      case SIGNAL.Reject:
        this.activeCalls.delete(callId);
        this.cleanupMediaStreams(callId);
        this.onCallRejected?.(callId);
        break;
      case SIGNAL.End:
        this.activeCalls.delete(callId);
        this.cleanupMediaStreams(callId);
        this.onCallEnded?.(callId);
        break;
      case SIGNAL.Hold: this.onCallHold?.(callId); break;
      case SIGNAL.Unhold: this.onCallUnhold?.(callId); break;
      case SIGNAL.AddParticipant: this.onParticipantAdded?.(callId, payload); break;
      case SIGNAL.RemoveParticipant: this.onParticipantRemoved?.(callId, payload); break;
      case SIGNAL.KeyExchange:
        // The current signal is not bound to an authenticated peer identity.
        // Ignore it so encrypted media cannot silently trust a substituted key.
        break;
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

  private cleanupMediaStreams(callId: string): void {
    for (const [streamId, stream] of this.mediaStreams) {
      if (stream.callId !== callId) continue;
      stream.dispose();
      this.mediaStreams.delete(streamId);
    }
  }
}
