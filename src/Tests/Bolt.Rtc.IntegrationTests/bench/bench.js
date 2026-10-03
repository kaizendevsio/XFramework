// Media efficiency benchmark: the same synthetic camera and microphone, at the same target bitrates, through
// (a) native WebRTC media (RTCPeerConnection tracks, with an SFrame-sized encoded transform) and
// (b) Bolt's datagram path: WebCodecs with the production encoder configuration, the production SFrame adapter (WASM),
//     Bolt MediaFrame framing and video fragmentation, on an unordered, unretransmitted "bolt-media" data channel.
// Both browsers' peers live in this page and reach the far end only through TURN (relay-only, a different TURN address
// per side), so the test host can shape the receiver's leg with netem and count bytes per leg with iptables.
//
// Topology. Native WebRTC goes peer to peer through TURN. Bolt goes as in production: each browser's data channel
// (the production bolt-rtc.js) runs to the relay's own WebRTC endpoint (the bolt-rtc sidecar, relay-only, with the
// production SCTP window floor), which forwards between them; so the receiver's lossy leg is driven by the relay's
// SCTP, as a phone's downlink is.
//
// What (b) is and is not: the wire format, the encryption, the channel and the encoder settings are the product's own
// modules. The receiver's loss recovery (NACK timing, picture hold, give-up rules) is a line-by-line port of
// VideoRecoveryBuffer. Rate control is the product's own SendRateController and AudioPacketization (.NET, in the test
// host, called every 250 ms with the receiver's delay report delivered half a round trip late, as it would travel); its
// decision sets the encoder's bitrate (the picture size stays, as native WebRTC is told to keep it) and the Opus packet
// length. Not here: the pacer, the relay's lanes and congestion reports, and its retransmission cache (this relay only
// forwards; NACKs go to the sender). The call network harness runs those. Native WebRTC runs its own GCC.
import { initializeSFrame, SFrameSession } from './bolt-sframe.mjs';
import { encoderConfig, opusEncoderConfig, probeTemporalModes, temporalModeFor } from './bolt-media.js';
import { createPeer } from './bolt-rtc.js';

const MESSAGE_BYTES = 1150;           // RtcDefaults.MaxMessageBytes
const MEDIA_HEADER = 30;              // BoltCodec.MediaFrameHeaderSize
const FRAGMENT_HEADER = 12;           // VideoFrameFragments.HeaderSize
const DEFAULT_OVERHEAD = 320;         // VideoFrameFragments.DefaultEncryptionOverhead
const CONTEXT_SLACK = 24;             // VideoFrameFragments.ContextSlack
const MIN_PAYLOAD = 256;              // VideoFrameFragments.MinPayload
const FrameType = { MediaFrame: 0x21, MediaKeyRequest: 0x23, NackRequest: 0x26 };
const now = () => performance.now();

// ─── Synthetic source: moving shapes, texture and a barcode of the draw time ───

const BARCODE_BLOCK = 24, BARCODE_COLUMNS = 16;
function drawBarcode(ctx, t) {
    const time = Math.round(t) & 0xffffff;
    const check = (time ^ (time >> 8) ^ (time >> 16)) & 0xff;
    const bits = ((time << 8) | check) >>> 0;
    for (let i = 0; i < 32; i++) {
        ctx.fillStyle = (bits >>> (31 - i)) & 1 ? '#fff' : '#000';
        ctx.fillRect((i % BARCODE_COLUMNS) * BARCODE_BLOCK, Math.floor(i / BARCODE_COLUMNS) * BARCODE_BLOCK, BARCODE_BLOCK, BARCODE_BLOCK);
    }
}
/// The draw time a decoded picture carries, or null when the barcode does not check out.
function readBarcode(frame, scratch) {
    const width = BARCODE_BLOCK * BARCODE_COLUMNS, height = BARCODE_BLOCK * 2;
    try {
        if (scratch.canvas.width !== frame.displayWidth || scratch.canvas.height !== frame.displayHeight) {
            scratch.canvas.width = frame.displayWidth; scratch.canvas.height = frame.displayHeight;
        }
        scratch.context.drawImage(frame, 0, 0);
        const scale = frame.displayWidth / scratch.sourceWidth;
        const pixels = scratch.context.getImageData(0, 0, Math.ceil(width * scale), Math.ceil(height * scale));
        let bits = 0;
        for (let i = 0; i < 32; i++) {
            const cx = Math.floor(((i % BARCODE_COLUMNS) * BARCODE_BLOCK + BARCODE_BLOCK / 2) * scale);
            const cy = Math.floor((Math.floor(i / BARCODE_COLUMNS) * BARCODE_BLOCK + BARCODE_BLOCK / 2) * scale);
            let sum = 0, count = 0;
            for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
                const p = ((cy + dy) * pixels.width + (cx + dx)) * 4;
                sum += pixels.data[p] * 0.3 + pixels.data[p + 1] * 0.59 + pixels.data[p + 2] * 0.11; count++;
            }
            bits = ((bits << 1) | (sum / count > 128 ? 1 : 0)) >>> 0;
        }
        const time = bits >>> 8, check = bits & 0xff;
        if (((time ^ (time >> 8) ^ (time >> 16)) & 0xff) !== check) return null;
        return time;
    } catch { return null; }
}

function createSource({ width, height, fps }) {
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    const ctx = canvas.getContext('2d');
    const random = (() => { let s = 1234567; return () => (s = (s * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff; })();
    const balls = Array.from({ length: 10 }, (_, i) => ({ x: random() * width, y: random() * height, vx: (random() - 0.5) * 8, vy: (random() - 0.5) * 8, r: 12 + i * 3, hue: i * 36 }));
    let frame = 0;
    const draw = () => {
        frame++;
        const t = now();
        const gradient = ctx.createLinearGradient(0, 0, width, height);
        gradient.addColorStop(0, `hsl(${(frame * 0.7) % 360},40%,35%)`);
        gradient.addColorStop(1, `hsl(${(frame * 0.7 + 120) % 360},40%,25%)`);
        ctx.fillStyle = gradient; ctx.fillRect(0, 0, width, height);
        for (const ball of balls) {
            ball.x += ball.vx; ball.y += ball.vy;
            if (ball.x < 0 || ball.x > width) ball.vx = -ball.vx;
            if (ball.y < 0 || ball.y > height) ball.vy = -ball.vy;
            ctx.fillStyle = `hsl(${ball.hue},70%,55%)`;
            ctx.beginPath(); ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2); ctx.fill();
        }
        // A talking-head-sized patch of fine moving texture: what keeps an encoder honest.
        for (let i = 0; i < 120; i++) {
            const v = Math.floor(random() * 255);
            ctx.fillStyle = `rgb(${v},${v},${v})`;
            ctx.fillRect(width * 0.55 + (i % 12) * 10 + Math.sin(frame / 9) * 20, height * 0.35 + Math.floor(i / 12) * 10, 10, 10);
        }
        ctx.fillStyle = '#fff'; ctx.font = '20px sans-serif';
        ctx.fillText(`frame ${frame}`, 20, height - 20);
        drawBarcode(ctx, t);
    };
    draw();
    const timer = setInterval(draw, 1000 / fps);
    const video = canvas.captureStream(fps).getVideoTracks()[0];

    // Voice-like audio that never goes silent (so DTX on one side and not the other cannot skew the bytes).
    const audioContext = new AudioContext({ sampleRate: 48000 });
    const destination = audioContext.createMediaStreamDestination();
    destination.channelCount = 1; destination.channelCountMode = 'explicit';
    const voice = audioContext.createGain(); voice.gain.value = 0.3;
    for (const [frequency, level] of [[180, 0.5], [360, 0.3], [900, 0.15], [2400, 0.05]]) {
        const oscillator = audioContext.createOscillator(); oscillator.frequency.value = frequency;
        const gain = audioContext.createGain(); gain.gain.value = level;
        oscillator.connect(gain).connect(voice); oscillator.start();
    }
    const lfo = audioContext.createOscillator(); lfo.frequency.value = 4;
    const depth = audioContext.createGain(); depth.gain.value = 0.15;
    lfo.connect(depth).connect(voice.gain); lfo.start();
    const noise = audioContext.createBuffer(1, 48000, 48000);
    const samples = noise.getChannelData(0);
    for (let i = 0; i < samples.length; i++) samples[i] = (random() - 0.5) * 0.05;
    const hiss = audioContext.createBufferSource(); hiss.buffer = noise; hiss.loop = true; hiss.connect(voice); hiss.start();
    voice.connect(destination);
    const audio = destination.stream.getAudioTracks()[0];
    return {
        video, audio, width, height, audioContext,
        stop() { clearInterval(timer); video.stop(); audio.stop(); void audioContext.close(); }
    };
}

// ─── Receiver-side picture metrics (identical for both paths) ───

class PictureMetrics {
    constructor(sourceWidth) {
        const canvas = new OffscreenCanvas(16, 16);
        this.scratch = { canvas, context: canvas.getContext('2d', { willReadFrequently: true }), sourceWidth };
        this.frames = []; this.delays = []; this.sizes = new Map();
        this.measuring = false;
    }
    onFrame(frame) {
        const at = now();
        if (this.measuring) {
            this.frames.push(at);
            const key = `${frame.displayWidth}x${frame.displayHeight}`;
            this.sizes.set(key, (this.sizes.get(key) ?? 0) + 1);
            const drawn = readBarcode(frame, this.scratch);
            if (drawn !== null) {
                const delay = ((Math.round(at) & 0xffffff) - drawn + 0x1000000) % 0x1000000;
                if (delay < 20000) this.delays.push(delay);
            }
        }
        frame.close();
    }
    summary(seconds) {
        const sorted = [...this.delays].sort((a, b) => a - b);
        const q = p => sorted.length ? Math.round(sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))]) : null;
        // WebRTC's freeze definition: a gap longer than max(3 x mean interval, mean + 150 ms).
        let freezes = 0, frozenMs = 0;
        const gaps = this.frames.slice(1).map((t, i) => t - this.frames[i]);
        const mean = gaps.length ? gaps.reduce((a, b) => a + b, 0) / gaps.length : 0;
        for (const gap of gaps) if (gap > Math.max(3 * mean, mean + 150)) { freezes++; frozenMs += gap; }
        const size = [...this.sizes.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? '-';
        return { fps: +(this.frames.length / seconds).toFixed(1), resolution: size, delayP50: q(0.5), delayP99: q(0.99),
            delaySamples: sorted.length, freezes, frozenSeconds: +(frozenMs / 1000).toFixed(1) };
    }
}

function readTrack(track, onFrame) {
    const reader = new MediaStreamTrackProcessor({ track }).readable.getReader();
    let stopped = false;
    (async () => {
        while (!stopped) {
            const { value, done } = await reader.read();
            if (done) break;
            onFrame(value);
        }
    })();
    return () => { stopped = true; void reader.cancel(); };
}

// ─── ICE through TURN, one address per side ───

async function connect(pc1, pc2) {
    pc1.onicecandidate = event => { if (event.candidate) void pc2.addIceCandidate(event.candidate); };
    pc2.onicecandidate = event => { if (event.candidate) void pc1.addIceCandidate(event.candidate); };
    const offer = await pc1.createOffer();
    await pc1.setLocalDescription(offer);
    await pc2.setRemoteDescription(offer);
    const answer = await pc2.createAnswer();
    await pc2.setLocalDescription(answer);
    await pc1.setRemoteDescription(answer);
    const deadline = now() + 20000;
    while (pc1.connectionState !== 'connected' || pc2.connectionState !== 'connected') {
        if (now() > deadline) throw new Error(`ICE did not connect: ${pc1.connectionState}/${pc2.connectionState}`);
        await new Promise(r => setTimeout(r, 50));
    }
}

async function stats(pc) {
    const report = await pc.getStats();
    const all = [];
    report.forEach(value => all.push(value));
    return all;
}
async function roundTripMs(pc) {
    for (const entry of await stats(pc))
        if (entry.type === 'candidate-pair' && entry.nominated && entry.currentRoundTripTime > 0) return entry.currentRoundTripTime * 1000;
    return null;
}

const sleep = ms => new Promise(r => setTimeout(r, ms));

// ─── (a) Native WebRTC media ───

async function runNative(options, source) {
    // A run without TURN (BENCH_DIRECT, local smoke tests only) connects directly over loopback.
    const config = turn => turn ? { iceServers: [turn], iceTransportPolicy: 'relay', encodedInsertableStreams: true } : { encodedInsertableStreams: true };
    const pc1 = new RTCPeerConnection(config(options.turnSender));
    const pc2 = new RTCPeerConnection(config(options.turnReceiver));
    const stream = new MediaStream([source.video, source.audio]);
    const videoSender = pc1.addTrack(source.video, stream);
    const audioSender = pc1.addTrack(source.audio, stream);
    const videoTransceiver = pc1.getTransceivers().find(x => x.sender === videoSender);
    const wanted = RTCRtpSender.getCapabilities('video').codecs.filter(c => c.mimeType.toLowerCase() === `video/${options.codec}`);
    const rest = RTCRtpSender.getCapabilities('video').codecs.filter(c => !wanted.includes(c));
    if (wanted.length) videoTransceiver.setCodecPreferences([...wanted, ...rest]);

    // SFrame-sized: 20 bytes (a compact SFrame's header and tag) on every encoded frame, removed on the far side. They
    // trail the frame: Chrome still packetizes H.264 by its NAL units after the transform, so a prefix would break that.
    // Non-zero bytes, so nothing in them reads as a start code.
    const transform = { bytes: 20, available: typeof videoSender.createEncodedStreams === 'function' };
    const seal = () => new TransformStream({ transform(chunk, controller) {
        const data = new Uint8Array(chunk.data);
        const sealed = new Uint8Array(data.length + transform.bytes).fill(0x5a);
        sealed.set(data, 0);
        chunk.data = sealed.buffer; controller.enqueue(chunk);
    } });
    const open = () => new TransformStream({ transform(chunk, controller) {
        const data = new Uint8Array(chunk.data);
        if (data.length > transform.bytes) chunk.data = data.slice(0, data.length - transform.bytes).buffer;
        controller.enqueue(chunk);
    } });
    if (transform.available)
        for (const sender of [videoSender, audioSender]) {
            const { readable, writable } = sender.createEncodedStreams();
            void readable.pipeThrough(seal()).pipeTo(writable);
        }

    const metrics = new PictureMetrics(source.width);
    let stopReading = null;
    pc2.ontrack = event => {
        if (transform.available) {
            const { readable, writable } = event.receiver.createEncodedStreams();
            void readable.pipeThrough(open()).pipeTo(writable);
        }
        if (event.track.kind === 'video') stopReading = readTrack(event.track, frame => metrics.onFrame(frame));
        else { const element = new Audio(); element.srcObject = new MediaStream([event.track]); void element.play().catch(() => {}); }
    };
    await connect(pc1, pc2);
    for (const [sender, kbps] of [[videoSender, options.videoKbps], [audioSender, options.audioKbps]]) {
        const parameters = sender.getParameters();
        parameters.encodings[0].maxBitrate = kbps * 1000;
        // The same picture size as the Bolt path, so quality differences show as frame rate and freezes. Left to itself,
        // WebRTC halves the size at these rates.
        if (sender === videoSender) parameters.degradationPreference = 'maintain-resolution';
        await sender.setParameters(parameters);
    }

    await sleep(options.warmupSeconds * 1000);
    await window.benchMark?.('start');
    const before = { sender: await stats(pc1), receiver: await stats(pc2) };
    metrics.measuring = true;
    const started = now();
    await sleep(options.seconds * 1000);
    const elapsed = (now() - started) / 1000;
    metrics.measuring = false;
    await window.benchMark?.('end');
    const after = { sender: await stats(pc1), receiver: await stats(pc2) };
    const rtt = await roundTripMs(pc2);
    stopReading?.();
    pc1.close(); pc2.close();

    const pick = (list, type, kind) => list.find(x => x.type === type && x.kind === kind) ?? {};
    const delta = (type, kind, field, side = 'sender') => (pick(after[side], type, kind)[field] ?? 0) - (pick(before[side], type, kind)[field] ?? 0);
    const videoPayload = delta('outbound-rtp', 'video', 'bytesSent') - delta('outbound-rtp', 'video', 'retransmittedBytesSent');
    const audioPayload = delta('outbound-rtp', 'audio', 'bytesSent') - delta('outbound-rtp', 'audio', 'retransmittedBytesSent');
    const audioReceived = delta('inbound-rtp', 'audio', 'packetsReceived', 'receiver');
    const audioSent = delta('outbound-rtp', 'audio', 'packetsSent');
    const inboundVideo = pick(after.receiver, 'inbound-rtp', 'video');
    return {
        seconds: elapsed, rttMs: rtt, transform: transform.available ? 'sframe-sized (20 bytes per frame)' : 'none',
        // Codec bytes as encoded (an encoded transform's 20 bytes count as overhead, like SFrame's on our side).
        mediaBytes: Math.max(0, videoPayload + audioPayload - (transform.available ? 20 * (delta('outbound-rtp', 'video', 'framesEncoded') + audioSent) : 0)),
        packetsSent: delta('outbound-rtp', 'video', 'packetsSent') + audioSent,
        audioPacketsSent: audioSent,
        audioDelivered: audioSent ? +(100 * Math.min(audioReceived, audioSent) / audioSent).toFixed(1) : null,
        videoTargetKbps: options.videoKbps,
        videoSentKbps: Math.round(delta('outbound-rtp', 'video', 'bytesSent') * 8 / elapsed / 1000),
        retransmittedKbps: Math.round((delta('outbound-rtp', 'video', 'retransmittedBytesSent') + delta('outbound-rtp', 'audio', 'retransmittedBytesSent')) * 8 / elapsed / 1000),
        nacks: delta('inbound-rtp', 'video', 'nackCount', 'receiver'),
        keyframeRequests: delta('inbound-rtp', 'video', 'pliCount', 'receiver') + delta('inbound-rtp', 'video', 'firCount', 'receiver'),
        codec: (after.sender.find(x => x.type === 'codec' && x.mimeType?.startsWith('video/')) ?? {}).mimeType ?? options.codec,
        receivedResolution: inboundVideo.frameWidth ? `${inboundVideo.frameWidth}x${inboundVideo.frameHeight}` : null,
        picture: metrics.summary(elapsed),
    };
}

// ─── (b) Bolt over a data channel ───

function mediaFrame(streamId, sequence, timestamp, flags, payload) {
    const frame = new Uint8Array(MEDIA_HEADER + payload.length);
    const view = new DataView(frame.buffer);
    frame[0] = FrameType.MediaFrame; frame.set(streamId, 1);
    view.setUint32(17, sequence, true); view.setUint32(21, timestamp >>> 0, true);
    frame[25] = flags; view.setUint32(26, payload.length, true);
    frame.set(payload, MEDIA_HEADER);
    return frame;
}
function sequenceList(type, streamId, sequences) {
    const frame = new Uint8Array(19 + sequences.length * 4);
    const view = new DataView(frame.buffer);
    frame[0] = type; frame.set(streamId, 1); view.setUint16(17, sequences.length, true);
    sequences.forEach((s, i) => view.setUint32(19 + i * 4, s, true));
    return frame;
}
const uuid = bytes => [...bytes].map((b, i) => ([4, 6, 8, 10].includes(i) ? '-' : '') + b.toString(16).padStart(2, '0')).join('');
const streamGuid = () => crypto.getRandomValues(new Uint8Array(16));

function fragments(data, frameId, timestamp, keyframe, layer, payload) {
    payload = Math.max(MIN_PAYLOAD, payload);
    const count = Math.max(1, Math.ceil(data.length / payload));
    if (count > 256) return [];
    const list = [];
    for (let index = 0; index < count; index++) {
        const part = data.subarray(index * payload, Math.min(data.length, (index + 1) * payload));
        const fragment = new Uint8Array(FRAGMENT_HEADER + part.length);
        const view = new DataView(fragment.buffer);
        fragment[0] = 0x10 | (keyframe ? 1 : 0) | (index === count - 1 ? 2 : 0) | ((keyframe ? 0 : Math.min(3, layer)) << 2);
        fragment[1] = count - 1;
        view.setUint16(2, index, true); view.setUint32(4, frameId, true); view.setUint32(8, timestamp >>> 0, true);
        fragment.set(part, FRAGMENT_HEADER);
        list.push(fragment);
    }
    return list;
}

/// VideoRecoveryBuffer (Bolt.Media.Browser) ported line for line: the same reorder wait, NACK pacing, window and
/// give-up rules. With recovery off it is the plain assembler (emit on completion, drop older partial pictures).
class RecoveryBuffer {
    static REORDER = 15; static MAX_TRIES = 3; static MAX_WINDOW = 1500;
    constructor(recover) { this.recover = recover; this.window = 0; this.rtt = 200; this.pictures = new Map(); this.missing = new Map();
        this.highest = null; this.lastReleased = null; this.lastHandled = null; this.localLoss = false; this.layered = false;
        this.topLayer = 0; this.skipAbove = null; this.stats = { nacked: 0, recovered: 0, abandoned: 0, skipped: 0, incomplete: 0 }; }
    configure(rttMs) { this.rtt = Math.max(1, Math.min(5000, rttMs)); this.window = this.recover ? Math.max(100, Math.min(RecoveryBuffer.MAX_WINDOW, Math.round(this.rtt * 1.5 + 50))) : 0; }
    static newer(a, b) { const d = (a - b) >>> 0; return d > 0 && d < 0x80000000; }
    push(sequence, fragment, at, ready) {
        if (fragment.length <= FRAGMENT_HEADER || (fragment[0] & 0xf0) !== 0x10) return;
        const view = new DataView(fragment.buffer, fragment.byteOffset, fragment.byteLength);
        const total = fragment[1] + 1, index = view.getUint16(2, true), frameId = view.getUint32(4, true);
        if (index >= total) return;
        const header = { total, index, frameId, timestamp: view.getUint32(8, true), keyframe: (fragment[0] & 1) !== 0, layer: (fragment[0] & 0x0c) >> 2 };
        const payload = fragment.subarray(FRAGMENT_HEADER);
        if (!this.recover) return this.#plain(header, payload, ready);
        const asked = this.missing.get(sequence);
        if (asked) { this.missing.delete(sequence); if (asked.tries > 0) this.stats.recovered++; }
        if (this.highest === null) this.highest = sequence;
        else if (RecoveryBuffer.newer(sequence, this.highest)) {
            const gap = (sequence - this.highest) >>> 0;
            if (gap <= 512) for (let o = 1; o < gap; o++) this.#addMissing((this.highest + o) >>> 0, at);
            else { this.missing.clear(); this.localLoss = true; }
            this.highest = sequence;
        }
        if (this.lastHandled !== null && !RecoveryBuffer.newer(frameId, this.lastHandled)) return;
        const first = (sequence - index) >>> 0;
        let slot = this.pictures.get(frameId);
        if (!slot) { slot = { frameId, first, total, parts: new Array(total), received: 0, bytes: 0, keyframe: false, layer: 0, firstSeen: at, complete: false, lost: false }; this.pictures.set(frameId, slot); }
        else if (slot.total !== total || slot.first !== first) { this.#lose(slot); return this.#release(ready); }
        if (slot.lost || slot.complete || slot.parts[index]) return;
        slot.parts[index] = payload.slice(); slot.received++; slot.bytes += payload.length; slot.timestamp = header.timestamp;
        if (header.keyframe) slot.keyframe = true;
        slot.layer = header.layer; if (header.layer > 0) this.layered = true; this.topLayer = Math.max(this.topLayer, header.layer);
        if (slot.received === slot.total) slot.complete = true;
        this.#release(ready);
    }
    poll(at, ready, nacks) {
        if (!this.recover) return;
        for (const picture of this.pictures.values()) {
            if (picture.complete || picture.lost) continue;
            for (let i = 0; i < picture.total; i++) {
                const s = (picture.first + i) >>> 0;
                if (!picture.parts[i] && this.highest !== null && RecoveryBuffer.newer(s, this.highest)) this.#addMissing(s, picture.firstSeen);
            }
        }
        for (const [sequence, missing] of [...this.missing]) {
            const age = at - missing.since, owner = this.#owner(sequence);
            if (owner?.lost) { this.missing.delete(sequence); continue; }
            if (age >= this.window) {
                this.missing.delete(sequence); this.stats.abandoned++;
                if (owner) this.#lose(owner); else this.localLoss = true;
                continue;
            }
            const due = missing.tries === 0 || at - missing.lastAsked >= this.rtt * 1.2 + 10;
            if (age >= RecoveryBuffer.REORDER && missing.tries < RecoveryBuffer.MAX_TRIES && age + this.rtt <= this.window && due) {
                nacks.push(sequence); missing.tries++; missing.lastAsked = at; this.stats.nacked++;
            }
        }
        for (const picture of this.pictures.values())
            if (!picture.complete && !picture.lost && at - picture.firstSeen >= this.window + this.rtt) this.#lose(picture);
        this.#release(ready);
    }
    #addMissing(sequence, since) { if (this.missing.size < 1024 && !this.missing.has(sequence)) this.missing.set(sequence, { since, tries: 0, lastAsked: 0 }); }
    #owner(sequence) { for (const p of this.pictures.values()) if (((sequence - p.first) >>> 0) < p.total) return p; return null; }
    #oldest() { let oldest = null; for (const p of this.pictures.values()) if (!oldest || RecoveryBuffer.newer(oldest.first, p.first)) oldest = p; return oldest; }
    #lose(p) { if (p.lost) return; p.lost = true; p.complete = false; for (let i = 0; i < p.total; i++) this.missing.delete((p.first + i) >>> 0); }
    #handled(id) { if (this.lastHandled === null || RecoveryBuffer.newer(id, this.lastHandled)) this.lastHandled = id; }
    #release(ready) {
        for (let p = this.#oldest(); p; p = this.#oldest()) {
            if (p.lost) { this.pictures.delete(p.frameId); this.#handled(p.frameId); this.stats.incomplete++;
                if (p.keyframe || p.layer === 0 || !this.layered) this.localLoss = true; else this.skipAbove = Math.min(this.skipAbove ?? p.layer, p.layer); continue; }
            if (!p.complete) {
                if (this.layered && p.layer > 0 && p.layer >= this.topLayer && [...this.pictures.values()].some(x => x.complete && x !== p)) { this.#lose(p); continue; }
                break;
            }
            if ([...this.missing.keys()].some(s => RecoveryBuffer.newer(p.first, s))) break;
            this.pictures.delete(p.frameId);
            this.#emit(p, ready);
        }
    }
    #emit(p, ready) {
        this.#handled(p.frameId);
        if (this.skipAbove !== null) {
            if (!p.keyframe && p.layer > this.skipAbove) { this.stats.skipped++; return; }
            this.skipAbove = null;
        }
        const gap = this.lastReleased !== null && ((p.frameId - this.lastReleased) >>> 0) !== 1;
        const discontinuity = gap && (!this.layered || this.localLoss);
        if (p.keyframe || discontinuity) this.localLoss = false;
        const data = new Uint8Array(p.bytes); let offset = 0;
        for (const part of p.parts) { data.set(part, offset); offset += part.length; }
        this.lastReleased = p.frameId;
        ready.push({ data, timestamp: p.timestamp, keyframe: p.keyframe, discontinuity });
    }
    // The assembler without recovery (VideoFrameAssembler): emit on completion; older partial pictures are loss.
    #plain(header, payload, ready) {
        const { frameId, total, index } = header;
        if (this.lastReleased !== null && !RecoveryBuffer.newer(frameId, this.lastReleased)) return;
        let slot = this.pictures.get(frameId);
        if (!slot) { slot = { frameId, parts: new Array(total), received: 0, bytes: 0, keyframe: false, layer: 0, total }; this.pictures.set(frameId, slot); }
        if (slot.parts[index]) return;
        slot.parts[index] = payload.slice(); slot.received++; slot.bytes += payload.length; slot.timestamp = header.timestamp;
        if (header.keyframe) slot.keyframe = true;
        slot.layer = header.layer; if (header.layer > 0) this.layered = true;
        if (slot.received !== slot.total) return;
        this.pictures.delete(frameId);
        for (const [id, stale] of [...this.pictures]) if (RecoveryBuffer.newer(frameId, id)) { this.pictures.delete(id); this.stats.incomplete++; if (!slot.keyframe) this.localLoss = true; }
        const gap = this.lastReleased !== null && ((frameId - this.lastReleased) >>> 0) !== 1;
        const discontinuity = gap && (!this.layered || this.localLoss);
        if (slot.keyframe || discontinuity) this.localLoss = false;
        const data = new Uint8Array(slot.bytes); let offset = 0;
        for (const part of slot.parts) { data.set(part, offset); offset += part.length; }
        this.lastReleased = frameId;
        ready.push({ data, timestamp: slot.timestamp, keyframe: slot.keyframe, discontinuity });
    }
}

/// One browser's production data channel (bolt-rtc.js) to the test host's relay, signalled over a WebSocket.
async function relayChannel(id, turn) {
    const socket = new WebSocket(`ws://${location.host}/relay?id=${id}`);
    await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
    let opened; const open = new Promise(resolve => opened = resolve);
    const channel = { onmessage: null, rttMs: null, socket };
    const dotnet = { invokeMethodAsync: async (method, ...args) => {
        if (method === 'OnCandidate') socket.send(JSON.stringify({ type: 'candidate', candidate: args[0], sdpMid: args[1], sdpMLineIndex: args[2] }));
        else if (method === 'OnState' && args[0] === 'open') opened();
        else if (method === 'OnMessage') channel.onmessage?.(args[0]);
        else if (method === 'OnPath' && args[4] > 0) channel.rttMs = args[4];
    } };
    const peer = channel.peer = createPeer(dotnet, { iceServers: turn ? [turn] : [], iceTransportPolicy: turn ? 'relay' : 'all', maxMessageBytes: MESSAGE_BYTES });
    socket.onmessage = async event => {
        const message = JSON.parse(event.data);
        if (message.type === 'answer') await peer.setAnswer(message.sdp);
        else if (message.type === 'candidate') await peer.addCandidate(message.candidate, message.sdpMid, message.sdpMLineIndex);
    };
    socket.send(JSON.stringify({ type: 'offer', sdp: await peer.createOffer(false) }));
    await Promise.race([open, sleep(20000).then(() => { throw new Error(`The ${id} channel did not open`); })]);
    return channel;
}

async function runBolt(options, source) {
    await initializeSFrame();
    const run = crypto.randomUUID().slice(0, 8);
    const [sender, receiver] = await Promise.all([relayChannel(`${run}-sender`, options.turnSender), relayChannel(`${run}-receiver`, options.turnReceiver)]);
    const sendChannel = { send: bytes => sender.peer.send(bytes), get bufferedAmount() { return sender.peer.bufferedAmount(); } };
    const receiveChannel = { send: bytes => receiver.peer.send(bytes) };

    // SFrame epoch exactly as Yap installs it: random keys and KIDs per member, the roster binding, compact or not.
    const call = crypto.randomUUID();
    const alice = `yap-media-${call.replaceAll('-', '')}-${'a'.repeat(32)}`, bob = `yap-media-${call.replaceAll('-', '')}-${'b'.repeat(32)}`;
    const key = () => crypto.getRandomValues(new Uint8Array(32));
    const kid = () => String(BigInt.asUintN(63, BigInt('0x' + [...crypto.getRandomValues(new Uint8Array(8))].map(b => b.toString(16).padStart(2, '0')).join(''))) + 8n);
    const members = { alice: { senderId: alice, kid: kid(), key: key() }, bob: { senderId: bob, kid: kid(), key: key() } };
    const binding = [...crypto.getRandomValues(new Uint8Array(32))].map(b => b.toString(16).padStart(2, '0')).join('');
    const tx = new SFrameSession(call, alice), rx = new SFrameSession(call, bob);
    tx.installEpoch({ epochId: '1', rosterBinding: binding, local: members.alice, remote: [{ ...members.bob, key: members.bob.key.slice() }], compact: options.compact });
    rx.installEpoch({ epochId: '1', rosterBinding: binding, local: members.bob, remote: [{ ...members.alice, key: members.alice.key.slice() }], compact: options.compact });
    tx.activateEpoch('1', binding); rx.activateEpoch('1', binding);

    const videoStream = streamGuid(), audioStream = streamGuid();
    const videoStreamId = uuid(videoStream), audioStreamId = uuid(audioStream);
    const counters = { mediaBytes: 0, messages: 0, messageBytes: 0, dropped: 0, keyframes: 0, keyRequests: 0, retransmitted: 0, nackRequests: 0,
        audioSent: 0, audioReceived: 0, videoFrames: 0, overhead: 0 };
    let measuring = false;
    const rate = { sent: 0, audio: 0, received: 0, transitFloor: Infinity, previousFloor: Infinity, floorSince: now(), latest: null,
        videoKbps: options.videoKbps, audioKbps: options.audioKbps, frameMs: 20,
        audioSentAt: new Map(), trace: [] };
    const send = (bytes, mediaBytes = 0) => {
        if (sendChannel.bufferedAmount > 256 * 1024 || !sendChannel.send(bytes)) { counters.dropped++; return false; }
        rate.sent += bytes.length;
        if (measuring) { counters.messages++; counters.messageBytes += bytes.length; counters.mediaBytes += mediaBytes; }
        return true;
    };

    // Video sender: the production encoder configuration (with its temporal layers), fragments sized for the channel.
    let sequence = 0, audioSequence = 0, frameId = 0, encryptionOverhead = 0;
    const retransmit = new Map();
    let keyframeWanted = true, lastKeyframe = -Infinity;
    const baseConfig = encoderConfig(options.codec, source.width, source.height, options.videoKbps, options.fps, 'no-preference');
    const modes = await probeTemporalModes(baseConfig);
    const videoConfig = { ...baseConfig, scalabilityMode: temporalModeFor(options.fps, modes) };
    const encoder = new VideoEncoder({
        output: (chunk, metadata) => {
            const data = new Uint8Array(chunk.byteLength); chunk.copyTo(data);
            const keyframe = chunk.type === 'key';
            const layer = keyframe || videoConfig.scalabilityMode === 'L1T1' ? 0 : Math.min(3, metadata?.svc?.temporalLayerId ?? 0);
            const id = (frameId = (frameId + 1) >>> 0);
            const overhead = encryptionOverhead > 0 ? encryptionOverhead : DEFAULT_OVERHEAD;
            const payload = MESSAGE_BYTES - (MEDIA_HEADER + overhead + CONTEXT_SLACK + FRAGMENT_HEADER);
            const timestamp = Math.round(chunk.timestamp * 90 / 1000) >>> 0;
            if (measuring) counters.videoFrames++;
            for (const [index, fragment] of fragments(data, id, Math.round(chunk.timestamp) >>> 0, keyframe, layer, payload).entries()) {
                const seq = (sequence = (sequence + 1) >>> 0);
                const sealed = tx.encrypt(fragment, videoStreamId, seq, timestamp);
                encryptionOverhead = Math.max(encryptionOverhead, sealed.length - fragment.length);
                if (measuring) counters.overhead = Math.max(counters.overhead, sealed.length - fragment.length);
                const flags = 0x10 | (index === 0 && keyframe ? 1 : 0) | ((Math.min(3, layer) << 1) & 0x06);
                const frame = mediaFrame(videoStream, seq, timestamp, flags, sealed);
                if (options.nack) { retransmit.set(seq, { frame, at: now() }); if (retransmit.size > 1024) retransmit.delete(retransmit.keys().next().value); }
                send(frame, index === 0 ? data.length : 0);
            }
        },
        error: error => console.error('bench video encoder', error)
    });
    encoder.configure(videoConfig);
    const stopVideo = readTrack(source.video, frame => {
        try {
            if (encoder.encodeQueueSize > 2) return;
            const at = now();
            const key = keyframeWanted || at - lastKeyframe > 10000;
            if (key) { keyframeWanted = false; lastKeyframe = at; if (measuring) counters.keyframes++; }
            encoder.encode(frame, { keyFrame: key });
        } finally { frame.close(); }
    });

    // Audio sender: the production Opus configuration (FEC, DTX), at the packet length the mode asks for.
    let audioConfig = await opusEncoderConfig(48000, 1, options.audioKbps, { inbandFec: true, packetLossPercent: 5, dtx: true });
    if (options.audioFrameMs !== 20) {
        const longer = { ...audioConfig, opus: { ...(audioConfig.opus ?? {}), frameDuration: options.audioFrameMs * 1000 } };
        if ((await AudioEncoder.isConfigSupported(longer)).config?.opus?.frameDuration === options.audioFrameMs * 1000) audioConfig = longer;
    }
    const audioEncoder = new AudioEncoder({
        output: chunk => {
            const data = new Uint8Array(chunk.byteLength); chunk.copyTo(data);
            const seq = (audioSequence = (audioSequence + 1) >>> 0);
            const timestamp = Math.round(chunk.timestamp * 48 / 1000) >>> 0;
            const sealed = tx.encrypt(data, audioStreamId, seq, timestamp);
            if (measuring) { counters.audioSent++; counters.overhead = Math.max(counters.overhead, sealed.length - data.length); }
            const frame = mediaFrame(audioStream, seq, timestamp, 0x10, sealed);
            if (send(frame, data.length)) {
                rate.audio += frame.length;
                rate.audioSentAt.set(seq, now());
                if (rate.audioSentAt.size > 2000) rate.audioSentAt.delete(rate.audioSentAt.keys().next().value);
            }
        },
        error: error => console.error('bench audio encoder', error)
    });
    audioEncoder.configure(audioConfig);
    rate.frameMs = audioConfig.opus?.frameDuration ? audioConfig.opus.frameDuration / 1000 : 20;
    const stopAudio = readTrack(source.audio, data => { try { audioEncoder.encode(data); } finally { data.close(); } });

    // Rate control: the product's controller, fed the way the call feeds it (see the header).
    const rateTimer = setInterval(async () => {
        if (!window.benchRate) return;
        const at = now();
        // The receiver's report: queuing delay of audio over its 10 s floor, and what arrived; it reaches the sender half a
        // round trip later.
        const report = { queueDelayMs: Math.max(0, Math.round(rate.lastTransit - Math.min(rate.transitFloor, rate.previousFloor))) || 0,
            receivedKbps: Math.round(rate.received * 8 / 250), at };
        rate.received = 0;
        setTimeout(() => { rate.latest = report; }, Math.max(0, rtt / 2));
        const sample = { sentKbps: Math.round(rate.sent * 8 / 250), audioKbps: Math.round(rate.audio * 8 / 250),
            localQueueMs: Math.round(sendChannel.bufferedAmount * 8 / Math.max(1, rate.sent * 8 / 250)),
            receiver: rate.latest && at - rate.latest.at < 1500 ? rate.latest : null };
        rate.sent = rate.audio = 0;
        let decision;
        try { decision = JSON.parse(await window.benchRate(JSON.stringify(sample))); } catch { return; }
        const video = Math.max(60, Math.min(options.videoKbps, decision.videoKbps));
        if (Math.abs(video - rate.videoKbps) / rate.videoKbps > 0.05 && encoder.state === 'configured') {
            rate.videoKbps = video;
            encoder.configure({ ...videoConfig, bitrate: video * 1000 });
        }
        const frameMs = decision.frameMs ?? rate.frameMs;
        if ((frameMs !== rate.frameMs || decision.audioKbps !== rate.audioKbps) && audioEncoder.state === 'configured') {
            const next = { ...audioConfig, bitrate: decision.audioKbps * 1000, opus: { ...(audioConfig.opus ?? {}), frameDuration: frameMs * 1000 } };
            if ((await AudioEncoder.isConfigSupported(next)).config?.opus?.frameDuration === frameMs * 1000) {
                audioConfig = next; rate.frameMs = frameMs; rate.audioKbps = decision.audioKbps;
                audioEncoder.configure(next);
            }
        }
        if (measuring && rate.trace.length < 400) rate.trace.push([Math.round(at), decision.totalKbps, video, rate.frameMs, report.queueDelayMs]);
    }, 250);

    // Sender side of the reverse direction: NACKs answered from the retransmission buffer, keyframe requests honoured.
    sender.onmessage = bytes => {
        if (bytes[0] === FrameType.MediaKeyRequest) { if (now() - lastKeyframe >= 1000) keyframeWanted = true; return; }
        if (bytes[0] !== FrameType.NackRequest || !options.nack) return;
        const view = new DataView(bytes.buffer);
        const count = view.getUint16(17, true);
        for (let i = 0; i < Math.min(count, 64); i++) {
            const stored = retransmit.get(view.getUint32(19 + i * 4, true));
            if (stored && now() - stored.at <= 2000 && send(stored.frame) && measuring) counters.retransmitted++;
        }
    };

    // Receiver: decrypt, recover (or plain reassembly), decode in order; a break restarts the decoder on a keyframe.
    const metrics = new PictureMetrics(source.width);
    const recovery = new RecoveryBuffer(options.nack);
    let rtt = 300, lastKeyRequest = -Infinity, decoderNeedsKey = true;
    const decoder = new VideoDecoder({ output: frame => metrics.onFrame(frame), error: error => console.error('bench video decoder', error) });
    const decoderConfig = { codec: videoConfig.codec, optimizeForLatency: true };
    decoder.configure(decoderConfig);
    const audioDecoder = new AudioDecoder({ output: data => data.close(), error: () => {} });
    audioDecoder.configure({ codec: 'opus', sampleRate: 48000, numberOfChannels: 1 });
    const askKeyframe = () => {
        if (now() - lastKeyRequest < 1000) return;
        lastKeyRequest = now();
        receiveChannel.send(new Uint8Array([FrameType.MediaKeyRequest, ...videoStream]));
        if (measuring) counters.keyRequests++;
    };
    const decode = pictures => {
        for (const picture of pictures) {
            if (picture.discontinuity && !picture.keyframe) { decoder.reset(); decoder.configure(decoderConfig); decoderNeedsKey = true; askKeyframe(); }
            if (decoderNeedsKey && !picture.keyframe) continue;
            decoderNeedsKey = false;
            try { decoder.decode(new EncodedVideoChunk({ type: picture.keyframe ? 'key' : 'delta', timestamp: picture.timestamp, data: picture.data })); }
            catch { decoder.reset(); decoder.configure(decoderConfig); decoderNeedsKey = true; askKeyframe(); }
        }
    };
    const sendNacks = list => {
        if (!list.length) return;
        receiveChannel.send(sequenceList(FrameType.NackRequest, videoStream, list));
        if (measuring) counters.nackRequests++;
    };
    const audioSeen = new Set();
    receiver.onmessage = frame => {
        if (frame[0] !== FrameType.MediaFrame || frame.length < MEDIA_HEADER) return;
        rate.received += frame.length;
        const view = new DataView(frame.buffer);
        const seq = view.getUint32(17, true), timestamp = view.getUint32(21, true);
        const isVideo = frame.subarray(1, 17).every((b, i) => b === videoStream[i]);
        let plain;
        try { plain = rx.decrypt(alice, frame.subarray(MEDIA_HEADER), isVideo ? videoStreamId : audioStreamId, seq, timestamp); }
        catch { return; } // A duplicate (a retransmission that raced its original) or garbage.
        if (!isVideo) {
            const sentAt = rate.audioSentAt.get(seq);
            if (sentAt !== undefined) {
                const transit = now() - sentAt, at = now();
                if (at - rate.floorSince >= 10000) { rate.previousFloor = rate.transitFloor; rate.transitFloor = Infinity; rate.floorSince = at; }
                rate.transitFloor = Math.min(rate.transitFloor, transit);
                rate.lastTransit = transit;
            }
            if (measuring && !audioSeen.has(seq)) counters.audioReceived++;
            audioSeen.add(seq);
            if (audioDecoder.state === 'configured' && audioDecoder.decodeQueueSize < 8)
                audioDecoder.decode(new EncodedAudioChunk({ type: 'key', timestamp: Math.round(timestamp * 1e6 / 48000), data: plain }));
            return;
        }
        const ready = [];
        recovery.push(seq, plain, now(), ready);
        decode(ready);
    };
    const poller = setInterval(() => {
        const ready = [], nacks = [];
        recovery.poll(now(), ready, nacks);
        sendNacks(nacks);
        decode(ready);
    }, 20);
    // The receiver's round trip to the relay, as ICE measures it: what the product's recovery window follows.
    const rttTimer = setInterval(() => { if (receiver.rttMs) { rtt = receiver.rttMs; recovery.configure(rtt); } }, 1000);
    recovery.configure(rtt);

    await sleep(options.warmupSeconds * 1000);
    await window.benchMark?.('start');

    measuring = true;
    metrics.measuring = true;
    const started = now();
    await sleep(options.seconds * 1000);
    measuring = false;
    metrics.measuring = false;
    const elapsed = (now() - started) / 1000;
    await window.benchMark?.('end');

    clearInterval(poller); clearInterval(rttTimer); clearInterval(rateTimer);
    stopVideo(); stopAudio();
    try { encoder.close(); audioEncoder.close(); decoder.close(); audioDecoder.close(); } catch { }
    tx.dispose(); rx.dispose();
    for (const side of [sender, receiver]) { side.peer.close(); side.socket.close(); }
    return {
        seconds: elapsed, rttMs: rtt, compact: options.compact, nack: options.nack,
        audioPacketMs: rate.frameMs, videoKbpsAtEnd: rate.videoKbps, rateTrace: rate.trace.filter((_, i) => i % 4 === 0),
        sframeBytesPerFrame: counters.overhead,
        mediaBytes: counters.mediaBytes,
        packetsSent: counters.messages,
        channelBytesSent: counters.messageBytes, relayed: true,
        audioPacketsSent: counters.audioSent,
        audioDelivered: counters.audioSent ? +(100 * Math.min(counters.audioReceived, counters.audioSent) / counters.audioSent).toFixed(1) : null,
        videoTargetKbps: options.videoKbps,
        videoSentKbps: null,
        retransmitted: counters.retransmitted, nackRequests: counters.nackRequests, keyframeRequests: counters.keyRequests,
        droppedAtChannel: counters.dropped, keyframes: counters.keyframes, temporalMode: videoConfig.scalabilityMode,
        codec: videoConfig.codec, recovery: recovery.stats,
        picture: metrics.summary(elapsed),
    };
}

window.runBenchmark = async options => {
    const source = createSource(options);
    try {
        if (source.audioContext.state !== 'running') await source.audioContext.resume();
        return options.mode === 'native' ? await runNative(options, source) : await runBolt(options, source);
    } finally { source.stop(); }
};
window.benchReady = true;
