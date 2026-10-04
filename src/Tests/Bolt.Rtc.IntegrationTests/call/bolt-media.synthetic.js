// A synthetic camera, microphone and decoder in place of bolt-media.js, for the browser call tests (BrowserCallTests.cs).
//
// Everything else on the page is production: the Blazor WASM media service, its pacer and rate control, fragmentation,
// the SFrame WASM, bolt-rtc.js and the browser's own WebRTC data channel. This module replaces only what needs a device
// or a codec, so the same call runs in Chromium and WebKit on a machine with neither:
//
// - Video: pictures at the tier's frame rate and bitrate, keyframes on the same rules as the real encoder (forced on
//   (re)configure and capture start, a safety interval, coalesced requests), temporal layers L1T3/L1T2 as
//   temporalModeFor picks them, sizes with the shape of a real H.264 stream (a keyframe several deltas big, base
//   pictures larger than enhancement ones). Each picture carries its own frame id, the picture it refers to, and a
//   hash of its bytes.
// - The decoder follows the real one's rules (waits for a keyframe, restarts on a discontinuity and asks for a
//   keyframe) and checks what the real one cannot: that each picture arrived byte for byte, and that the picture it
//   refers to was decoded. Renders are timed, so a freeze is measured on the receiving page itself.
// - Audio: Opus-sized packets every frame duration, numbered and timestamped, so loss and one-way delay are measured.
//
// Every helper the media service imports from the real module comes from it unchanged (export * below).
export * from './bolt-media.real.js';
import { temporalModeFor } from './bolt-media.real.js';

const MAGIC = 0x4e595342; // 'BSYN'
const AUDIO_MAGIC = 0x44554142; // 'BAUD'
const HEADER = 24;
const KEYFRAME_REQUEST_GAP_MS = 1000;

const state = globalThis.__boltCall = {
    sender: { encoded: 0, bytes: 0, keyframes: 0, largestKeyframe: 0, tiers: [], audioSent: 0, encodeStarted: 0 },
    remotes: new Map(),
    audio: new Map(),
    measureFrom: 0,
};

function fnv(bytes, start) {
    let hash = 0x811c9dc5;
    for (let i = start; i < bytes.length; i++) { hash ^= bytes[i]; hash = Math.imul(hash, 0x01000193); }
    return hash >>> 0;
}

function picture(frameId, refId, size, key, layer) {
    const data = new Uint8Array(Math.max(HEADER + 4, size));
    const view = new DataView(data.buffer);
    let x = (frameId * 2654435761) >>> 0 || 1;
    for (let i = HEADER; i < data.length; i++) {
        x ^= x << 13; x >>>= 0; x ^= x >>> 17; x ^= x << 5; x >>>= 0;
        data[i] = x & 0xff;
    }
    view.setUint32(0, MAGIC, true);
    view.setUint32(4, frameId, true);
    view.setUint32(8, refId, true);
    view.setUint32(12, data.length, true);
    data[16] = (key ? 1 : 0) | (layer << 1);
    view.setUint32(20, fnv(data, HEADER), true);
    return data;
}

function inspect(data) {
    if (data.length < HEADER) return null;
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    if (view.getUint32(0, true) !== MAGIC || view.getUint32(12, true) !== data.length) return null;
    if (view.getUint32(20, true) !== fnv(data, HEADER)) return null;
    return { frameId: view.getUint32(4, true), refId: view.getUint32(8, true), key: (data[16] & 1) === 1, layer: (data[16] >> 1) & 3 };
}

const LAYER_PATTERNS = { L1T3: [0, 2, 1, 2], L1T2: [0, 1], L1T1: [0] };
const LAYER_WEIGHTS = { L1T3: [1.6, 1.0, 0.7], L1T2: [1.3, 0.7], L1T1: [1] };

class SyntheticVideoPipeline {
    constructor() {
        this.tier = null; this.mode = 'L1T1'; this.layers = false; this.keyframeIntervalMs = 10000;
        this.running = false; this.timer = 0; this.dotNetRef = null; this.hostRef = null;
        this.frameId = 0; this.sinceKey = 0; this.lastByLayer = [0, 0, 0, 0]; this.lastKeyframe = 0;
        this.forceKeyframe = true; this.pendingKeyframe = false;
        this.window = { since: 0, frames: 0, bytes: 0 }; this.stats = { fps: 0, kbps: 0, dropped: 0, backlog: 0 };
        this.debug = null;
    }

    async initEncoder(codec, width, height, bitrateKbps, framerate, keyframeSeconds, temporalLayers = false) {
        this.codec = codec || 'h264';
        this.keyframeIntervalMs = Math.max(500, (keyframeSeconds || 10) * 1000);
        this.layers = temporalLayers === true && globalThis.__boltCallOptions?.temporalLayers !== false;
        this._setTier({ width, height, bitrateKbps, framerate });
        this.forceKeyframe = true;
        return { width, height, codec: this.codec };
    }

    _setTier(tier) {
        const previous = this.tier;
        this.tier = tier;
        this.mode = this.layers ? temporalModeFor(tier.framerate, new Set(['L1T2', 'L1T3'])) : 'L1T1';
        state.sender.tiers.push({ at: performance.now(), ...tier });
        if (state.sender.tiers.length > 200) state.sender.tiers.shift();
        return !previous || previous.width !== tier.width || previous.height !== tier.height || previous.framerate !== tier.framerate;
    }

    async startCapture(dotNetRef) {
        if (this.running) return this.describe();
        this.dotNetRef = dotNetRef;
        this.running = true;
        this.forceKeyframe = true;
        this.window = { since: 0, frames: 0, bytes: 0 };
        state.sender.encodeStarted ||= performance.now();
        const loop = () => {
            if (!this.running) return;
            const started = performance.now();
            this._emit();
            const interval = 1000 / Math.max(1, this.tier.framerate);
            this.next = (this.next ?? started) + interval;
            if (this.next < started - interval) this.next = started;
            this.timer = setTimeout(loop, Math.max(0, this.next - performance.now()));
        };
        this.next = null;
        loop();
        return this.describe();
    }

    _emit() {
        const now = Date.now();
        const key = this.forceKeyframe || now - this.lastKeyframe >= this.keyframeIntervalMs ||
            (this.pendingKeyframe && now - this.lastKeyframe >= KEYFRAME_REQUEST_GAP_MS);
        if (key) { this.lastKeyframe = now; this.forceKeyframe = this.pendingKeyframe = false; this.sinceKey = 0; }
        const pattern = LAYER_PATTERNS[this.mode], weights = LAYER_WEIGHTS[this.mode];
        const layer = key ? 0 : pattern[this.sinceKey % pattern.length];
        this.sinceKey++;
        const budget = this.tier.bitrateKbps * 1000 / 8 / Math.max(1, this.tier.framerate);
        const short = Math.min(this.tier.width, this.tier.height);
        const keyFactor = short >= 1080 ? 8 : short >= 720 ? 7 : 6;
        const jitter = 0.85 + Math.random() * 0.3;
        const size = Math.round(budget * (key ? keyFactor : weights[layer]) * jitter);
        const frameId = (this.frameId = (this.frameId + 1) >>> 0);
        // The picture it refers to: the last one of a lower layer (or the last base picture, for a base picture).
        let refId = 0xffffffff;
        if (!key) {
            refId = 0;
            for (let l = 0; l <= Math.max(0, layer - 1); l++) refId = Math.max(refId, this.lastByLayer[l]);
            if (layer === 0) refId = this.lastByLayer[0];
        }
        if (key) this.lastByLayer = [frameId, frameId, frameId, frameId];
        else for (let l = layer; l < 4; l++) this.lastByLayer[l] = frameId;
        const data = picture(frameId, refId, size, key, layer);
        state.sender.encoded++; state.sender.bytes += data.length;
        if (key) { state.sender.keyframes++; state.sender.largestKeyframe = Math.max(state.sender.largestKeyframe, data.length); }
        if (this.window.since === 0) this.window.since = now;
        this.window.frames++; this.window.bytes += data.length;
        if (now - this.window.since >= 1000) {
            this.stats.fps = this.window.frames * 1000 / (now - this.window.since);
            this.stats.kbps = this.window.bytes * 8 / (now - this.window.since);
            this.window = { since: now, frames: 0, bytes: 0 };
        }
        const timestamp = Math.max(0, Math.round(performance.now() * 1000)) >>> 0;
        void this.dotNetRef?.invokeMethodAsync('OnVideoEncoded', data, key, frameId, timestamp, this.mode === 'L1T1' ? 0 : layer, 0);
    }

    stopCapture() { this.running = false; clearTimeout(this.timer); }
    setOrientationMetadata() { }
    attachPreview() { }
    describe() {
        return { capturing: this.running, deviceId: 'synthetic', facingMode: 'user', width: this.tier?.width ?? 0,
            height: this.tier?.height ?? 0, codec: this.codec, strategy: 'synthetic' };
    }

    async applyTier(width, height, bitrateKbps, framerate) {
        const before = this.tier;
        if (before && before.width === width && before.height === height && before.framerate === framerate && before.bitrateKbps === bitrateKbps) return false;
        if (this._setTier({ width, height, bitrateKbps, framerate })) this.forceKeyframe = true;
        return true;
    }

    requestKeyframe(force = false) { if (force) this.forceKeyframe = true; else this.pendingKeyframe = true; }

    getStats() { return { fps: this.running ? this.stats.fps : 0, kbps: this.stats.kbps, dropped: 0, backlog: 0 }; }

    addRemote(streamId, canvas, codec, hostRef) {
        this.hostRef = hostRef ?? this.hostRef;
        state.remotes.set(streamId, {
            streamId, primed: false, received: 0, bytes: 0, rendered: 0, resets: 0, waiting: 0, corrupt: 0, brokenReference: 0,
            keyframes: 0, decoded: new Set(), order: [], renders: [], lastRender: 0, events: [],
        });
        return true;
    }

    removeRemote(streamId) { state.remotes.delete(streamId); }

    decodeFrame(streamId, data, timestamp, isKeyframe, discontinuity = false) {
        const remote = state.remotes.get(streamId);
        if (!remote) return false;
        remote.received++; remote.bytes += data.byteLength;
        if (!remote.primed && !isKeyframe) { remote.waiting++; return false; }
        if (discontinuity && !isKeyframe) {
            // The real decoder: close, reopen, ignore deltas until a keyframe, ask the sender for one.
            remote.resets++; remote.primed = false;
            void this.hostRef?.invokeMethodAsync('OnVideoDecodeFailed', streamId);
            return false;
        }
        const header = inspect(data instanceof Uint8Array ? data : new Uint8Array(data));
        if (!header || header.key !== isKeyframe) {
            // A real decoder errors on a damaged bitstream: it is rebuilt and asks for a keyframe.
            remote.corrupt++; remote.primed = false;
            if (remote.events.length < 30) remote.events.push({ at: Math.round(performance.now()), what: 'corrupt', bytes: data.byteLength, isKeyframe, header });
            void this.hostRef?.invokeMethodAsync('OnVideoDecodeFailed', streamId);
            return false;
        }
        remote.primed = true;
        if (header.key) { remote.decoded.clear(); remote.order.length = 0; remote.keyframes++; }
        else if (!remote.decoded.has(header.refId)) {
            // A real decoder shows this as corruption: the pipeline must never hand it a picture whose reference it lost.
            remote.brokenReference++;
            if (remote.events.length < 30) remote.events.push({ at: Math.round(performance.now()), what: 'broken', frameId: header.frameId,
                refId: header.refId, layer: header.layer, discontinuity, recent: remote.order.slice(-6) });
            return true;
        }
        remote.decoded.add(header.frameId); remote.order.push(header.frameId);
        if (remote.order.length > 64) remote.decoded.delete(remote.order.shift());
        const now = performance.now();
        remote.rendered++; remote.lastRender = now;
        remote.renders.push(now);
        if (remote.renders.length > 20000) remote.renders.splice(0, 10000);
        return true;
    }

    getDiagnostics(enabled = true) {
        if (!enabled) return null;
        const now = performance.now(), previous = this.debug, elapsed = previous ? now - previous.at : 0;
        const rate = (count, before) => elapsed > 0 && before !== undefined ? Math.max(0, count - before) * 1000 / elapsed : null;
        const remotes = [...state.remotes.values()].map(remote => {
            const before = previous?.remotes.get(remote.streamId);
            return { streamId: remote.streamId, codec: 'synthetic', width: 0, height: 0,
                receivedFps: rate(remote.received, before?.received), renderedFps: rate(remote.rendered, before?.rendered),
                receivedKbps: before && elapsed > 0 ? rate(remote.bytes, before.bytes) * 8 / 1000 : null,
                decoderQueue: 0, pendingFrames: 0, decoderDelayMs: null, acceleration: 'synthetic', resets: remote.resets, rotation: 'none' };
        });
        this.debug = { at: now, remotes: new Map([...state.remotes.values()].map(r => [r.streamId, { received: r.received, rendered: r.rendered, bytes: r.bytes }])) };
        return { capturing: this.running, strategy: 'synthetic', codec: this.codec, width: this.tier?.width ?? 0, height: this.tier?.height ?? 0,
            targetFps: this.tier?.framerate ?? 0, cameraWidth: 0, cameraHeight: 0, cameraFps: null, captureFps: null,
            encodedFps: this.stats.fps, encodedKbps: this.stats.kbps, encoderQueue: 0, encoderDelayMs: null, acceleration: 'synthetic',
            dropped: 0, orientation: 'none', remotes };
    }

    dispose() { this.stopCapture(); }
}

class SyntheticAudioPipeline {
    constructor() { this.kbps = 32; this.frameMs = 20; this.running = false; this.transmit = false; this.muted = false; this.sequence = 0; }
    initEncoder(sampleRate, channels, bitrateKbps) { this.kbps = bitrateKbps; }
    initDecoder() { }
    initManaged() { }
    async startCapture(dotNetRef, constraints, transmit = true) {
        this.dotNetRef = dotNetRef;
        this.muted = !transmit;
        if (this.running) return true;
        this.running = true;
        const loop = () => {
            if (!this.running) return;
            const started = performance.now();
            if (!this.muted) this._emit();
            this.next = (this.next ?? started) + this.frameMs;
            if (this.next < started - this.frameMs) this.next = started;
            this.timer = setTimeout(loop, Math.max(0, this.next - performance.now()));
        };
        this.next = null;
        loop();
        return true;
    }
    _emit() {
        const size = Math.max(24, Math.round(this.kbps * this.frameMs / 8));
        const data = new Uint8Array(size);
        const view = new DataView(data.buffer);
        view.setUint32(0, AUDIO_MAGIC, true);
        view.setUint32(4, ++this.sequence, true);
        view.setFloat64(8, performance.timeOrigin + performance.now(), true);
        state.sender.audioSent++;
        void this.dotNetRef?.invokeMethodAsync('OnAudioEncoded', data, (performance.timeOrigin + performance.now()) * 1000);
    }
    setCaptureMuted(muted) { this.muted = muted === true; }
    stopCapture() { this.running = false; clearTimeout(this.timer); }
    playPcm() { }
    decodeFrame(data, timestamp, streamId) {
        const bytes = data instanceof Uint8Array ? data : new Uint8Array(data);
        if (bytes.length < 16) return;
        const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        if (view.getUint32(0, true) !== AUDIO_MAGIC) return;
        let audio = state.audio.get(streamId);
        if (!audio) state.audio.set(streamId, audio = { received: 0, highest: 0, first: 0, delays: [] });
        const sequence = view.getUint32(4, true);
        audio.received++;
        if (!audio.first) audio.first = sequence;
        audio.highest = Math.max(audio.highest, sequence);
        audio.delays.push(performance.timeOrigin + performance.now() - view.getFloat64(8, true));
        if (audio.delays.length > 20000) audio.delays.splice(0, 10000);
    }
    removeRemoteStream() { }
    reconfigureBitrate(sampleRate, channels, kbps) { this.kbps = kbps; }
    setFrameDuration(frameMs) { this.frameMs = [20, 40, 60].includes(frameMs) ? frameMs : 20; return this.frameMs; }
    stopPlayback() { }
    getPlaybackState() { return 'running'; }
    resumePlayback() { return true; }
    getAudioOutputs() { return { supported: false, deviceId: '', devices: [] }; }
    setAudioOutput() { return { supported: false, deviceId: '', devices: [] }; }
    dispose() { this.stopCapture(); }
}

export function createVideoPipeline() { return new SyntheticVideoPipeline(); }
export function createAudioPipeline() { return new SyntheticAudioPipeline(); }
export function requireSecureVoiceContext() { }
export async function checkVoiceCapabilities() { return { supported: true, reason: null, nativeCodecs: true }; }
export async function enumerateVideoInputs() { return [{ deviceId: 'synthetic', kind: 'videoinput', label: 'Synthetic camera', groupId: '' }]; }
export async function checkVideoCapabilities() {
    return { supported: true, reason: null, ceiling: 2160,
        codecs: [{ codec: 'h264', encode: true, decode: true, hardware: true, maxHeight: 2160, decodeMaxHeight: 2160, decodeHardware: true }] };
}

/// Damage so far, cheap enough to sample every few seconds.
globalThis.__boltCallDamage = () => [...state.remotes.values()].map(r => `corrupt=${r.corrupt} broken=${r.brokenReference} resets=${r.resets}`).join(' ');

/// Measurement window: everything below counts renders and audio from this moment on.
globalThis.__boltCallMeasure = () => { state.measureFrom = performance.now(); };

/// The receiving side's numbers since the measurement window opened.
globalThis.__boltCallResult = () => {
    const from = state.measureFrom, now = performance.now();
    const remotes = [...state.remotes.values()].map(remote => {
        const renders = remote.renders.filter(at => at >= from);
        let longest = 0, frozen = 0, previous = from;
        for (const at of [...renders, now]) {
            const gap = at - previous;
            longest = Math.max(longest, gap);
            if (gap > 1000) frozen += gap;
            previous = at;
        }
        return { streamId: remote.streamId, received: remote.received, rendered: remote.rendered, renderedInWindow: renders.length,
            fps: renders.length * 1000 / Math.max(1, now - from), longestFreezeMs: Math.round(longest), frozenMs: Math.round(frozen),
            resets: remote.resets, corrupt: remote.corrupt, brokenReference: remote.brokenReference, waitingForKeyframe: remote.waiting,
            events: remote.events,
            keyframes: remote.keyframes, bytes: remote.bytes };
    });
    const audio = [...state.audio.entries()].map(([streamId, a]) => {
        const sorted = [...a.delays].sort((x, y) => x - y);
        const pick = q => sorted.length ? Math.round(sorted[Math.min(sorted.length - 1, Math.floor(q * sorted.length))]) : null;
        const expected = a.highest - a.first + 1;
        return { streamId, received: a.received, expected, delivered: expected > 0 ? a.received / expected : 0, p50: pick(0.5), p99: pick(0.99) };
    });
    const sender = { ...state.sender, windowMs: Math.round(now - from), tiers: state.sender.tiers.slice(-20) };
    return { remotes, audio, sender };
};
