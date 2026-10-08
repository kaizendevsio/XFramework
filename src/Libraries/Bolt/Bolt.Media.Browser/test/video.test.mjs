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
    capture = 'processor', frameFromElement = true, clock = () => performance.now(), webgl = null, mediaCapabilities = undefined,
    orientationInit = true, flushError = null } = {}) {
    const listeners = new Map();
    const stats = { encoded: [], decoded: [], stopped: 0, opened: [], invoked: [], closedFrames: 0, frames: [] };

    class Frame {
        constructor(source, init) {
            if (source?.isVideoElement && !frameFromElement) throw new TypeError('unsupported source');
            this.source = source; this.timestamp = init?.timestamp ?? source?.timestamp; this.closed = false;
            // Canvas frames start upright. Pixel-level orientation is also checked in a real browser.
            this.rotation = init?.rotation ?? source?.rotation ?? 0;
            this.flip = init?.flip ?? source?.flip ?? false;
            this.displayWidth = source?.displayWidth ?? source?.width ?? source?.videoWidth ?? 0;
            this.displayHeight = source?.displayHeight ?? source?.height ?? source?.videoHeight ?? 0;
            if (source?.camera || source instanceof Frame) {
                // A frame wrapping a frame composes orientation (WebCodecs "add rotations"), as Chromium does: the
                // rotation adds (subtracts once flipped), the flips cancel, and a quarter turn swaps the displayed sides.
                // orientationInit: false is a browser that predates orientation in VideoFrameInit and ignores it.
                const base = source.rotation ?? 0, baseFlip = source.flip === true;
                const rotation = orientationInit ? init?.rotation ?? 0 : 0, flip = orientationInit && init?.flip === true;
                this.rotation = (((baseFlip ? base - rotation : base + rotation) % 360) + 360) % 360;
                this.flip = baseFlip !== flip;
                if ((this.rotation / 90) % 2 !== (base / 90) % 2)
                    [this.displayWidth, this.displayHeight] = [this.displayHeight, this.displayWidth];
                this.wraps = source;
            }
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
            setTransform: (...m) => canvas.ops.push(['setTransform', ...m]),
            drawImage: (...args) => canvas.ops.push(['drawImage', ...args])
        };
        // No WebGL unless the test describes one: the 2D canvas is then the only painter.
        canvas.getContext = type => type === 'webgl' ? (webgl ? (canvas.gl ??= webgl(canvas)) : null) : canvas.context;
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
        configure(config) { this.state = 'configured'; this.config = config; stats.configured = (stats.configured ?? 0) + 1; }
        encode(frame, options) {
            stats.encoded.push({ frame, options, config: this.config });
            this.callbacks.output({ byteLength: 8, type: options?.keyFrame ? 'key' : 'delta', timestamp: frame.timestamp,
                copyTo(target) { target.fill(1); } }, {});
        }
        async flush() {
            const error = typeof flushError === 'function' ? flushError(this.config) : flushError;
            if (error) throw new Error(error);
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
        setTimeout, clearTimeout,
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
            mediaCapabilities,
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
    // A WebSocket as the page has one, so the transport meter can wrap its send().
    sandbox.WebSocket = class { constructor() { this.readyState = 1; this.bufferedAmount = 0; this.sent = 0; } send() { this.sent++; } };
    sandbox.globalThis = sandbox;
    vm.createContext(sandbox);
    vm.runInContext(source + `
this.meter = socketBufferedAmount;
this.temporalModeFor = temporalModeFor;
this.pipeline = createVideoPipeline();
this.probe = probeVideoCodecs;
this.ceiling = videoDeviceCeiling;
this.capabilities = checkVideoCapabilities;
this.strategyOf = typeof videoCaptureStrategy === 'function' ? videoCaptureStrategy : () => 'absent';
this.codecString = videoCodecString;
this.hevcBitstreamCodec = hevcBitstreamCodec;
this.encoderConfig = encoderConfig;
this.fitTier = fitTierToSource;
this.GlFramePainter = GlFramePainter;
this.orientationMatrix = orientationMatrix;
this.orientedSize = orientedSize;
this.orientationCode = orientationCode;`, sandbox);

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
    assert.deepEqual(plain(by.av1), { codec: 'av1', encode: false, decode: false, hardware: false, maxHeight: 0, decodeMaxHeight: 0, decodeHardware: false });
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
    assert.equal(f.canvases.filter(x => x.ops.length).length, 1, 'reuse the canvas across pictures');
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

// ── Orientation as metadata ──
// When every receiver announced (in its authenticated key envelope) that it reads the orientation from the
// fragment header, the sender stops redrawing: the sensor pixels are encoded as they are, and the receiver turns
// the picture when it paints it.

const sent = f => f.stats.invoked.filter(x => x[0] === 'OnVideoEncoded');
const settle = () => new Promise(resolve => setImmediate(resolve));
/// A portrait phone on MediaStreamTrackProcessor: 1280x720 sensor pixels shown as 720x1280.
const portrait = (f, rotation = 90, timestamp = 0, flip = false) =>
    f.pushFrame({ displayWidth: rotation % 180 ? 720 : 1280, displayHeight: rotation % 180 ? 1280 : 720, rotation, flip, timestamp });

test('with every receiver reading orientation, a rotated Android frame is encoded as the sensor gave it, with no redraw', async () => {
    const f = fixture();
    await init(f);
    f.p.setOrientationMetadata(true);
    await f.p.startCapture(f.host, {});
    const camera = portrait(f, 90);
    await settle();

    assert.ok(f.canvases.every(c => c.ops.length === 0), 'no canvas, no drawImage, no pixel copy');
    const encoded = f.stats.encoded.at(-1).frame;
    assert.equal(encoded.wraps, camera, 'the encoder gets the camera frame itself, its orientation taken off');
    assert.equal(encoded.rotation, 0, 'an encoder must never see an orientation: it would throw on the next one');
    assert.equal(encoded.flip, false);
    assert.deepEqual([encoded.displayWidth, encoded.displayHeight], [1280, 720]);
    assert.deepEqual([f.p.encoder.config.width, f.p.encoder.config.height], [1280, 720], 'fitted to the sensor raster');
    assert.equal(f.stats.encoded.at(-1).options.keyFrame, true);
    assert.equal(sent(f).at(-1)[6], 1, 'a quarter turn clockwise goes with the picture');
    assert.equal(camera.closed, true);
    assert.equal(encoded.closed, true);
});

test('every rotation and flip is carried as its code, and an upright frame as none', async () => {
    const f = fixture();
    await init(f);
    f.p.setOrientationMetadata(true);
    await f.p.startCapture(f.host, {});
    const cases = [[0, false, 0], [90, false, 1], [180, false, 2], [270, false, 3], [0, true, 4], [90, true, 5], [180, true, 6], [270, true, 7]];
    for (const [i, [rotation, flip]] of cases.entries()) { portrait(f, rotation, (i + 1) * 40_000, flip); await settle(); }
    assert.deepEqual(sent(f).map(x => x[6]), cases.map(x => x[2]));
    assert.ok(f.stats.encoded.every(x => x.frame.rotation === 0 && x.frame.flip === false));
    assert.ok(f.canvases.every(c => c.ops.length === 0));
    assert.equal(f.p.encodeOrientation.size, 0, 'each recorded orientation is claimed by its output');
});

test('without that promise from every receiver, the frame is still redrawn upright and sent with no code', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    portrait(f, 90);
    await settle();
    assert.ok(f.canvases.some(c => c.ops.some(op => op[0] === 'drawImage')), 'an older receiver gets upright pixels');
    assert.equal(sent(f).at(-1)[6], 0);
    assert.deepEqual([f.p.encoder.config.width, f.p.encoder.config.height], [720, 1280]);
});

test('an older client joining switches to the redraw on a keyframe, and leaving switches back on one', async () => {
    const f = fixture();
    await init(f);
    f.p.setOrientationMetadata(true);
    await f.p.startCapture(f.host, {});
    portrait(f, 180);
    await settle();
    f.p._takeKeyframe(Date.now());
    portrait(f, 180, 40_000);
    await settle();
    assert.equal(f.stats.encoded.at(-1).options.keyFrame, false);
    // A half turn keeps the shape, so nothing reshapes the encoder: the switch itself must ask for the keyframe.
    f.p.setOrientationMetadata(false);
    portrait(f, 180, 80_000);
    await settle();
    assert.equal(f.stats.encoded.at(-1).options.keyFrame, true, 'turned pixels must not be decoded against unturned references');
    assert.equal(sent(f).at(-1)[6], 0);
    f.p.setOrientationMetadata(true);
    portrait(f, 180, 120_000);
    await settle();
    assert.equal(f.stats.encoded.at(-1).options.keyFrame, true);
    assert.equal(sent(f).at(-1)[6], 2);
});

test('a browser that cannot take the orientation off a frame falls back to the redraw, and stops trying', async () => {
    const f = fixture({ orientationInit: false });
    await init(f);
    f.p.setOrientationMetadata(true);
    await f.p.startCapture(f.host, {});
    portrait(f, 90);
    await settle();
    assert.equal(f.p.orientationUnwrap, false);
    assert.equal(sent(f).at(-1)[6], 0, 'the redrawn picture is upright: no code');
    assert.ok(f.canvases.some(c => c.ops.some(op => op[0] === 'drawImage')));
    const wrapped = f.stats.frames.filter(x => x.wraps).length;
    portrait(f, 90, 40_000);
    await settle();
    assert.equal(f.stats.frames.filter(x => x.wraps).length, wrapped, 'no second attempt per frame');
    assert.ok(f.stats.frames.every(x => x.closed), 'the abandoned wrapper and every camera frame are closed');
});

test('turning the phone mid-call changes only the code: no reconfigure and no keyframe', async () => {
    const f = fixture();
    await init(f);
    f.p.setOrientationMetadata(true);
    await f.p.startCapture(f.host, {});
    portrait(f, 90);
    await settle();
    const configured = f.stats.configured;
    for (const [i, rotation] of [0, 270, 180, 90].entries()) { portrait(f, rotation, (i + 1) * 40_000); await settle(); }
    assert.equal(f.stats.configured, configured, 'the sensor raster never changed');
    assert.deepEqual(f.stats.encoded.slice(1).map(x => x.options.keyFrame), [false, false, false, false]);
    assert.deepEqual(sent(f).map(x => x[6]), [1, 0, 3, 2, 1]);
    assert.deepEqual([f.p.encoder.config.width, f.p.encoder.config.height], [1280, 720]);
});

test('with the redraw, turning the phone over reshapes on a keyframe, and a half turn now forces one too', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    portrait(f, 90);
    await settle();
    f.p._takeKeyframe(Date.now());
    portrait(f, 0, 40_000);
    await settle();
    assert.equal(f.stats.encoded.at(-1).options.keyFrame, true, 'a new raster');
    assert.equal(f.p.encoder.config.width, 1280);
    f.p._takeKeyframe(Date.now());
    portrait(f, 180, 80_000);
    await settle();
    assert.equal(f.stats.encoded.at(-1).options.keyFrame, true, 'same raster, upside-down pixels: references no longer fit');
});

test('tier fitting on the sensor raster gives the same picture, turned, as fitting the upright one', async () => {
    const f = fixture();
    const { fitTier, orientedSize } = f.sandbox;
    for (const [tierWidth, tierHeight] of [[3840, 2160], [1920, 1080], [1280, 720], [960, 540], [640, 360], [426, 240]])
        for (const [sensorWidth, sensorHeight] of [[1280, 720], [1920, 1080], [1440, 1080], [640, 480]]) {
            const sensor = plain(fitTier(tierWidth, tierHeight, sensorWidth, sensorHeight));
            const upright = plain(fitTier(tierWidth, tierHeight, sensorHeight, sensorWidth));
            assert.deepEqual(plain(orientedSize(sensor.width, sensor.height, 1)), upright, `${tierWidth}x${tierHeight} on ${sensorWidth}x${sensorHeight}`);
        }
    await init(f);
    f.p.setOrientationMetadata(true);
    await f.p.startCapture(f.host, {});
    portrait(f, 90);
    await settle();
    assert.equal(await f.p.applyTier(640, 360, 400, 20), true);
    assert.deepEqual([f.p.encoder.config.width, f.p.encoder.config.height], [640, 360], 'shown 360x640 portrait');
    assert.equal(f.p.encoder.config.codec, f.sandbox.codecString('h264', 360, 20), 'the level follows the short edge either way');
});

test('the receiver paints each code at the displayed size, with one transformed drawImage', () => {
    const f = fixture();
    const canvas = f.sandbox.document.createElement('canvas');
    f.p.addRemote('s', canvas, 'h264', f.host);
    const remote = f.p.remotes.get('s');
    for (let code = 0; code < 8; code++) {
        const timestamp = 1000 * (code + 1);
        assert.equal(f.p.decodeFrame('s', new Uint8Array([1]), timestamp, true, false, code), true);
        canvas.ops.length = 0;
        remote.decoder.callbacks.output({ timestamp, displayWidth: 1280, displayHeight: 720, close() {} });
        const [width, height] = code & 1 ? [720, 1280] : [1280, 720];
        assert.deepEqual([canvas.width, canvas.height], [width, height], `code ${code}`);
        assert.deepEqual([remote.width, remote.height], [width, height]);
        const draws = canvas.ops.filter(op => op[0] === 'drawImage');
        assert.equal(draws.length, 1);
        assert.deepEqual(draws[0].slice(2), [0, 0, 1280, 720], 'the decoded picture is drawn at its own size');
        const transforms = canvas.ops.filter(op => op[0] === 'setTransform');
        if (code === 0) { assert.equal(transforms.length, 0, 'an upright picture is painted exactly as before'); continue; }
        assert.deepEqual(transforms[0].slice(1), plain(f.sandbox.orientationMatrix(1280, 720, code)));
        assert.deepEqual(transforms.at(-1).slice(1), [1, 0, 0, 1, 0, 0], 'the context is left as it was found');
    }
});

test('the transform maps the picture onto the canvas exactly, as rotation then flip', () => {
    const { orientationMatrix, orientedSize } = fixture().sandbox;
    const apply = ([a, b, c, d, e, f], x, y) => [a * x + c * y + e, b * x + d * y + f];
    const w = 4, h = 2;
    // Where the picture's top-left corner lands, per code (clockwise quarter turns, then a horizontal flip).
    const topLeft = { 0: [0, 0], 1: [2, 0], 2: [4, 2], 3: [0, 4], 4: [4, 0], 5: [0, 0], 6: [0, 2], 7: [2, 4] };
    for (let code = 0; code < 8; code++) {
        const m = orientationMatrix(w, h, code);
        const size = orientedSize(w, h, code);
        assert.deepEqual(apply(m, 0, 0), topLeft[code], `code ${code}`);
        const corners = [[0, 0], [w, 0], [0, h], [w, h]].map(([x, y]) => apply(m, x, y));
        assert.ok(corners.every(([x, y]) => x >= 0 && y >= 0 && x <= size.width && y <= size.height), 'nothing falls off the canvas');
        assert.equal(new Set(corners.map(String)).size, 4);
    }
});

test('a decoded picture is turned by the code it was sent with, not by the latest one', () => {
    const f = fixture();
    const canvas = f.sandbox.document.createElement('canvas');
    f.p.addRemote('s', canvas, 'h264', f.host);
    const remote = f.p.remotes.get('s');
    f.p.decodeFrame('s', new Uint8Array([1]), 100, true, false, 1);
    f.p.decodeFrame('s', new Uint8Array([1]), 200, false, false, 0);
    remote.decoder.callbacks.output({ timestamp: 100, displayWidth: 1280, displayHeight: 720, close() {} });
    assert.deepEqual([canvas.width, canvas.height], [720, 1280]);
    remote.decoder.callbacks.output({ timestamp: 200, displayWidth: 1280, displayHeight: 720, close() {} });
    assert.deepEqual([canvas.width, canvas.height], [1280, 720], 'the phone was turned back between the two');
    for (let i = 0; i < 100; i++) f.p.decodeFrame('s', new Uint8Array([1]), 1000 + i, false, false, 3);
    assert.equal(remote.orientations.size, 32, 'bounded while the decoder holds pictures back');
});

test('diagnostics say how the sender handles orientation and what the receiver applied', async () => {
    const metadata = fixture();
    await init(metadata);
    metadata.p.setOrientationMetadata(true);
    await metadata.p.startCapture(metadata.host, {});
    assert.equal(metadata.p.getDiagnostics().orientation, 'none');
    portrait(metadata, 90);
    await settle();
    assert.equal(metadata.p.getDiagnostics().orientation, 'sent as metadata (90°)');
    portrait(metadata, 270, 40_000, true);
    await settle();
    assert.equal(metadata.p.getDiagnostics().orientation, 'sent as metadata (270°, mirrored)');

    const redraw = fixture();
    await init(redraw);
    await redraw.p.startCapture(redraw.host, {});
    portrait(redraw, 90);
    await settle();
    assert.equal(redraw.p.getDiagnostics().orientation, 'redrawn upright (2d canvas; 90°)');

    const safari = fixture({ capture: 'rvfc' });
    await init(safari);
    await safari.p.startCapture(safari.host, {});
    assert.equal(safari.p.getDiagnostics().orientation, 'painted upright by the element (2d canvas)');

    const canvas = metadata.sandbox.document.createElement('canvas');
    metadata.p.addRemote('s', canvas, 'h264', metadata.host);
    assert.equal(metadata.p.getDiagnostics().remotes[0].rotation, 'none');
    metadata.p.decodeFrame('s', new Uint8Array([1]), 5, true, false, 1);
    metadata.p.remotes.get('s').decoder.callbacks.output({ timestamp: 5, displayWidth: 1280, displayHeight: 720, close() {} });
    const remote = metadata.p.getDiagnostics().remotes[0];
    assert.equal(remote.rotation, '90°');
    assert.deepEqual([remote.width, remote.height], [720, 1280], 'the incoming size is the displayed one');
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
    assert.equal(info.strategy, 'rvfc/2d', 'the frame-callback path names the painter it used');
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

test('keyframes are sent on demand, coalesced to one a second, with a long safety interval', async () => {
    const f = fixture();
    await f.p.initEncoder('h264', tier.width, tier.height, tier.bitrate, tier.framerate, 10);
    const p = f.p;
    assert.equal(p._takeKeyframe(1_000), true, 'a fresh encoder starts on a keyframe');
    assert.equal(p._takeKeyframe(1_033), false);
    p.requestKeyframe(); p.requestKeyframe(); p.requestKeyframe();
    assert.equal(p._takeKeyframe(1_066), false, 'a request right after a keyframe waits instead of bursting again');
    assert.equal(p._takeKeyframe(2_000), true, 'three requests are served by one keyframe');
    assert.equal(p._takeKeyframe(2_033), false);
    assert.equal(p._takeKeyframe(11_999), false, 'no unrequested keyframe before the safety interval');
    assert.equal(p._takeKeyframe(12_000), true);
    await p.applyTier(640, 360, 400, 20);
    assert.equal(p._takeKeyframe(12_010), true, 'a reconfigured encoder cannot wait for the rate limit');
});

// ── Phase 1: temporal layers, bitrate-only changes, forced keyframes, the transport meter ──

test('temporal layers are asked for where the encoder accepts them, by frame rate', async () => {
    const f = fixture();
    await f.p.initEncoder('vp9', 1280, 720, 1500, 30, 10, true);
    assert.equal(f.p.config.scalabilityMode, 'L1T3', 'L1T3 at 30 fps: the relay can shed half the pictures');
    await f.p.applyTier(640, 360, 400, 15);
    assert.equal(f.p.encoder.config.scalabilityMode, 'L1T2', 'L1T2 at 15 fps keeps a 7.5 fps base layer');
    await f.p.applyTier(320, 180, 100, 10);
    assert.equal(f.p.encoder.config.scalabilityMode, 'L1T1', 'no layers below 12 fps');
    assert.equal(f.sandbox.temporalModeFor(30, new Set(['L1T2'])), 'L1T2', 'L1T2 where L1T3 is refused');
});

test('an encoder that refuses scalability modes (WebKit) keeps plain L1T1 and never claims a layer', async () => {
    const f = fixture({ support: config => !config.scalabilityMode || config.scalabilityMode === 'L1T1' });
    await f.p.initEncoder('h264', 1280, 720, 1500, 30, 10, true);
    assert.equal(f.p.config.scalabilityMode, 'L1T1');
    await f.p.startCapture(f.host, {});
    f.p._onEncoded({ byteLength: 8, type: 'delta', timestamp: 1, copyTo(t) { t.fill(1); } }, { svc: { temporalLayerId: 2 } });
    const sent = f.stats.invoked.filter(x => x[0] === 'OnVideoEncoded').at(-1);
    assert.equal(sent[5], 0, 'without layers configured, metadata is ignored');
});

test('temporal layers off never probes them', async () => {
    let probed = 0;
    const f = fixture({ support: config => { if (config.scalabilityMode !== 'L1T1') probed++; return true; } });
    await f.p.initEncoder('av1', 1280, 720, 1500, 30, 10, false);
    assert.equal(f.p.config.scalabilityMode, 'L1T1');
    assert.equal(probed, 0);
});

test('layer ids come from the encoder metadata, and a keyframe is always the base layer', async () => {
    const f = fixture();
    await f.p.initEncoder('vp9', 1280, 720, 1500, 30, 10, true);
    await f.p.startCapture(f.host, {});
    const chunk = type => ({ byteLength: 8, type, timestamp: 1, copyTo(t) { t.fill(1); } });
    f.p._onEncoded(chunk('key'), { svc: { temporalLayerId: 2 } });
    f.p._onEncoded(chunk('delta'), { svc: { temporalLayerId: 2 } });
    f.p._onEncoded(chunk('delta'), { svc: { temporalLayerId: 1 } });
    f.p._onEncoded(chunk('delta'), {});
    const layers = f.stats.invoked.filter(x => x[0] === 'OnVideoEncoded').map(x => x[5]);
    assert.deepEqual(layers, [0, 2, 1, 0], 'no metadata is the safe answer: base layer, never dropped as enhancement');
});

test('a bitrate change reconfigures the encoder without a keyframe and without touching the camera', async () => {
    const f = fixture();
    await init(f);
    await f.p.startCapture(f.host, {});
    let constraints = 0;
    f.track.applyConstraints = async () => { constraints++; };
    f.p._takeKeyframe(1_000);
    assert.equal(await f.p.applyTier(1280, 720, 1100, 30), true);
    assert.equal(f.p.encoder.config.bitrate, 1_100_000);
    assert.equal(f.p.forceKeyframe, false, 'the references still fit: a keyframe would only be a burst');
    assert.equal(constraints, 0, 'the camera is not renegotiated for a bitrate');
    assert.equal(await f.p.applyTier(640, 360, 400, 20), true);
    assert.equal(f.p.forceKeyframe, true);
    assert.equal(constraints, 1);
});

test('a forced keyframe request skips the one-a-second coalescing', async () => {
    const f = fixture();
    await f.p.initEncoder('h264', tier.width, tier.height, tier.bitrate, tier.framerate, 10);
    const p = f.p;
    assert.equal(p._takeKeyframe(1_000), true);
    p.requestKeyframe();
    assert.equal(p._takeKeyframe(1_100), false);
    p.requestKeyframe(true);
    assert.equal(p._takeKeyframe(1_150), true, 'this sender dropped a base picture: every receiver is stalled');
});

test('the transport meter sums what open sockets still buffer and forgets closed ones', () => {
    const f = fixture();
    const WebSocket = f.sandbox.WebSocket;
    const a = new WebSocket(), b = new WebSocket(), idle = new WebSocket();
    idle.bufferedAmount = 5_000; // never sent through: not counted
    a.send('x'); b.send('y');
    assert.equal(a.sent, 1, 'the wrapped send still sends');
    a.bufferedAmount = 1_000; b.bufferedAmount = 500;
    assert.equal(f.sandbox.meter(), 1_500);
    b.readyState = 3;
    assert.equal(f.sandbox.meter(), 1_000);
});

// ── Safari capture without a CPU readback ──

/// A WebKit page: the only engine whose 2D canvas reads back to the CPU to become a VideoFrame.
const apple = options => { const f = fixture(options); f.sandbox.navigator.vendor = 'Apple Computer, Inc.'; return f; };

/// A WebGL context that records what was asked of it. `pixels` is what the 2x2 centre check reads back; `fail`
/// makes getError report an error after a draw.
function fakeGl({ pixels = 7, fail = false, lost = false } = {}) {
    return canvas => {
        const gl = { calls: [], uploads: [], draws: 0, lost, released: false,
            VERTEX_SHADER: 1, FRAGMENT_SHADER: 2, COMPILE_STATUS: 3, LINK_STATUS: 4, ARRAY_BUFFER: 5, STATIC_DRAW: 6, FLOAT: 7,
            TEXTURE_2D: 8, TEXTURE_MIN_FILTER: 9, TEXTURE_MAG_FILTER: 10, TEXTURE_WRAP_S: 11, TEXTURE_WRAP_T: 12, LINEAR: 13,
            CLAMP_TO_EDGE: 14, UNPACK_FLIP_Y_WEBGL: 15, UNPACK_PREMULTIPLY_ALPHA_WEBGL: 16, RGBA: 17, UNSIGNED_BYTE: 18,
            TRIANGLE_STRIP: 19, NO_ERROR: 0,
            createShader: () => ({}), shaderSource() {}, compileShader() {}, getShaderParameter: () => true,
            createProgram: () => ({}), attachShader() {}, linkProgram() {}, getProgramParameter: () => true, useProgram() {},
            createBuffer: () => ({}), bindBuffer() {}, bufferData() {}, getAttribLocation: () => 0, enableVertexAttribArray() {},
            vertexAttribPointer() {}, createTexture: () => ({}), bindTexture() {}, texParameteri() {},
            pixelStorei(name, value) { gl.calls.push(['pixelStorei', name, value]); },
            viewport(x, y, w, h) { gl.calls.push(['viewport', w, h]); },
            texImage2D(...args) { gl.uploads.push(args); },
            drawArrays() { gl.draws++; },
            getError: () => fail ? 1282 : 0,
            readPixels(x, y, w, h, format, type, target) { target.fill(pixels); },
            isContextLost: () => gl.lost,
            getExtension: name => name === 'WEBGL_lose_context' ? { loseContext() { gl.released = true; } } : null };
        gl.canvas = canvas;
        return gl;
    };
}

test('on the frame-callback path the camera is painted on the GPU, upright, at the encoder size', async () => {
    const f = apple({ capture: 'rvfc', webgl: fakeGl() });
    await init(f);
    await f.p.startCapture(f.host, {});
    for (let i = 0; i < 4; i++) f.video.emit(i / 30);
    const gl = f.p.glPainter.gl;
    assert.equal(f.stats.encoded.length, 4);
    assert.ok(f.stats.frames.every(x => x.source === gl.canvas && x.closed), 'frames come from the WebGL canvas and are closed');
    assert.equal(gl.uploads.length, 4, 'one texture upload a picture');
    assert.ok(gl.uploads.every(args => args.length === 6 && args[1] === 0 && args[2] === gl.RGBA && args[4] === gl.UNSIGNED_BYTE && args[5] === f.video),
        "only the level-0 RGBA/UNSIGNED_BYTE upload of the element itself takes WebKit's GPU-to-GPU path");
    assert.deepEqual(plain(gl.calls.find(x => x[0] === 'viewport')), ['viewport', 1280, 720]);
    assert.deepEqual([gl.canvas.width, gl.canvas.height], [1280, 720]);
    assert.ok(gl.calls.some(x => x[0] === 'pixelStorei' && x[1] === gl.UNPACK_FLIP_Y_WEBGL && x[2] === false),
        'WebKit undoes the camera orientation in the upload; flipping again would turn the picture over');
    assert.equal(f.canvases.filter(x => x.ops.length).length, 0, 'the 2D canvas, and its CPU readback, is never used');
});

test('an engine whose WebGL upload comes back empty falls back to the 2D canvas instead of sending black', async () => {
    const f = apple({ capture: 'rvfc', webgl: fakeGl({ pixels: 0 }) });
    await init(f);
    await f.p.startCapture(f.host, {});
    f.video.emit(0); f.video.emit(1 / 30);
    assert.equal(f.p.glPainter, false);
    assert.equal(f.stats.encoded.length, 2, 'no picture is lost to the switch');
    assert.ok(f.stats.frames.every(x => x.source === f.p.captureCanvas));
    assert.equal(f.canvases.find(x => x.gl)?.gl.released, true, 'the abandoned context is released');
});

test('a WebGL error or a lost context also falls back, and the next capture tries WebGL again', async () => {
    for (const options of [{ fail: true }, { lost: true }]) {
        const f = apple({ capture: 'rvfc', webgl: fakeGl(options) });
        await init(f);
        await f.p.startCapture(f.host, {});
        f.video.emit(0);
        assert.equal(f.p.glPainter, false);
        assert.equal(f.stats.encoded.length, 1);
        f.p.stopCapture();
        assert.equal(f.p.glPainter, null, 'a lost context is not a verdict on this device');
    }
});

test('the painter checks its first picture once, not every picture', () => {
    const gl = fakeGl()({ width: 0, height: 0 });
    let reads = 0;
    gl.readPixels = (x, y, w, h, format, type, target) => { reads++; target.fill(9); };
    const canvas = { width: 0, height: 0, getContext: () => gl };
    const sandbox = fixture().sandbox;
    const painter = sandbox.GlFramePainter.create({ createElement: () => canvas });
    for (let i = 0; i < 30; i++) painter.paint({}, 720, 1280);
    assert.equal(reads, 1);
    assert.deepEqual([canvas.width, canvas.height], [720, 1280]);
    painter.release();
    assert.equal(gl.released, true);
});

// ── Which encoders are hardware: Media Capabilities, since WebCodecs cannot say ──

/// Safari's answers (LibWebRTCProvider::videoEncodingCapabilitiesOverride): H.264 power efficient, VP8/VP9/AV1
/// encode not; decode as the device found hardware.
const safariCapabilities = (vp9HardwareDecode = true) => ({
    queries: [],
    async encodingInfo(query) { this.queries.push(['encode', query]); return { supported: true, smooth: query.video.contentType === 'video/H264', powerEfficient: query.video.contentType === 'video/H264' }; },
    async decodingInfo(query) { this.queries.push(['decode', query]); const hw = query.video.contentType === 'video/H264' || (vp9HardwareDecode && query.video.contentType === 'video/VP9');
        return { supported: true, smooth: hw, powerEfficient: hw }; }
});

test('hardware is what Media Capabilities says is power efficient, so software VP9 on an iPhone is known as software', async () => {
    const mediaCapabilities = safariCapabilities(false);
    const f = fixture({ mediaCapabilities, support: config => !config.codec.startsWith('av01') });
    const by = Object.fromEntries((await f.sandbox.probe(2160)).map(x => [x.codec, x]));
    assert.equal(by.h264.hardware, true, 'VideoToolbox');
    assert.equal(by.vp9.hardware, false, 'libvpx in the web process');
    assert.equal(by.h264.decodeHardware, true);
    assert.equal(by.vp9.decodeHardware, false, 'this iPhone has no VP9 decoder');
    const query = mediaCapabilities.queries[0][1];
    assert.equal(query.type, 'webrtc');
    assert.deepEqual(Object.keys(query.video).sort(), ['bitrate', 'contentType', 'framerate', 'height', 'width'], 'every field WebKit requires');
    assert.ok(!mediaCapabilities.queries.some(([kind, q]) => kind === 'encode' && q.video.contentType === 'video/AV1'), 'nothing is asked about an encoder that does not exist');
});

test('no Media Capabilities, a refusal or a throw all mean "not known to be hardware"', async () => {
    assert.equal((await fixture().sandbox.probe(1080)).some(x => x.hardware || x.decodeHardware), false);
    const refusing = { async encodingInfo() { return { supported: false, powerEfficient: true }; }, async decodingInfo() { throw new TypeError('bad'); } };
    assert.equal((await fixture({ mediaCapabilities: refusing }).sandbox.probe(1080)).some(x => x.hardware || x.decodeHardware), false);
});

test('other engines keep the 2D canvas, which is GPU-backed there and cheaper than a WebGL round trip', async () => {
    const f = fixture({ capture: 'rvfc', webgl: fakeGl() });
    f.sandbox.navigator.vendor = 'Google Inc.';
    await init(f);
    await f.p.startCapture(f.host, {});
    f.video.emit(0);
    assert.ok(!f.p.glPainter);
    assert.ok(f.stats.frames.every(x => x.source === f.p.captureCanvas));
    assert.equal(f.p.getDiagnostics().strategy, 'rvfc/2d');
});

test('diagnostics say which painter an iPhone is using', async () => {
    const f = apple({ capture: 'rvfc', webgl: fakeGl() });
    await init(f);
    await f.p.startCapture(f.host, {});
    f.video.emit(0);
    assert.equal(f.p.getDiagnostics().strategy, 'rvfc/webgl');
});


test('HEVC uses Annex B and probes its actual size for power efficiency', async () => {
    const queries = [];
    const mc = Object.fromEntries(['encodingInfo', 'decodingInfo'].map(method => [method, async query => {
        queries.push(query); return { supported: true, powerEfficient: query.video.contentType === 'video/H265' };
    }]));
    const f = fixture({ mediaCapabilities: mc });
    const hevc = (await f.sandbox.probe(1440)).find(x => x.codec === 'hevc');
    assert.equal(hevc.hardware, true); assert.equal(hevc.decodeHardware, true);
    assert.equal(hevc.maxHeight, 1440);
    const config = f.sandbox.encoderConfig('hevc', 2561, 1441, 5000, 60, 'prefer-hardware');
    assert.equal(config.hevc.format, 'annexb');
    assert.equal(config.width, 2560); assert.equal(config.height, 1440);
    assert.equal(config.codec, 'hvc1.1.6.L156.B0'); // An over-1440 short edge requires the next level.
    assert.ok(queries.filter(q => q.video.contentType === 'video/H265').every(q => q.video.height === 1440));
});

test('HEVC SPS updates decoder profile and level without enabling software latency fallback', async () => {
    const f = fixture();
    const canvas = f.sandbox.document.createElement('canvas');
    await f.p.addRemote('hevc-peer', canvas, 'hevc');
    // Main profile, compatibility flags1/2, progressive/nonpacked/frameonly; escaped zero runs.
    const sps = new Uint8Array([0,0,0,1,66,1,1,1,96,0,0,3,0,176,0,0,3,0,0,3,0,153]);
    assert.equal(f.sandbox.hevcBitstreamCodec(sps), 'hvc1.1.6.L153.B0');
    assert.equal(f.p.decodeFrame('hevc-peer', sps, 0, true), true);
    const remote = f.p.remotes.get('hevc-peer');
    assert.equal(remote.decoder.config.codec, 'hvc1.1.6.L153.B0');
    assert.equal(remote.decoder.config.hardwareAcceleration, 'prefer-hardware');
    for (let i = 0; i < 20; i++) await f.p._considerDecoderLatency(remote, { displayWidth: 1920, displayHeight: 1080 }, 150);
    assert.equal(remote.software, false);
    assert.equal(f.sandbox.hevcBitstreamCodec(sps.slice(0, 12)), null);
    assert.equal(f.sandbox.hevcBitstreamCodec(new Uint8Array([0,0,1,66,1,1,1,0,0,1,66,1])), null);
});


test('HEVC validates a real first output before publishing despite a positive capability probe', async () => {
    const failing = fixture({ flushError: 'Encoder creation error' });
    await assert.rejects(failing.p.initEncoder('hevc', 2560, 1440, 5000, 60, 10), /Encoder creation error/);
    assert.equal(failing.p.encoder, null);
    assert.equal(failing.stats.opened.length, 0, 'no camera permission or stream before native refusal');
    const working = fixture();
    await working.p.initEncoder('hevc', 2560, 1440, 5000, 30, 10);
    assert.equal(working.p.encoder.state, 'configured');
    assert.equal(working.stats.invoked.length, 0, 'synthetic verification frame is never sent');
    assert.equal(working.p.encodeCount, 0, 'verification is not counted as call throughput');
});

test('HEVC refuses a live 60 fps upgrade before changing the working encoder and retries the same size at 30', async () => {
    const f = fixture({ flushError: config => config.framerate > 30 ? 'Encoder creation error' : null });
    await f.p.initEncoder('hevc', 1280, 720, 1500, 30, 10);
    await f.p.startCapture(f.host, {});
    const encoder = f.p.encoder, config = f.p.config, before = f.p.tier;
    await assert.rejects(f.p.applyTier(2560, 1440, 7500, 60), /Encoder creation error/);
    assert.equal(f.p.encoder, encoder);
    assert.equal(f.p.config, config);
    assert.equal(f.p.tier, before);
    assert.equal(encoder.state, 'configured');
    assert.equal(f.p.captureRunning, true);
    assert.equal(f.stats.stopped, 0, 'a refused upgrade does not release the working camera');
    assert.equal(await f.p.applyTier(2560, 1440, 7500, 30), true);
    assert.equal(f.p.config.height, 1440);
    assert.equal(f.p.config.framerate, 30);
    assert.equal(f.p.encoder, encoder, 'reconfigure the existing encoder after successful preflight');
    assert.equal(f.p.forceKeyframe, true);
    f.p.stopCapture();
});
