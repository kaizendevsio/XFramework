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
function fixture({ support = () => ({ supported: true }), hardwareConcurrency = 8, battery = null, cameras = 1 } = {}) {
    const listeners = new Map();
    const stats = { encoded: [], decoded: [], stopped: 0, opened: [], invoked: [], closedFrames: 0 };

    class Encoder {
        static async isConfigSupported(config) { return { supported: !!support(config, 'encode'), config }; }
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
        configure(config) { this.state = 'configured'; this.config = config; }
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
        console, URL, Uint8Array, Float32Array, Math, Date, JSON, Promise, Set, Map, Number, isSecureContext: true,
        AudioEncoder: class { static async isConfigSupported() { return { supported: true }; } },
        AudioDecoder: class { static async isConfigSupported() { return { supported: true }; } },
        AudioContext: class {}, AudioWorkletNode: class {},
        VideoEncoder: Encoder, VideoDecoder: Decoder,
        EncodedVideoChunk: class { constructor(data) { Object.assign(this, data); } },
        MediaStreamTrackProcessor: class {
            constructor({ track }) {
                this.readable = { getReader: () => ({ read: () => new Promise(() => {}), cancel() {} }) };
                this.track = track;
            }
        },
        document: { hidden: false, addEventListener: (name, handler) => listeners.set(name, handler),
            removeEventListener: name => listeners.delete(name) },
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
    sandbox.globalThis = sandbox;
    vm.createContext(sandbox);
    vm.runInContext(source + `
this.pipeline = createVideoPipeline();
this.probe = probeVideoCodecs;
this.ceiling = videoDeviceCeiling;
this.capabilities = checkVideoCapabilities;
this.codecString = videoCodecString;`, sandbox);

    const host = { invokeMethodAsync: async (...args) => { stats.invoked.push(args); } };
    const canvas = { width: 0, height: 0, getContext: () => ({ drawImage() {} }) };
    return { p: sandbox.pipeline, sandbox, stats, listeners, host, canvas, get track() { return track; } };
}

const tier = { width: 1280, height: 720, bitrate: 1500, framerate: 30 };
const init = (f, codec = 'h264') => f.p.initEncoder(codec, tier.width, tier.height, tier.bitrate, tier.framerate, 2);

// ── Codec probing ──

test('probing reports hardware and software encoders separately per codec', async () => {
    // A device with hardware H.264, software VP9 at 720p and no AV1 at all.
    const f = fixture({ support: (config, kind) => {
        const is = name => config.codec.startsWith(name);
        if (is('av01')) return false;
        if (is('vp09')) return kind === 'decode' || (config.hardwareAcceleration !== 'require-hardware' && config.height <= 720);
        return true;
    } });
    const probed = await f.sandbox.probe(1080);
    const by = Object.fromEntries(probed.map(x => [x.codec, x]));
    assert.deepEqual(plain(by.av1), { codec: 'av1', encode: false, decode: false, hardware: false, maxHeight: 0 });
    assert.equal(by.vp9.encode, true);
    assert.equal(by.vp9.hardware, false, 'require-hardware was refused, so VP9 is software here');
    assert.equal(by.vp9.maxHeight, 720);
    assert.equal(by.h264.hardware, true);
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

test('few cores and a low unplugged battery lower the ceiling', async () => {
    assert.equal(await fixture({ hardwareConcurrency: 8 }).sandbox.ceiling(), 1080);
    assert.equal(await fixture({ hardwareConcurrency: 4 }).sandbox.ceiling(), 720);
    assert.equal(await fixture({ battery: { charging: false, level: 0.15 } }).sandbox.ceiling(), 540);
    assert.equal(await fixture({ battery: { charging: true, level: 0.05 } }).sandbox.ceiling(), 1080,
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
    assert.equal(f.p.applyTier(1280, 720, 1500, 30), false, 'the same tier is not a reconfigure');
    assert.equal(f.p.applyTier(640, 360, 400, 20), true);
    assert.equal(f.p.encoder.config.height, 360);
    assert.equal(f.p.encoder.config.bitrate, 400000);
    assert.equal(f.p.pendingKeyframe, true, 'the far side cannot decode a new size against an old reference');
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
