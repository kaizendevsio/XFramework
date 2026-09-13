import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../wwwroot/bolt-media.js', import.meta.url), 'utf8')
    .replaceAll('export ', '').replaceAll('import.meta.url', "'https://example.test/bolt-media.js'");
function fixture() {
    const stats = { stopped: 0, encoded: [], decoded: [], closedFrames: 0, played: [] };
    class Encoder {
        static async isConfigSupported() { return { supported: true }; }
        constructor(callbacks) { this.callbacks = callbacks; this.state = 'unconfigured'; this.encodeQueueSize = 0; }
        configure() { this.state = 'configured'; }
        encode(data) { stats.encoded.push(data); }
        close() { this.state = 'closed'; }
    }
    class Decoder extends Encoder {
        constructor(callbacks) { super(callbacks); this.decodeQueueSize = 0; }
        decode(chunk) { stats.decoded.push(chunk); }
    }
    class Context {
        constructor() { this.state = 'running'; this.currentTime = 1; this.audioWorklet = { addModule: async () => {} }; }
        async resume() { this.state = 'running'; }
        async close() { this.state = 'closed'; }
        createMediaStreamSource() { return { connect() {}, disconnect() {} }; }
        createBuffer(channels, frames, rate) { return { duration: frames / rate, copyToChannel() {} }; }
        createBufferSource() {
            const item = { connect() {}, disconnect() {}, start(t) { stats.played.push(t); }, stop() { item.stopped = true; } };
            return item;
        }
    }
    const stream = { getTracks: () => [{ stop() { stats.stopped++; } }] };
    const sandbox = {
        console, URL, Uint8Array, Float32Array, isSecureContext: true, AudioEncoder: Encoder, AudioDecoder: Decoder, AudioContext: Context,
        AudioData: class { constructor(data) { Object.assign(this, data); } close() { stats.closedFrames++; } },
        EncodedAudioChunk: class { constructor(data) { Object.assign(this, data); } },
        AudioWorkletNode: class { constructor() { this.port = { postMessage() {}, close() {} }; } disconnect() {} },
        navigator: { mediaDevices: { getUserMedia: async () => stream } }
    };
    vm.createContext(sandbox);
    vm.runInContext(source + '\nthis.pipeline = createAudioPipeline(); this.check = checkVoiceCapabilities;', sandbox);
    return { p: sandbox.pipeline, sandbox, stats, stream };
}
async function initialized(f) { await f.p.initEncoder(48000, 1, 64); await f.p.initDecoder(48000, 1); }
function audio(stats) { return { numberOfFrames: 960, numberOfChannels: 1, sampleRate: 48000, copyTo() {}, close() { stats.closedFrames++; } }; }

test('capture uses AudioWorklet without audio TrackProcessor', async () => {
    const f = fixture(); await initialized(f); await f.p.startCapture({ invokeMethodAsync: async () => {} });
    f.p.captureNode.port.onmessage({ data: { samples: new Float32Array(960), timestamp: 20000 } });
    assert.equal(f.stats.encoded.length, 1); assert.equal(f.stats.closedFrames, 1);
    await f.p.dispose(); assert.equal(f.stats.stopped, 1);
});
test('capture setup failure releases acquired microphone', async () => {
    const f = fixture(); await initialized(f);
    f.p.audioContext.audioWorklet.addModule = async () => { throw Error('module failed'); };
    await assert.rejects(f.p.startCapture({}), /module failed/);
    assert.equal(f.stats.stopped, 1); assert.equal(f.p.captureRunning, false);
});
test('hangup during microphone permission stops the late stream', async () => {
    const f = fixture(); await initialized(f); let resolve;
    f.sandbox.navigator.mediaDevices.getUserMedia = () => new Promise(r => { resolve = r; });
    const start = f.p.startCapture({}); await new Promise(r => setImmediate(r));
    f.p.stopCapture(); resolve(f.stream); await start;
    assert.equal(f.stats.stopped, 1); assert.equal(f.p.captureRunning, false);
});
test('encoder and JS interop work stay bounded under slow transport', async () => {
    const f = fixture(); await initialized(f); let done;
    await f.p.startCapture({ invokeMethodAsync: () => new Promise(r => { done = r; }) });
    for (let i = 0; i < 50; i++) f.p.encoder.callbacks.output({ byteLength: 1, copyTo(a) { a[0] = i; } });
    assert.equal(f.p.encodedQueue.length, 4); assert.equal(f.p.encodedQueue.at(-1)[0], 49);
    f.p.encoder.encodeQueueSize = 4;
    f.p.captureNode.port.onmessage({ data: { samples: new Float32Array(960), timestamp: 0 } });
    assert.equal(f.stats.encoded.length, 0); f.p.stopCapture(); done();
});
test('wire clock converts to microseconds and decoder queue stays bounded', async () => {
    const f = fixture(); await initialized(f); f.p.decodeFrame(new Uint8Array(1), 960);
    assert.equal(f.stats.decoded[0].timestamp, 20000);
    f.p.decoder.decodeQueueSize = 8; f.p.decodeFrame(new Uint8Array(1), 1920);
    assert.equal(f.stats.decoded.length, 1);
});
test('playback stays bounded and hangup stops scheduled audio', async () => {
    const f = fixture(); await initialized(f);
    for (let i = 0; i < 100; i++) f.p._playAudioData(audio(f.stats));
    assert.ok(f.stats.played.length <= 11); assert.equal(f.stats.closedFrames, 100);
    const sources = [...f.p.sources]; f.p.stopPlayback();
    assert.ok(sources.every(s => s.stopped)); assert.equal(f.p.sources.size, 0);
    f.p._playAudioData(audio(f.stats)); assert.equal(f.stats.closedFrames, 101);
});
test('unsupported formats fail before capture and suspended playback closes frames', async () => {
    const f = fixture(); await assert.rejects(f.p.initEncoder(44100, 2, 64), /48 kHz mono/);
    await initialized(f); f.p.audioContext.state = 'suspended'; f.p._playAudioData(audio(f.stats));
    assert.equal(f.stats.played.length, 0); assert.equal(f.stats.closedFrames, 1);
});
test('worklet emits 20 ms frames and bounds cross-thread backlog', () => {
    let Worklet;
    const output = [];
    const context = { Float32Array, AudioWorkletProcessor: class { constructor() { this.port = { postMessage: data => output.push(data) }; } }, registerProcessor(name, cls) { Worklet = cls; } };
    vm.runInNewContext(readFileSync(new URL('../wwwroot/bolt-audio-capture.js', import.meta.url), 'utf8'), context);
    const worklet = new Worklet();
    for (let i = 0; i < 30; i++) worklet.process([[new Float32Array(128)]]);
    assert.equal(output.length, 2); assert.equal(output[1].timestamp, 20000); assert.equal(output[0].samples.length, 960);
    worklet.port.onmessage(); for (let i = 0; i < 8; i++) worklet.process([[new Float32Array(128)]]);
    assert.equal(output.length, 3);
});
test('capability preflight selects managed fallback without microphone access', async () => {
    const f = fixture(); let requests = 0;
    f.sandbox.navigator.mediaDevices.getUserMedia = () => { requests++; };
    assert.equal((await f.sandbox.check()).supported, true);
    f.sandbox.AudioEncoder = undefined;
    assert.equal((await f.sandbox.check()).supported, true);
    assert.equal((await f.sandbox.check()).nativeCodecs, false);
    assert.equal(requests, 0);
});
test('capability preflight selects fallback for unsupported Opus but rejects insecure contexts', async () => {
    const f = fixture();
    f.sandbox.AudioEncoder.isConfigSupported = async () => ({ supported: false });
    assert.equal((await f.sandbox.check()).nativeCodecs, false);
    f.sandbox.isSecureContext = false;
    assert.match((await f.sandbox.check()).reason, /HTTPS/);
});
test('managed capture sends bounded little endian PCM and plays decoded PCM', async () => {
    const f = fixture(); f.p.initManaged(); let pcm;
    await f.p.startCapture({ invokeMethodAsync: async (method, bytes) => { assert.equal(method, 'OnAudioPcm'); pcm = bytes; } });
    const samples = new Float32Array(960); samples[0] = 0.5;
    f.p.captureNode.port.onmessage({ data: { samples, timestamp: 0 } });
    await new Promise(r => setImmediate(r));
    assert.equal(pcm.length, 1920); assert.equal(new DataView(pcm.buffer).getInt16(0, true), 16384);
    f.p.playPcm(pcm); assert.equal(f.stats.played.length, 1);
    await f.p.dispose(); assert.equal(f.stats.stopped, 1);
});

test('native preparation acquires microphone without advancing encoder until answer', async () => {
    const f = fixture(); await initialized(f);
    const target = { invokeMethodAsync: async () => {} };
    await f.p.startCapture(target, null, false);
    const node = f.p.captureNode;
    for (let i = 0; i < 500; i++)
        node.port.onmessage({ data: { samples: new Float32Array(960), timestamp: i * 20000 } });
    assert.equal(f.p.captureRunning, true);
    assert.equal(f.stats.encoded.length, 0);
    await f.p.startCapture(target, null, true);
    assert.equal(f.p.captureNode, node, 'Answer must reuse the prepared microphone.');
    node.port.onmessage({ data: { samples: new Float32Array(960), timestamp: 10000000 } });
    assert.equal(f.stats.encoded.length, 1);
    await f.p.dispose();
});

test('managed preparation does not invoke codec before answer and pauses PCM under backpressure', async () => {
    const f = fixture(); f.p.initManaged(); let calls = 0, finish;
    const target = { invokeMethodAsync: () => { calls++; return new Promise(resolve => { finish = resolve; }); } };
    await f.p.startCapture(target, null, false);
    const frame = { data: { samples: new Float32Array(960), timestamp: 0 } };
    for (let i = 0; i < 100; i++) f.p.captureNode.port.onmessage(frame);
    assert.equal(calls, 0);
    await f.p.startCapture(target, null, true);
    f.p.captureNode.port.onmessage(frame);
    for (let i = 0; i < 500; i++) f.p.captureNode.port.onmessage(frame);
    assert.equal(calls, 1);
    assert.equal(f.p.encodedQueue.length, 0, 'Do not advance codec history while the prior packet is unsent.');
    f.p.stopCapture(); finish();
});

test('mute stops encoding and resume keeps the same codec history', async () => {
    const f = fixture(); await initialized(f);
    const target = { invokeMethodAsync: async () => {} };
    await f.p.startCapture(target);
    const encoder = f.p.encoder;
    const receive = f.p.captureNode.port.onmessage;
    f.p.stopCapture();
    receive({ data: { samples: new Float32Array(960), timestamp: 20000 } });
    assert.equal(f.stats.encoded.length, 0);
    await f.p.startCapture(target);
    assert.equal(f.p.encoder, encoder);
    f.p.captureNode.port.onmessage({ data: { samples: new Float32Array(960), timestamp: 0 } });
    assert.equal(f.stats.encoded.length, 1);
    await f.p.dispose();
});
