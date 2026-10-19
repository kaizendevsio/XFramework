import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../wwwroot/bolt-video-capture-worker.js', import.meta.url), 'utf8').replace(/^import .*;$/gm, '');
const tick = () => new Promise(resolve => setImmediate(resolve));
function fixture(supported = true, normalizer = true, gpu = undefined) {
    let pending;
    const messages = [], track = { stopped: false, constraints: [], stop() { this.stopped = true; },
        async applyConstraints(value) { this.constraints.push(value); } };
    const reader = { read: () => new Promise(resolve => { pending = resolve; }),
        async cancel() { pending?.({ done: true }); } };
    const self = { postMessage: (data, transfer) => messages.push({ data, transfer }), close() { self.closed = true; } };
    const painted = [], normalized = [];
    const painter = { checked: false, device: { pushErrorScope() {}, async popErrorScope() { return null; } }, paint(frame, width, height) { painted.push({ frame, width, height }); return { width, height }; },
        release() { painter.released = true; } };
    const sandbox = { self, Math, Error, performance,
        GlFramePainter: { create: () => normalizer ? painter : null },
        GpuFramePainter: { create: async () => gpu === undefined ? (normalizer ? painter : null) : gpu },
        OffscreenCanvas: class {},
        VideoFrame: class { constructor(canvas, init) { Object.assign(this, init); this.rotation = 0; this.flip = false;
            this.displayWidth = canvas.width; this.displayHeight = canvas.height; normalized.push(this); }
            close() { this.closed = true; } } };
    if (supported) sandbox.MediaStreamTrackProcessor = class {
        constructor(options) { assert.equal(options.maxBufferSize, 1); this.readable = { getReader: () => reader }; }
    };
    vm.runInNewContext(source, sandbox);
    return { self, track, messages, painter, painted, normalized, send: data => self.onmessage({ data }),
        frame(timestamp = 0, rotation = 0) {
            const value = { displayWidth: 1080, displayHeight: 1920, timestamp, rotation, flip: false, closed: false, close() { this.closed = true; } };
            pending({ value, done: false }); return value;
        }, end() { pending({ done: true }); } };
}

test('worker probes its own context without opening any camera', () => {
    for (const supported of [true, false]) {
        const f = fixture(supported);
        assert.equal(f.messages[0].data.supported, supported);
        assert.equal(f.track.stopped, false);
    }
});

test('worker prefers external GPU textures and reports separate preparation costs', async () => {
    const gpu = { checked: false, device: { pushErrorScope() {}, async popErrorScope() { return null; } },
        paint(frame, width, height) { gpu.size = [width, height]; return { width, height }; }, release() { gpu.released = true; } };
    const f = fixture(true, true, gpu);
    f.send({ type: 'start', track: f.track, width: 720, height: 1280 });
    f.send({ type: 'diagnostics', enabled: true });
    const frame = f.frame(); delete frame.rotation; await tick();
    const sent = f.messages.at(-1).data;
    assert.equal(sent.path, 'worker-webgpu');
    assert.deepEqual(gpu.size, [720, 1280]);
    assert.ok(Number.isFinite(sent.paintMs) && Number.isFinite(sent.frameMs));
    assert.equal(f.painted.length, 0);
    f.send({ type: 'stop' }); await tick(); assert.ok(gpu.released);
});

test('GPU validation failure stops capture, closes frames and never uses WebGL', async () => {
    const gpu = { checked: false, device: { pushErrorScope() {}, async popErrorScope() { return Error('unsupported'); } },
        paint(frame, width, height) { return { width, height }; }, release() { gpu.released = true; } };
    const f = fixture(true, true, gpu); f.send({ type: 'start', track: f.track });
    const frame = f.frame(); delete frame.rotation; await tick();
    assert.equal(f.messages.at(-1).data.reason, 'webgpu-failed');
    assert.equal(f.messages.filter(x => x.data.type === 'frame').length, 0);
    assert.equal(f.painted.length, 0);
    assert.ok(frame.closed && f.normalized[0].closed && gpu.released && f.track.stopped);
});

test('worker bounds in-flight ownership, drops excess and paces before transferring', async () => {
    const f = fixture(); f.send({ type: 'start', track: f.track, framerate: 30 });
    const a = f.frame(); await tick();
    assert.equal(f.messages.at(-1).transfer[0], a);
    assert.ok(a.closed);
    const b = f.frame(40000); await tick(); assert.ok(b.closed);
    assert.equal(f.messages.filter(x => x.data.type === 'frame').length, 1);
    f.send({ type: 'credit', framerate: 30 });
    const c = f.frame(10000); await tick(); assert.ok(c.closed);
    assert.equal(f.messages.filter(x => x.data.type === 'frame').length, 1);
    f.frame(80000); await tick();
    assert.equal(f.messages.filter(x => x.data.type === 'frame').length, 2);
    f.send({ type: 'stop' }); await tick();
    assert.ok(f.track.stopped); assert.ok(f.self.closed);
});

test('worker reports unavailable normalization and releases its camera clone', async () => {
    const f = fixture(true, false); f.send({ type: 'start', track: f.track });
    const a = f.frame(); delete a.rotation; await tick();
    assert.equal(f.messages.at(-1).data.type, 'failed');
    assert.equal(f.messages.at(-1).data.reason, 'webgpu-unavailable');
    assert.ok(a.closed); assert.ok(f.track.stopped); assert.ok(f.self.closed);
});

test('worker releases the track on stream end, transfer failure and explicit stop', async () => {
    for (const reason of ['end', 'transfer', 'stop']) {
        const f = fixture(); f.send({ type: 'start', track: f.track });
        if (reason === 'end') f.end();
        if (reason === 'transfer') {
            f.self.postMessage = data => { if (data.type === 'frame') throw new Error('transfer'); };
            const frame = f.frame(); await tick(); assert.ok(frame.closed);
        }
        if (reason === 'stop') f.send({ type: 'stop' });
        await tick(); assert.ok(f.track.stopped); assert.ok(f.self.closed);
    }
});

test('worker applies adaptation to the transferred clone', async () => {
    const f = fixture(); f.send({ type: 'start', track: f.track });
    const constraints = { width: { ideal: 640 }, frameRate: { max: 20 } };
    f.send({ type: 'constraints', framerate: 20, constraints });
    await tick(); assert.equal(f.track.constraints[0], constraints);
    f.send({ type: 'stop' }); await tick();
});

test('worker deadline pacing retains 60fps with alternating camera jitter', async () => {
    const f = fixture(); f.send({ type: 'start', track: f.track, framerate: 60 });
    for (let i = 0; i < 120; i++) {
        f.send({ type: 'credit', framerate: 60 });
        f.frame(Math.round(i * 1e6 / 60 + (i % 2 ? 1200 : 0)));
        await tick();
    }
    assert.ok(f.messages.filter(x => x.data.type === 'frame').length >= 118);
    f.send({ type: 'stop' }); await tick();
});


test('worker normalizes hidden orientation without rejecting Safari frames or guessing a rotation', async () => {
    const f = fixture(); f.send({ type: 'start', track: f.track });
    const frame = f.frame(); delete frame.rotation; delete frame.flip; await tick();
    const sent = f.messages.at(-1);
    assert.equal(sent.data.path, 'worker-webgpu');
    assert.equal(sent.data.prepareMs, null, 'timing is opt-in');
    assert.equal(sent.transfer[0], f.normalized[0]);
    assert.deepEqual([sent.data.frame.displayWidth, sent.data.frame.displayHeight], [1080, 1920]);
    assert.ok(frame.closed && f.normalized[0].closed);
    assert.equal(f.painted[0].frame, frame);
    assert.equal(f.track.stopped, false);
    f.send({ type: 'diagnostics', enabled: true }); f.send({ type: 'credit' });
    const next = f.frame(40000); delete next.rotation; await tick();
    assert.ok(Number.isFinite(f.messages.at(-1).data.prepareMs));
    f.send({ type: 'stop' }); await tick(); assert.ok(f.painter.released);
});

test('normalization failure after allocation releases both frames and the GPU context', async () => {
    const f = fixture(); f.send({ type: 'start', track: f.track });
    f.self.postMessage = data => { if (data.type === 'frame') throw Error('transfer'); };
    const frame = f.frame(); delete frame.rotation; await tick();
    assert.ok(frame.closed && f.normalized[0].closed && f.painter.released && f.track.stopped);
});

test('device loss after a successful frame stops instead of switching painters', async () => {
    const f = fixture(); f.send({ type: 'start', track: f.track });
    const first = f.frame(); delete first.rotation; await tick();
    f.painter.paint = () => { throw Error('device lost'); };
    f.send({ type: 'credit' }); const next = f.frame(40000); delete next.rotation; await tick();
    assert.equal(f.messages.at(-1).data.reason, 'webgpu-failed');
    assert.equal(f.messages.filter(x => x.data.type === 'frame').length, 1);
    assert.ok(next.closed && f.track.stopped && f.painter.released);
});
