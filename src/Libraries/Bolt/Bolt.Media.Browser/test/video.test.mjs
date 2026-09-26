import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// Values built inside the vm carry the vm's prototypes; compare them by value.
const plain = value => JSON.parse(JSON.stringify(value));
const source = readFileSync(new URL('../wwwroot/bolt-media.js', import.meta.url), 'utf8')
    .replaceAll('export ', '').replaceAll('import.meta.url', "'https://example.test/bolt-media.js'");

// `support` decides what each probed configuration answers, so a test can describe a device
// (hardware AV1, software VP9, H.264 only) without pretending to run a real encoder.
// `capture` shapes the capture primitives the browser has: 'processor' is Chromium, 'rvfc' is
// Safari (MediaStreamTrackProcessor is worker-only there, so undefined on the page) and 'none' is
// a browser too old for either.
function fixture({ support = () => ({ supported: true }), hardwareConcurrency = 8, battery = null, cameras = 1,
    capture = 'processor', frameFromElement = true, clock = () => performance.now() } = {}) {
    const listeners = new Map();
    const stats = { encoded: [], decoded: [], stopped: 0, opened: [], invoked: [], closedFrames: 0, frames: [] };

    class Frame {
        constructor(source, init) {
            if (source?.isVideoElement && !frameFromElement) throw new TypeError('unsupported source');
            this.source = source; this.timestamp = init?.timestamp; this.closed = false;
            // Canvas frames start upright. Pixel-level orientation is also checked in a real browser.
            this.rotation = init?.rotation ?? source?.rotation ?? 0;
            this.flip = init?.flip ?? source?.flip ?? false;
            this.displayWidth = source?.displayWidth ?? source?.width ?? source?.videoWidth ?? 0;
            this.displayHeight = source?.displayHeight ?? source?.height ?? source?.videoHeight ?? 0;
            stats.frames.push(this);
        }
        close() { this.closed = true; stats.closedFrames++; }
    }

    /// A canvas that remembers what was drawn on it, so a test can read the transform the pipeline
    /// applied rather than trusting that it called something.
    const canvases = [];
    const makeCanvas = () => {
        const canvas = { width: 0, height: 0, ops: [], isCanvas: true };
        canvas.context = {
            save: () => canvas.ops.push(['save']),
            restore: () => canvas.ops.push(['restore']),
            translate: (x, y) => canvas.ops.push(['translate', x, y]),
            rotate: angle => canvas.ops.push(['rotate', angle]),
            scale: (x, y) => canvas.ops.push(['scale', x, y]),
            drawImage: (...args) => canvas.ops.push(['drawImage', ...args])
        };
        canvas.getContext = () => canvas.context;
        canvases.push(canvas);
        return canvas;
    };

    /// One camera frame as MediaStreamTrackProcessor would hand it over: sensor-oriented pixels
    /// with the display rotation carried alongside as metadata.
    const queue = [];
    let waiting = null;
    const pushFrame = ({ displayWidth = 1280, displayHeight = 720, rotation = 0, flip = false, timestamp = 0 } = {}) => {
        const frame = { displayWidth, displayHeight, rotation, flip, timestamp, closed: false, camera: true,
            close() { this.closed = true; stats.closedFrames++; } };
        stats.frames.push(frame);
        if (waiting) { const resolve = waiting; waiting = null; resolve({ value: frame, done: false }); }
        else queue.push(frame);
        return frame;
    };

    // Stands in for the <video> the fallback reads through: one pending frame callback at a time,
    // fired by hand so a test controls exactly when a frame arrives.
    class FakeVideoElement {
        constructor() {
            this.isVideoElement = true; this.videoWidth = 1280; this.videoHeight = 720; this.readyState = 2;
            this.style = {}; this.srcObject = null; this.currentTime = 0; this.paused = true; this.attached = false;
            this.callbacks = new Map(); this.nextId = 1; this.cancelled = [];
        }
        setAttribute() { }
        requestVideoFrameCallback(callback) { const id = this.nextId++; this.callbacks.set(id, callback); return id; }
        cancelVideoFrameCallback(id) { this.cancelled.push(id); this.callbacks.delete(id); }
        play() { this.paused = false; return Promise.resolve(); }
        pause() { this.paused = true; }
        remove() { this.attached = false; }
        /// Deliver one decoded frame, as rVFC would.
        emit(mediaTime) {
            const entry = [...this.callbacks.entries()].at(-1);
            if (!entry) return false;
            this.callbacks.delete(entry[0]);
            entry[1](mediaTime * 1000, { mediaTime });
            return true;
        }
    }
    let element = null;

    class Encoder {
        static async isConfigSupported(config) {
            assert.ok(['no-preference', 'prefer-hardware', 'prefer-software'].includes(config.hardwareAcceleration));
            return { supported: !!support(config, 'encode'), config };
        }
        constructor(callbacks) { this.callbacks = callbacks; this.state = 'unconfigured'; this.encodeQueueSize = 0; }
        configure(config) { this.state = 'configured'; this.config = config; }
        encode(frame, options) {
            stats.encoded.push({ frame, options, config: this.config });
            this.callbacks.output({ byteLength: 8, type: options?.keyFrame ? 'key' : 'delta', timestamp: frame.timestamp,
                copyTo(target) { target.fill(1); } }, {});
        }
        close() { this.state = 'closed'; }
    }
    class Decoder {
        static async isConfigSupported(config) { return { supported: !!support(config, 'decode'), config }; }
        constructor(callbacks) { this.callbacks = callbacks; this.state = 'unconfigured'; this.decodeQueueSize = 0; }
        configure(config) { if (!support(config, 'decode')) throw new Error('unsupported decoder'); this.state = 'configured'; this.config = config; }
        decode(chunk) { stats.decoded.push(chunk); }
        close() { this.state = 'closed'; }
    }

    const makeTrack = () => {
        const track = {
            live: true, settings: { deviceId: 'cam-front', facingMode: 'user' }, handlers: {},
            getSettings() { return track.settings; },
            addEventListener(name, handler) { track.handlers[name] = handler; },
            stop() { track.live = false; stats.stopped++; }
        };
        return track;
    };
    let track = null;
    const sandbox = {
        console, URL, Uint8Array, Float32Array, Math, Date, JSON, Promise, Set, Map, Number, performance: { now: clock }, isSecureContext: true,
        AudioEncoder: class { static async isConfigSupported() { return { supported: true }; } },
        AudioDecoder: class { static async isConfigSupported() { return { supported: true }; } },
        AudioContext: class {}, AudioWorkletNode: class {},
        VideoEncoder: Encoder, VideoDecoder: Decoder,
        EncodedVideoChunk: class { constructor(data) { Object.assign(this, data); } },
        VideoFrame: Frame,
        document: { hidden: false, addEventListener: (name, handler) => listeners.set(name, handler),
            removeEventListener: name => listeners.delete(name),
            body: { appendChild: node => { node.attached = true; } },
            createElement: name => name === 'video' ? (element = new FakeVideoElement()) : makeCanvas() },
        addEventListener: (name, handler) => listeners.set(name, handler),
        removeEventListener: name => listeners.delete(name),
        navigator: {
            hardwareConcurrency,
            getBattery: battery ? async () => battery : undefined,
            mediaDevices: {
                async getUserMedia(constraints) {
                    if (cameras === 0) throw new Error('NotAllowedError');
                    stats.opened.push(constraints);
                    track = makeTrack();
                    if (constraints.video?.deviceId) track.settings.deviceId = constraints.video.deviceId.exact;
                    if (constraints.video?.facingMode) track.settings.facingMode = constraints.video.facingMode.ideal;
                    return { getVideoTracks: () => [track], getTracks: () => [track] };
                },
                async enumerateDevices() { return []; }
            }
        }
    };
    if (capture === 'processor') sandbox.MediaStreamTrackProcessor = class {
        constructor({ track }) {
            this.readable = { getReader: () => ({
                // Resolves as soon as a test pushes a frame, and otherwise waits forever - which is
                // exactly what a camera that has produced nothing yet does.
                read: () => queue.length ? Promise.resolve({ value: queue.shift(), done: false })
                    : new Promise(resolve => { waiting = resolve; }),
                cancel() { waiting = null; }
            }) };
            this.track = track;
        }
    };
    if (capture === 'rvfc') sandbox.HTMLVideoElement = FakeVideoElement;
    sandbox.globalThis = sandbox;
    vm.createContext(sandbox);
    vm.runInContext(source + `
this.pipeline = createVideoPipeline();
this.probe = probeVideoCodecs;
this.ceiling = videoDeviceCeiling;
this.capabilities = checkVideoCapabilities;
this.strategyOf = typeof videoCaptureStrategy === 'function' ? videoCaptureStrategy : () => 'absent';
this.codecString = videoCodecString;
this.fitTier = fitTierToSource;`, sandbox);

    const host = { invokeMethodAsync: async (...args) => { stats.invoked.push(args); } };
    const canvas = { width: 0, height: 0, getContext: () => ({ drawImage() {} }) };
    return { p: sandbox.pipeline, sandbox, stats, listeners, host, canvas, pushFrame, canvases,
        get track() { return track; }, get video() { return element; } };
}

const tier = { width: 1280, height: 720, bitrate: 1500, framerate: 30 };
const init = (f, codec = 'h264') => f.p.initEncoder(codec, tier.width, tier.height, tier.bitrate, tier.framerate, 2);

// ── Codec probing ──

test('probing reports codec limits without treating an acceleration preference as proof', async () => {
    // H.264 accepts every preference; WebCodecs still cannot prove hardware use.
    const f = fixture({ support: (config, kind) => {
        const is = name => config.codec.startsWith(name);
        if (is('av01')) return false;
        if (is('vp09')) return kind === 'decode' || (config.height <= 720);
        return true;
    } });
    const probed = await f.sandbox.probe(1080);
    const by = Object.fromEntries(probed.map(x => [x.codec, x]));
    assert.deepEqual(plain(by.av1), { codec: 'av1', encode: false, decode: false, hardware: false, maxHeight: 0, decodeMaxHeight: 0 });
    assert.equal(by.vp9.encode, true);
    assert.equal(by.vp9.hardware, false, 'acceleration is unknown, so use conservative limits');
    assert.equal(by.vp9.maxHeight, 720);
    assert.equal(by.h264.hardware, false, 'support for a preference does not guarantee hardware');
    assert.equal(by.h264.maxHeight, 1080);
});

test('probing opens no camera', async () => {
    const f = fixture();
    await f.sandbox.probe(1080);
    await f.sandbox.capabilities();
    assert.equal(f.stats.opened.length, 0);
});

test('a device with no video encoder at all is reported unsupported, not guessed at', async () => {
    const f = fixture({ support: () => false });
    const capabilities = await f.sandbox.capabilities();
    assert.equal(capabilities.supported, false);
    assert.match(capabilities.reason, /no video encoder/i);
});

test('a low unplugged battery lowers the ceiling, but core count is not a video codec limit', async () => {
    assert.equal(await fixture({ hardwareConcurrency: 8 }).sandbox.ceiling(), 2160);
    assert.equal(await fixture({ hardwareConcurrency: 4 }).sandbox.ceiling(), 2160);
    assert.equal(await fixture({ battery: { charging: false, level: 0.15 } }).sandbox.ceiling(), 540);
    assert.equal(await fixture({ battery: { charging: true, level: 0.05 } }).sandbox.ceiling(), 2160,
        'on the charger the battery is not a reason to shrink the picture');
});

// Raw H.264 chunks carry no decoder description, so the bitstream has to be Annex B or the
// far side renders garbage. VP9 and AV1 need no such container hint.
test('H.264 is configured as Annex B and the other codecs are left alone', async () => {
    const f = fixture();
    await init(f, 'h264');
    assert.deepEqual(plain(f.p.config.avc), { format: 'annexb' });
    await init(f, 'av1');
    assert.equal(f.p.config.avc, undefined);
    assert.match(f.p.config.codec, /^av01\./);
});

// Chrome does not treat 'prefer-hardware' as a soft hint: on a machine with no hardware encoder
// it reports the configuration unsupported and configure() throws. Verified against real Chrome,
// where all three codecs answered false for prefer-hardware and true for no-preference.
test('a device with no hardware encoder still gets a software one', async () => {
    const f = fixture({ support: config => config.hardwareAcceleration !== 'prefer-hardware' });
    await init(f, 'vp9');
    assert.equal(f.p.config.hardwareAcceleration, 'no-preference');
    assert.equal(f.p.encoder.state, 'configured');
});

test('hardware is still asked for first where it exists', async () => {
    const f = fixture();
    await init(f, 'h264');
    assert.equal(f.p.config.hardwareAcceleration, 'prefer-hardware');
});

test('a codec no acceleration can encode is refused with a plain message', async () => {
    const f = fixture({ support: () => false });
    await assert.rejects(() => init(f, 'av1'), /cannot encode video/i);
});

// ── Camera lifecycle ──

test('creating the pipeline and configuring the encoder never open the camera', async () => {
    const f = fixture();
    await init(f);
    assert.equal(f.stats.opened.length, 0);
    assert.equal(f.p.captureRunning, false);
});

test('starting capture opens exactly one camera and stopping releases it', async () => {
    const f = fixture();
    await init(f);
    const state = await f.p.startCapture(f.host, { facingMode: 'environment' });
    assert.equal(state.capturing, true);
    assert.equal(state.facingMode, 'environment');
    assert.equal(f.stats.opened.length, 1);
    f.p.stopCapture();
    assert.equal(f.stats.stopped, 1, 'track.stop() is what turns the hardware indicator off');
    assert.equal(f.p.captureRunning, false);
});

test('hiding the page releases the camera and says so', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.sandbox.document.hidden = true;
    f.listeners.get('visibilitychange')();
    assert.equal(f.p.captureRunning, false);
    assert.equal(f.stats.stopped, 1);
    assert.deepEqual(f.stats.invoked.at(-1), ['OnVideoCaptureStopped', 'hidden']);
});

test('a camera revoked from the browser UI ends the send instead of hanging', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.track.handlers.ended();
    assert.equal(f.p.captureRunning, false);
    assert.deepEqual(f.stats.invoked.at(-1), ['OnVideoCaptureStopped', 'ended']);
});

test('a refused camera leaves nothing running', async () => {
    const f = fixture({ cameras: 0 });
    await init(f);
    await assert.rejects(() => f.p.startCapture(f.host, {}));
    assert.equal(f.p.captureRunning, false);
    assert.equal(f.p.mediaStream, null);
});

test('a bandwidth-driven restart comes back on the camera the user chose', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, { deviceId: 'cam-back' });
    f.p.stopCapture();
    const resumed = await f.p.startCapture(f.host);
    assert.equal(resumed.deviceId, 'cam-back');
    assert.equal(f.stats.opened.at(-1).video.deviceId.exact, 'cam-back');
});

test('dispose releases the camera, the decoders and the page listeners', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.p.addRemote('s1', f.canvas, 'h264', f.host);
    await f.p.dispose();
    assert.equal(f.stats.stopped, 1);
    assert.equal(f.p.remotes.size, 0);
    assert.equal(f.listeners.has('visibilitychange'), false);
});

// ── Encode, adapt, decode ──

test('encoded pictures reach .NET with a frame id and a keyframe flag', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.p.encoder.encode({ timestamp: 5000, close() {} }, { keyFrame: true });
    f.p.encoder.encode({ timestamp: 38000, close() {} }, { keyFrame: false });
    const sent = f.stats.invoked.filter(x => x[0] === 'OnVideoEncoded');
    assert.equal(sent.length, 2);
    assert.equal(sent[0][2], true);
    assert.equal(sent[1][2], false);
    assert.deepEqual([sent[0][3], sent[1][3]], [1, 2], 'frame ids must not restart mid-call');
    assert.equal(sent[1][4], 38000);
});

test('a tier change reconfigures the encoder and forces a fresh keyframe', async () => {
    const f = fixture();
    await init(f);
    assert.equal(await f.p.applyTier(1280, 720, 1500, 30), false, 'the same tier is not a reconfigure');
    assert.equal(await f.p.applyTier(640, 360, 400, 20), true);
    assert.equal(f.p.encoder.config.height, 360);
    assert.equal(f.p.encoder.config.bitrate, 400000);
    assert.equal(f.p.forceKeyframe, true, 'the far side cannot decode a new size against an old reference');
});

test('stats report the measured frame rate and the encoder backlog', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.p.encoder.encodeQueueSize = 4;
    assert.equal(f.p.getStats().backlog, 4);
});

test('a remote tile decodes only once it has a keyframe to start from', async () => {
    const f = fixture();
    await init(f);
    f.p.addRemote('s1', f.canvas, 'vp9', f.host);
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 100, false), false, 'deltas before a keyframe are wasted work');
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 200, true), true);
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 300, false), true);
    assert.equal(f.stats.decoded.length, 2);
    assert.match(f.p.remotes.get('s1').decoder.config.codec, /^vp09\./);
});

test('one participant per decoder, and removing a tile closes it', async () => {
    const f = fixture();
    await init(f);
    f.p.addRemote('s1', f.canvas, 'h264', f.host);
    f.p.addRemote('s2', f.canvas, 'av1', f.host);
    assert.equal(f.p.remotes.size, 2);
    const closed = f.p.remotes.get('s1').decoder;
    f.p.removeRemote('s1');
    assert.equal(closed.state, 'closed');
    assert.equal(f.p.remotes.size, 1);
});

test('a decoder that errors is rebuilt and the sender is asked for a keyframe', async () => {
    const f = fixture();
    await init(f);
    f.p.addRemote('s1', f.canvas, 'h264', f.host);
    const first = f.p.remotes.get('s1').decoder;
    first.callbacks.error(new Error('bad bitstream'));
    const rebuilt = f.p.remotes.get('s1').decoder;
    assert.notEqual(rebuilt, first);
    assert.equal(rebuilt.state, 'configured');
    assert.equal(f.p.remotes.get('s1').primed, false);
    assert.deepEqual(f.stats.invoked.at(-1), ['OnVideoDecodeFailed', 's1']);
});

// ── Capture without MediaStreamTrackProcessor (Safari, Firefox) ──

// Chromium exposes MediaStreamTrackProcessor on Window; Safari 18 only inside a DedicatedWorker,
// so on the page it is undefined. Gating the probe on it handed iOS an empty ladder, which the
// user saw as "this device cannot encode video" on a phone that encodes H.264 in hardware.
test('a browser without MediaStreamTrackProcessor still gets a real codec ladder', async () => {
    const f = fixture({ capture: 'rvfc' });
    assert.equal(f.sandbox.strategyOf(), 'rvfc');
    const probed = await f.sandbox.probe(1080);
    assert.ok(probed.some(x => x.encode), 'the encoder was never asked before; now it is');
    const capabilities = await f.sandbox.capabilities();
    assert.equal(capabilities.supported, true);
    assert.equal(capabilities.reason, null);
});

// WebKit accepts avc1.* and vp09.00*, refuses av01 unless AV1 hardware is present, and throws a
// TypeError on 'require-hardware' because it is not a WebCodecs enum value at all.
test('a Safari-shaped probe finds H.264 and VP9, never AV1, and never claims hardware', async () => {
    const f = fixture({ capture: 'rvfc', support: (config) => {
        if (config.hardwareAcceleration === 'require-hardware') throw new TypeError('not a valid enum value');
        return config.codec.startsWith('avc1.') || config.codec.startsWith('vp09.00');
    } });
    const by = Object.fromEntries((await f.sandbox.probe(1080)).map(x => [x.codec, x]));
    assert.equal(by.av1.encode, false);
    assert.equal(by.vp9.encode, true);
    assert.equal(by.h264.encode, true);
    assert.equal(by.h264.maxHeight, 1080);
    assert.equal(by.h264.hardware, false, 'require-hardware throws rather than answering, everywhere');
});

test('the frame-callback path paints displayed pixels into one reusable canvas and closes frames', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    const state = await f.p.startCapture(f.host, {});
    assert.equal(state.strategy, 'rvfc');
    assert.equal(f.video.attached, true, 'iOS stalls a detached element, and a stalled one never calls back');
    assert.equal(f.video.srcObject, f.p.mediaStream);
    for (let i = 0; i < 5; i++) f.video.emit(i / 30);
    assert.equal(f.stats.encoded.length, 5);
    assert.equal(f.stats.frames.length, 5);
    assert.ok(f.stats.frames.every(x => x.closed), 'one leaked VideoFrame per picture exhausts memory in seconds');
    assert.ok(f.stats.frames.every(x => x.source === f.p.captureCanvas));
    assert.equal(f.canvases.length, 1, 'reuse the canvas across pictures');
    assert.equal(f.stats.frames[3].timestamp, Math.round(3e6 / 30), 'mediaTime seconds become WebCodecs microseconds');
    assert.equal(f.stats.encoded[0].options.keyFrame, true, 'the first picture of a send must be a keyframe');
    assert.equal(f.video.callbacks.size, 1, 'the loop re-arms itself for the next frame');
});

test('the fallback drops the newest frame rather than queueing latency, and allocates none', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    await f.p.startCapture(f.host, {});
    f.video.emit(0);
    f.p.encoder.encodeQueueSize = 2;
    f.video.emit(1 / 30);
    f.video.emit(2 / 30);
    assert.equal(f.stats.encoded.length, 1);
    assert.equal(f.p.stats.dropped, 2);
    assert.equal(f.stats.frames.length, 1, 'a frame that will be dropped is never built in the first place');
    assert.equal(f.video.callbacks.size, 1, 'a dropped frame must not end the capture');
});

test('stopping cancels the pending frame callback and hands the element back', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    await f.p.startCapture(f.host, {});
    const video = f.video;
    const late = [...video.callbacks.values()][0];
    f.p.stopCapture();
    assert.equal(video.cancelled.length, 1);
    assert.equal(video.srcObject, null);
    assert.equal(video.paused, true);
    assert.equal(video.attached, false);
    assert.equal(f.stats.stopped, 1, 'track.stop() is still what turns the hardware indicator off');
    // A callback already queued by the browser fires after the cancel; the generation must stop it.
    late(0, { mediaTime: 99 });
    assert.equal(f.stats.encoded.length, 0);
    assert.equal(f.stats.frames.length, 0);
});

test('hiding the page cancels the frame callback, and a restart is not fed by the old one', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    await f.p.startCapture(f.host, {});
    const stale = [...f.video.callbacks.values()][0];
    f.sandbox.document.hidden = true;
    f.listeners.get('visibilitychange')();
    assert.equal(f.p.captureRunning, false);
    assert.deepEqual(f.stats.invoked.at(-1), ['OnVideoCaptureStopped', 'hidden']);
    f.sandbox.document.hidden = false;
    await f.p.startCapture(f.host, {});
    stale(0, { mediaTime: 99 });
    assert.equal(f.stats.encoded.length, 0, 'a callback from the previous session pushes nothing');
    f.video.emit(0);
    assert.equal(f.stats.encoded.length, 1);
});

test('dispose on the fallback path releases the element and the camera', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    await f.p.startCapture(f.host, {});
    const video = f.video;
    await f.p.dispose();
    assert.equal(f.stats.stopped, 1);
    assert.equal(video.srcObject, null);
    assert.equal(video.cancelled.length, 1);
    assert.equal(f.p.captureVideo, null);
});

// The direct path is the shipped one. This only proves the degrade exists for a browser that
// refuses an element source, so such a device loses a pixel copy rather than the whole call.
test('a browser that refuses a VideoFrame from the element degrades to a canvas, still closing every frame', async () => {
    const f = fixture({ capture: 'rvfc', frameFromElement: false });
    await init(f);
    await f.p.startCapture(f.host, {});
    f.video.emit(0);
    f.video.emit(1 / 30);
    assert.equal(f.stats.encoded.length, 2);
    assert.ok(f.p.captureCanvas);
    assert.ok(f.stats.frames.every(x => x.closed && x.source !== f.video));
    f.p.stopCapture();
    assert.equal(f.p.captureCanvas, null, 'the canvas would otherwise keep the last picture of the camera');
});

// ── What the notice says ──

// "This device cannot encode video" reads as a hardware limit. When the cause is a browser without
// the capture or codec API, the only useful advice is to try a different one.
test('a browser with no capture primitive at all names the browser, not the hardware', async () => {
    const f = fixture({ capture: 'none' });
    assert.equal(f.sandbox.strategyOf(), 'none');
    const capabilities = await f.sandbox.capabilities();
    assert.equal(capabilities.supported, false);
    assert.match(capabilities.reason, /this browser cannot send video/i);
    assert.match(capabilities.reason, /try the latest Safari/i);
});

test('a browser that can capture but encodes nothing still blames the device, not the browser', async () => {
    const f = fixture({ capture: 'rvfc', support: () => false });
    const capabilities = await f.sandbox.capabilities();
    assert.equal(capabilities.supported, false);
    assert.match(capabilities.reason, /no video encoder/i);
});


// ── Aspect ratio ──
// A ladder tier is a bitrate budget written as a landscape box. VideoEncoder does not letterbox a
// frame that does not match its configured raster, it *scales* it, so a portrait phone encoded into
// 1280x720 arrives genuinely squashed - and the receiver then letterboxes the squash, which is the
// stretched picture between black bands that was reported from a real device.

test('a tier is reshaped to the camera aspect rather than squashing it into a landscape box', () => {
    const f = fixture();
    const fit = f.sandbox.fitTier;
    assert.deepEqual(plain(fit(1280, 720, 720, 1280)), { width: 720, height: 1280 }, 'a portrait phone stays portrait');
    assert.deepEqual(plain(fit(1280, 720, 1280, 720)), { width: 1280, height: 720 }, '16:9 already fits the tier');
    assert.deepEqual(plain(fit(1280, 720, 640, 480)), { width: 640, height: 480 }, 'a small camera is not upscaled');
    assert.deepEqual(plain(fit(3840, 2160, 720, 1280)), { width: 720, height: 1280 }, '4K is a ceiling, not artificial detail');
    assert.deepEqual(plain(fit(640, 360, 720, 1280)), { width: 360, height: 640 }, 'the short edge is the quality knob');
    assert.deepEqual(plain(fit(1280, 720, 0, 0)), { width: 1280, height: 720 }, 'no camera yet means no reshaping');
});

test('a portrait camera is encoded portrait on the Chromium path', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    assert.equal(f.p.encoder.config.width, 1280, 'before any frame, the tier is all there is to go on');
    f.pushFrame({ displayWidth: 720, displayHeight: 1280 });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.p.encoder.config.width, 720);
    assert.equal(f.p.encoder.config.height, 1280);
    // The reshape happens before this same frame is encoded, so the keyframe the new raster needs
    // is the frame that caused it - the far side never sees the new size against an old reference.
    assert.equal(f.stats.encoded.at(-1).options.keyFrame, true);
    assert.equal(f.stats.encoded.at(-1).config.height, 1280);
});

test('a portrait camera is encoded portrait on the Safari path too', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    await f.p.startCapture(f.host, {});
    f.video.videoWidth = 720; f.video.videoHeight = 1280;
    f.video.emit(0);
    assert.equal(f.p.encoder.config.width, 720);
    assert.equal(f.p.encoder.config.height, 1280);
});

test('the ladder still moves, and each tier lands at the camera aspect', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.pushFrame({ displayWidth: 720, displayHeight: 1280 });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(await f.p.applyTier(640, 360, 400, 20), true, 'adaptation is untouched by the reshaping');
    assert.equal(f.p.encoder.config.width, 360);
    assert.equal(f.p.encoder.config.height, 640);
    assert.equal(f.p.encoder.config.bitrate, 400000);
});

test('turning the phone over mid-call re-fits the encoder', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.pushFrame({ displayWidth: 720, displayHeight: 1280 });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.p.encoder.config.height, 1280);
    f.pushFrame({ displayWidth: 1280, displayHeight: 720, timestamp: 33334 });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.p.encoder.config.width, 1280);
    assert.equal(f.p.encoder.config.height, 720, 'landscape again, not stuck on the portrait raster');
});

test('switching camera does not fit the new one to the old one aspect', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, { facingMode: 'user' });
    f.pushFrame({ displayWidth: 720, displayHeight: 1280 });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.p.sourceHeight, 1280);
    await f.p.startCapture(f.host, { facingMode: 'environment' });
    assert.equal(f.p.sourceWidth, 0, 'the back camera has its own shape and is measured fresh');
    assert.equal(f.p.sourceHeight, 0);
});

// ── Orientation ──
// Android looked rotated and iOS did not, because the two capture strategies differ: Chromium's
// MediaStreamTrackProcessor yields sensor-oriented pixels with the rotation as metadata that
// VideoEncoder then drops, while Safari's rVFC reads a <video> element that has already applied it.

test('a rotated Android frame is turned upright before it is encoded', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    const camera = f.pushFrame({ displayWidth: 720, displayHeight: 1280, rotation: 90 });
    await new Promise(resolve => setImmediate(resolve));

    const drawn = f.canvases.find(c => c.ops.length);
    assert.ok(drawn, 'a rotated frame has to be redrawn: the encoder ignores the metadata');
    assert.equal(drawn.width, 720, 'the canvas is the upright size, not the sensor size');
    assert.equal(drawn.height, 1280);
    assert.equal(drawn.ops.filter(op => op[0] === 'rotate').length, 0, 'drawImage alone applies metadata');
    assert.equal(drawn.ops.find(op => op[0] === 'drawImage')[1], camera);

    assert.equal(camera.closed, true, 'the sensor frame is released once its pixels have been copied');
    const encoded = f.stats.encoded.at(-1).frame;
    assert.notEqual(encoded, camera, 'what reaches the encoder is the upright copy');
    assert.equal(encoded.source, drawn);
    assert.equal(f.p.encoder.config.width, 720, 'and the raster follows the upright shape');
    assert.equal(f.p.encoder.config.height, 1280);
});

test('frame metadata is applied once by drawImage without a second transform', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.pushFrame({ displayWidth: 720, displayHeight: 1280, rotation: 270 });
    await new Promise(resolve => setImmediate(resolve));
    // drawImage honours the original rotation metadata on Chromium.
    const drawn = f.canvases.find(c => c.ops.length);
    const source = drawn.ops.find(op => op[0] === 'drawImage')[1];
    assert.equal(source.rotation, 270, 'drawImage applies the original metadata');
    assert.equal(drawn.ops.some(op => op[0] === 'rotate'), false);
    assert.equal(source.flip, false);
    assert.equal(source.closed, true, 'the camera frame is released after copying');
});

test('an upright Chromium frame is encoded with no copy at all', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    const camera = f.pushFrame({ displayWidth: 1280, displayHeight: 720 });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.stats.encoded.at(-1).frame, camera, 'no rotation means no canvas and no pixel copy');
    assert.ok(f.canvases.every(c => c.ops.length === 0));
    assert.equal(camera.closed, true);
});

test('a mirrored frame is unmirrored into the pixels as well', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    f.pushFrame({ displayWidth: 1280, displayHeight: 720, flip: true });
    await new Promise(resolve => setImmediate(resolve));
    const drawn = f.canvases.find(c => c.ops.length);
    assert.ok(drawn, 'flip is metadata too, and the encoder drops it the same way');
    assert.equal(drawn.ops.find(op => op[0] === 'drawImage')[1].flip, true);
    assert.equal(drawn.ops.some(op => op[0] === 'scale'), false);
});

test('Safari paints upright element pixels, rather than retaining sensor metadata', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    await f.p.startCapture(f.host, {});
    f.video.videoWidth = 720; f.video.videoHeight = 1280;
    f.video.emit(0);
    assert.equal(f.stats.encoded.length, 1);
    const canvas = f.p.captureCanvas;
    assert.equal(f.stats.encoded[0].frame.source, canvas);
    assert.deepEqual([canvas.width, canvas.height], [720, 1280]);
    assert.equal(canvas.ops.find(op => op[0] === 'drawImage')[1], f.video);
    assert.equal(canvas.ops.some(op => op[0] === 'rotate'), false);
});


test('camera off/on preserves frame numbers on the existing stream', async () => {
    const f = fixture(); await init(f); await f.p.startCapture(f.host, {});
    f.p.encoder.encode({ timestamp: 1 }, { keyFrame: true });
    f.p.stopCapture(); await init(f); await f.p.startCapture(f.host, {});
    f.p.encoder.encode({ timestamp: 2 }, { keyFrame: true });
    assert.deepEqual(f.stats.invoked.filter(x => x[0] === 'OnVideoEncoded').map(x => x[3]), [1, 2]);
});

for (const reason of ['backlog', 'missing-picture']) test(`decoder recovers from ${reason} before accepting deltas`, async () => {
    const f = fixture(); await init(f); f.p.addRemote('s1', f.canvas, 'h264', f.host);
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 100, true), true);
    const before = f.p.remotes.get('s1').decoder;
    if (reason === 'backlog') before.decodeQueueSize = 6;
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 200, false, reason === 'missing-picture'), false);
    assert.equal(before.state, 'closed');
    assert.deepEqual(f.stats.invoked.at(-1), ['OnVideoDecodeFailed', 's1']);
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 300, false), false);
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 400, true), true);
    assert.equal(f.p.decodeFrame('s1', new Uint8Array([1]), 500, false), true);
});

for (const capture of ['processor', 'rvfc']) test(`${capture} paces a 60fps camera at a 30fps target before conversion`, async () => {
    const f = fixture({ capture }); await init(f); await f.p.startCapture(f.host, {});
    for (let i = 0; i < 60; i++) {
        if (capture === 'rvfc') f.video.emit(i / 60);
        else { f.pushFrame({ timestamp: Math.round(i * 1e6 / 60) }); await new Promise(resolve => setImmediate(resolve)); }
    }
    assert.equal(f.stats.encoded.length, 30);
    assert.ok(f.stats.frames.every(x => x.closed));
});

test('4K60 asks for a sufficient H264 level and the portrait equivalent uses the same level', async () => {
    const f = fixture(); await f.p.initEncoder('h264', 3840, 2160, 21000, 60, 2);
    assert.equal(f.p.config.codec, 'avc1.640034');
    f.p._noteSource(2160, 3840);
    assert.equal(f.p.config.codec, 'avc1.640034');
    assert.deepEqual([f.p.config.width, f.p.config.height, f.p.config.framerate], [2160, 3840, 60]);
});

test('H264 receiver reads the profile and level from the keyframe SPS', () => {
    const f = fixture(); f.p.addRemote('s', f.canvas, 'h264', f.host);
    const key = new Uint8Array([0,0,0,1,0x67,0x64,0,0x34,1,2]);
    assert.equal(f.p.decodeFrame('s', key, 0, true), true);
    assert.equal(f.p.remotes.get('s').decoder.config.codec, 'avc1.640034');
});


for (const [width, height] of [[1920, 1080], [2560, 1440], [1440, 2560], [1440, 1920]])
test(`persistent H264 buffering at ${width}x${height} tries software once and restores native if it falls behind`, async () => {
    const f = fixture(); f.p.addRemote('s', f.canvas, 'h264', f.host);
    const remote = f.p.remotes.get('s');
    const frame = { displayWidth: width, displayHeight: height };
    for (let i = 0; i < 11; i++) await f.p._considerDecoderLatency(remote, frame, 200);
    assert.equal(remote.software, false);
    await f.p._considerDecoderLatency(remote, frame, 200);
    assert.equal(remote.decoder.config.hardwareAcceleration, 'prefer-software');
    assert.deepEqual(f.stats.invoked.at(-1), ['OnVideoDecodeFailed', 's']);
    for (let i = 0; i < 12; i++) await f.p._considerDecoderLatency(remote, frame, 200);
    assert.equal(remote.decoder.config.hardwareAcceleration, 'prefer-hardware');
    for (let i = 0; i < 20; i++) await f.p._considerDecoderLatency(remote, frame, 200);
    assert.equal(remote.software, false, 'do not oscillate between decoders');
});

for (const reason of ['4k', 'above-1440p', 'brief-stall', 'unsupported']) test(`decoder fallback preserves native decoding for ${reason}`, async () => {
    const f = fixture({ support: config => reason !== 'unsupported' || config.hardwareAcceleration !== 'prefer-software' });
    f.p.addRemote('s', f.canvas, 'h264', f.host);
    const remote = f.p.remotes.get('s');
    const frame = reason === '4k' ? { displayWidth: 2160, displayHeight: 3840 }
        : reason === 'above-1440p' ? { displayWidth: 2560, displayHeight: 1442 }
        : { displayWidth: 1440, displayHeight: 2560 };
    for (let i = 0; i < 24; i++) await f.p._considerDecoderLatency(remote, frame, reason === 'brief-stall' && i % 2 ? 5 : 200);
    assert.equal(remote.decoder.config.hardwareAcceleration, 'prefer-hardware');
});

test('software decoding returns to native when the incoming picture grows above 1440p', async () => {
    const f = fixture(); f.p.addRemote('s', f.canvas, 'h264', f.host);
    const remote = f.p.remotes.get('s');
    for (let i = 0; i < 12; i++)
        await f.p._considerDecoderLatency(remote, { displayWidth: 1440, displayHeight: 2560 }, 200);
    assert.equal(remote.decoder.config.hardwareAcceleration, 'prefer-software');
    await f.p._considerDecoderLatency(remote, { displayWidth: 2160, displayHeight: 3840 }, 5);
    assert.equal(remote.decoder.config.hardwareAcceleration, 'prefer-hardware');
    assert.equal(remote.software, false);
});

test('decoder delay tracking releases timestamps after rendering and stays bounded for stalled decoders', () => {
    const f = fixture(); f.p.addRemote('s', f.canvas, 'h264', f.host);
    const remote = f.p.remotes.get('s');
    for (let i = 0; i < 100; i++) f.p.decodeFrame('s', new Uint8Array([1]), i, i === 0);
    assert.equal(remote.pending.size, 32);
    remote.decoder.callbacks.output({ timestamp: 99, displayWidth: 1280, displayHeight: 720, close() {} });
    assert.equal(remote.pending.has(99), false);
});


test('Safari capture hands frame callbacks to the visible preview without reopening the camera', async () => {
    const f = fixture({ capture: 'rvfc' });
    await init(f);
    await f.p.startCapture(f.host, {});
    const hidden = f.video;
    const stale = [...hidden.callbacks.values()][0];
    const preview = new hidden.constructor(); preview.attached = true;
    f.p.attachPreview(preview);
    assert.equal(hidden.callbacks.size, 0);
    stale(0, {mediaTime: 0});
    assert.equal(f.stats.encoded.length, 0, "cancelled callbacks cannot restart capture on the old node");
    assert.equal(hidden.attached, false);
    assert.equal(f.p.captureVideo, preview);
    preview.emit(1 / 30);
    preview.emit(2 / 30);
    assert.equal(f.stats.encoded.length, 2);
    assert.equal(f.stats.opened.length, 1);
    f.p.stopCapture();
    assert.equal(preview.callbacks.size, 0);
    assert.equal(preview.attached, true, 'the UI owns its preview DOM node');
    assert.equal(preview.srcObject, null);
});


test('removing the preview moves Safari capture off the detached UI node and expanding rebinds it', async () => {
    const f = fixture({ capture: 'rvfc' }); await init(f); await f.p.startCapture(f.host, {});
    const preview = new f.video.constructor(); preview.attached = true;
    f.p.attachPreview(preview); preview.emit(1 / 30);
    f.p.attachPreview(null);
    assert.equal(preview.callbacks.size, 0);
    const temporary = f.p.captureVideo;
    assert.equal(temporary.attached, true);
    temporary.emit(2 / 30);
    f.p.attachPreview(preview); preview.emit(3 / 30);
    assert.equal(temporary.callbacks.size, 0);
    assert.equal(f.stats.encoded.length, 3);
    assert.equal(f.stats.opened.length, 1);
    f.p.stopCapture();
});


test('60fps pacing tolerates alternating early and late camera arrivals without halving cadence', () => {
    const f = fixture(); f.p.config = { framerate: 60 };
    let accepted = 0;
    for (let i = 0; i < 120; i++)
        if (f.p._acceptCaptureTime(Math.round(i * 1e6 / 60 + (i % 2 ? 1200 : -1200)))) accepted++;
    assert.ok(accepted >= 118, `only ${accepted}/120 camera frames accepted`);
});

test('Safari fresh frame callbacks survive a repeated or reset media clock', async () => {
    const f = fixture({ capture: 'rvfc' }); await init(f); await f.p.startCapture(f.host, {});
    for (let i = 0; i < 30; i++) f.p._encodeElementFrame(f.video, { mediaTime: 0 }, i * 1000 / 30);
    assert.equal(f.stats.encoded.length, 30);
    assert.ok(f.stats.encoded.every((x, i, all) => !i || x.frame.timestamp > all[i - 1].frame.timestamp));
    f.p.stopCapture();
});

test('render scales decoded display pixels into the canvas rather than using coded raster size', () => {
    const f = fixture(); const canvas = f.sandbox.document.createElement('canvas');
    f.p.addRemote('s', canvas, 'h264', f.host);
    f.p._render(f.p.remotes.get('s'), {displayWidth: 1440, displayHeight: 1920, close() {}});
    assert.deepEqual(canvas.ops.at(-1).slice(2), [0, 0, 1440, 1920]);
});


test('on-screen diagnostics measure independent capture and encoder rates without altering adaptation', async () => {
    let now = 0;
    const f = fixture({ capture: 'rvfc', clock: () => now });
    await f.p.initEncoder('h264', 1280, 720, 1500, 30, 2);
    await f.p.startCapture(f.host, {});
    assert.equal(f.p.debugSample, null, 'timing stays off until requested');
    assert.equal(f.p.getDiagnostics().captureFps, null, 'first snapshot is not a measured zero');
    for (let i = 0; i < 30; i++) f.video.emit(i / 30);
    now = 1000;
    const before = plain(f.p.stats);
    const info = f.p.getDiagnostics();
    assert.equal(info.captureFps, 30);
    assert.equal(info.encodedFps, 30);
    assert.equal(info.encodedKbps, 30 * 8 * 8 / 1000);
    assert.equal(info.acceleration, 'prefer-hardware');
    assert.equal(info.strategy, 'rvfc');
    assert.deepEqual(plain(f.p.stats), before, 'debug polling does not consume adaptation statistics');
    now = 2000;
    const stalled = f.p.getDiagnostics();
    assert.equal(stalled.captureFps, 0);
    assert.equal(stalled.encodedFps, 0);
    assert.equal(stalled.encodedKbps, 0);
    assert.equal(stalled.encoderDelayMs, null);
    assert.equal(f.p.getDiagnostics(false), null);
    assert.equal(f.p.debugSample, null);
    assert.equal(f.p.encodePending.size, 0);
    now = 10000;
    assert.equal(f.p.getDiagnostics().encodedFps, null, 'reopening starts a fresh measurement');
    await f.p.dispose();
});

test('receive-only diagnostics distinguish received frames, rendered frames and decoder preference', () => {
    let now = 0;
    const f = fixture({ clock: () => now });
    f.p.addRemote('incoming', f.canvas, 'h264', f.host);
    assert.equal(f.p.getDiagnostics().remotes[0].receivedFps, null);
    f.p.decodeFrame('incoming', new Uint8Array(100), 123, true);
    now = 25;
    const remote = f.p.remotes.get('incoming');
    remote.decoder.callbacks.output({ timestamp: 123, displayWidth: 720, displayHeight: 1280, close() {} });
    f.p.decodeFrame('incoming', new Uint8Array(100), 124, false);
    remote.acceleration = 'prefer-software';
    now = 1000;
    const info = f.p.getDiagnostics();
    assert.equal(info.capturing, false);
    assert.equal(info.remotes[0].receivedFps, 2);
    assert.equal(info.remotes[0].renderedFps, 1);
    assert.equal(info.remotes[0].decoderDelayMs, 25);
    assert.equal(info.remotes[0].pendingFrames, 1);
    assert.equal(info.remotes[0].acceleration, 'prefer-software');
    assert.equal(info.remotes[0].width, 720);
    assert.equal(info.remotes[0].height, 1280);
    now = 2000;
    assert.equal(f.p.getDiagnostics().remotes[0].renderedFps, 0);
    f.p.removeRemote('incoming');
    assert.equal(f.p.getDiagnostics().remotes.length, 0);
});

test('diagnostic encoder timing remains bounded when native output stalls', async () => {
    const f = fixture();
    await f.p.initEncoder('h264', 1280, 720, 1500, 30, 2);
    f.p.encoder.encode = () => {};
    f.p.getDiagnostics();
    for (let i = 0; i < 100; i++) f.p._encode({ timestamp: i }, false);
    assert.equal(f.p.encodePending.size, 32);
    f.p.getDiagnostics(false);
    f.p._encode({ timestamp: 101 }, false);
    assert.equal(f.p.encodePending.size, 0);
});

for (const rejected of ['configure', 'error']) test(`hardware decoder ${rejected} rejection recovers with browser default`, () => {
    const f = fixture({ support: (config, kind) => !(rejected === 'configure' && kind === 'decode' && config.hardwareAcceleration === 'prefer-hardware') });
    f.p.addRemote('s', f.canvas, 'h264', f.host);
    const remote = f.p.remotes.get('s');
    if (rejected === 'error') {
        assert.equal(remote.decoder.config.hardwareAcceleration, 'prefer-hardware');
        const old = remote.decoder;
        old.callbacks.error(new Error('hardware unavailable'));
        const current = remote.decoder;
        old.callbacks.error(new Error('stale error'));
        assert.equal(remote.decoder, current);
    }
    assert.equal(remote.decoder.config.hardwareAcceleration, 'no-preference');
    assert.equal(remote.hardwareFailed, true);
    assert.equal(f.p.decodeFrame('s', new Uint8Array([1]), 1, true), true);
});
