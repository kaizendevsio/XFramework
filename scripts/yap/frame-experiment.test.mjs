import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../../src/Presentation/XFramework.Yap.Client/wwwroot/frame-experiment-worker.js', import.meta.url), 'utf8').replace(/^import .*;$/gm, '');
const tick = () => new Promise(r => setImmediate(r));
function fixture(gpu = true) {
    let pending, probes = 0;
    const messages = [], painted = [], constructed = [];
    const track = { stop() { this.stopped = true; }, getSettings() { return { width: 1080, height: 1920, frameRate: 30 }; } };
    const painter = { paint(...args) { painted.push(args); return {}; }, release() { this.released = true; } };
    const self = { postMessage: (data, transfer) => messages.push({ data, transfer }), close() { this.closed = true; } };
    vm.runInNewContext(source, { self, performance,
        GpuFramePainter: { async create() { probes++; return gpu ? painter : null; } },
        MediaStreamTrackProcessor: class { constructor() { this.readable = { getReader: () => ({ read: () => new Promise(r => pending = r), async cancel() { pending?.({ done: true }); } }) }; } },
        VideoFrame: class { constructor() { constructed.push(this); } close() { this.closed = true; } } });
    return { self, track, messages, painted, constructed, painter, get probes() { return probes; },
        send: data => self.onmessage({ data }), frame(timestamp = 0) {
            const frame = { timestamp, codedWidth: 1920, codedHeight: 1080, displayWidth: 1080, displayHeight: 1920,
                close() { this.closed = true; } }; pending({ value: frame }); return frame;
        } };
}
test('direct hidden-orientation frames transfer unchanged without creating a GPU or canvas frame', async () => {
    const f = fixture(false); f.send({ type: 'start', mode: 'direct', fps: 30, track: f.track });
    const raw = f.frame(); await tick();
    const sent = f.messages.at(-1);
    assert.equal(sent.transfer[0], raw); assert.equal(sent.data.frame, raw);
    assert.equal(sent.data.source.rotation, null); assert.equal(sent.data.source.flip, null);
    assert.equal(f.probes, 0); assert.equal(f.constructed.length, 0); assert.ok(raw.closed);
    const dropped = f.frame(40000); await tick(); assert.ok(dropped.closed);
    assert.equal(f.messages.filter(x => x.data.type === 'frame').length, 1);
    f.send({ type: 'stop' }); await tick(); assert.ok(f.track.stopped && f.self.closed);
});
test('reference path paints at camera dimensions and closes both frames', async () => {
    const f = fixture(); f.send({ type: 'start', mode: 'webgpu', fps: 30, track: f.track }); await tick();
    const raw = f.frame(); await tick();
    assert.deepEqual(f.painted[0].slice(1), [1080,1920]);
    assert.ok(raw.closed && f.constructed[0].closed);
    assert.equal(f.messages.at(-1).transfer[0], f.constructed[0]);
    f.send({ type: 'stop' }); await tick(); assert.ok(f.painter.released && f.track.stopped);
});
test('missing WebGPU is explicit and never retries raw frames', async () => {
    const f = fixture(false); f.send({ type: 'start', mode: 'webgpu', fps: 30, track: f.track }); await tick();
    assert.match(f.messages.at(-1).data.message, /No alternate path/);
    assert.equal(f.messages.filter(x => x.data.type === 'frame').length, 0);
    assert.ok(f.track.stopped && f.self.closed);
});

test('camera permission resolving after stop releases the new camera without starting a worker', async () => {
    const main = readFileSync(new URL('../../src/Presentation/XFramework.Yap.Client/wwwroot/frame-experiment.js', import.meta.url), 'utf8')
        .replace('export async function create', 'async function create').replaceAll('import.meta.url', "'https://test/frame-experiment.js'");
    let permission, workers = 0;
    const camera = { srcObject: null, async play() {} }, track = { stop() { this.stopped = true; } };
    const sandbox = { performance, Map, Math, Number, Error, URL, screen: {}, setTimeout, clearTimeout, setInterval, clearInterval,
        document: { addEventListener() {}, removeEventListener() {} },
        navigator: { mediaDevices: { getUserMedia: () => new Promise(r => permission = r) } },
        VideoEncoder: class {}, VideoDecoder: class {}, Worker: class { constructor() { workers++; } } };
    vm.runInNewContext(main + ';globalThis.create = create;', sandbox);
    const api = await sandbox.create(camera, {}, { async invokeMethodAsync() {} });
    const starting = api.start({ mode: 'direct', height: 1080, fps: 30 }); await tick();
    await api.stop(); permission({ getTracks: () => [track] }); await starting;
    assert.ok(track.stopped); assert.equal(camera.srcObject, null); assert.equal(workers, 0);
    await api.dispose();
});
