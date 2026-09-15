import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function fixture() {
    const sources = [], timers = new Map(), handlers = {}, calls = [];
    let sequence = 0;
    class EventSource {
        static CLOSED = 2;
        constructor(url) { this.url = url; this.readyState = 0; sources.push(this); }
        close() { this.readyState = 2; }
        addEventListener() {}
    }
    const document = { hidden: false, addEventListener: (name, fn) => handlers[name] = fn };
    const navigator = { onLine: true }, window = { yap: {} };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/device.js', import.meta.url), 'utf8'), {
        document, navigator, window, yap: window.yap, EventSource, visualViewport: null,
        addEventListener: (name, fn) => handlers[name] = fn,
        requestAnimationFrame() {}, cancelAnimationFrame() {},
        setTimeout(fn, delay) { const id = ++sequence; timers.set(id, { fn, delay }); return id; },
        clearTimeout(id) { timers.delete(id); }
    });
    const api = window.yap.device;
    api.watch({ invokeMethodAsync(...args) { calls.push(args); return Promise.resolve(); } });
    const fail = source => { source.readyState = 2; source.onerror(); };
    const tick = () => { const [id, timer] = timers.entries().next().value; timers.delete(id); timer.fn(); return timer.delay; };
    return { api, sources, timers, handlers, calls, navigator, document, fail, tick };
}

test('closed event streams reconnect, reconcile on open and forward targeted metadata', () => {
    const f = fixture(); f.api.events('account', 'thread');
    f.api.events('account', 'thread'); assert.equal(f.sources.length, 1);
    const first = f.sources[0]; first.onerror(); assert.equal(f.timers.size, 0, 'Native CONNECTING retries remain browser-owned');
    f.fail(first); f.fail(first); assert.equal(f.timers.size, 1);
    assert.equal(f.tick(), 1000); assert.equal(f.sources.length, 2);
    const next = f.sources[1]; next.readyState = 1; next.onopen();
    next.onmessage({ data: '{"Kind":"MessagesRead"}' });
    assert.deepEqual(f.calls, [['RefreshHint'], ['ChatEvent', 'account', '{"Kind":"MessagesRead"}']]);
});

test('repeated failures back off; offline/background clients resume without retry flooding', () => {
    const f = fixture(); f.api.events('account', 'thread'); f.fail(f.sources[0]); f.tick();
    f.fail(f.sources[1]); f.navigator.onLine = false;
    assert.equal(f.tick(), 2000); assert.equal(f.sources.length, 2);
    f.navigator.onLine = true; f.document.hidden = true; f.handlers.online();
    assert.equal(f.sources.length, 2);
    f.document.hidden = false; f.handlers.visibilitychange();
    assert.equal(f.sources.length, 3); assert.equal(f.timers.size, 0);
    const next = f.sources[2]; next.readyState = 1; next.onopen(); f.fail(next);
    assert.equal(f.tick(), 1000, 'Successful connection resets backoff');
    for (let i = 0; i < 8; i++) { f.fail(f.sources.at(-1)); assert.ok(f.tick() <= 30000); }
});

test('logout cancels retries and old account callbacks cannot revive a stream', () => {
    const f = fixture(); f.api.events('first', 'thread'); const old = f.sources[0];
    f.fail(old); f.api.events(''); assert.equal(f.timers.size, 0);
    old.onerror(); old.onopen(); old.onmessage({ data: 'refresh' });
    f.handlers.online(); assert.equal(f.sources.length, 1); assert.equal(f.calls.length, 0);
    f.api.events('second', 'thread'); old.onerror();
    assert.equal(f.sources.length, 2); assert.equal(f.timers.size, 0);
});
