import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../wwwroot/bolt-video-capture-worker.js', import.meta.url), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));
function fixture(supported = true) {
    let pending;
    const messages = [], track = { stopped: false, constraints: [], stop() { this.stopped = true; },
        async applyConstraints(value) { this.constraints.push(value); } };
    const reader = { read: () => new Promise(resolve => { pending = resolve; }),
        async cancel() { pending?.({ done: true }); } };
    const self = { postMessage: (data, transfer) => messages.push({ data, transfer }), close() { self.closed = true; } };
    const sandbox = { self, Math, Error };
    if (supported) sandbox.MediaStreamTrackProcessor = class {
        constructor(options) { assert.equal(options.maxBufferSize, 1); this.readable = { getReader: () => reader }; }
    };
    vm.runInNewContext(source, sandbox);
    return { self, track, messages, send: data => self.onmessage({ data }),
        frame(timestamp = 0, rotation = 0) {
            const value = { timestamp, rotation, flip: false, closed: false, close() { this.closed = true; } };
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

test('worker refuses uninspectable orientation and releases its camera clone', async () => {
    const f = fixture(); f.send({ type: 'start', track: f.track });
    const a = f.frame(); delete a.rotation; await tick();
    assert.equal(f.messages.at(-1).data.type, 'failed');
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
