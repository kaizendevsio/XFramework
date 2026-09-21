import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const read = name => readFileSync(new URL(`../../Presentation/XFramework.Yap.Client/wwwroot/${name}`, import.meta.url), 'utf8');
const source = read('updates.js');
function fixture({ waiting = false, installing = false, controlled = true } = {}) {
    const events = () => ({ handlers: {}, addEventListener(name, fn) { (this.handlers[name] ??= []).push(fn); },
        async emit(name) { for (const fn of this.handlers[name] ?? []) await fn(); } });
    let now = 0, checks = 0, registrations = 0, reloads = 0, busy = false, fail = false;
    const worker = { ...events(), state: waiting ? 'installed' : 'installing', messages: [], postMessage(value) { this.messages.push(value); } };
    const registration = { ...events(), waiting: waiting ? worker : null, installing: installing ? worker : null,
        async update() { checks++; if (fail) throw Error('Offline'); } };
    const registered = [];
    const serviceWorker = { ...events(), controller: controlled ? {} : null,
        async register(url, options) { registrations++; registered.push(url); assert.equal(options.updateViaCache, 'none'); return registration; } };
    const label = { textContent: '' }, notice = { hidden: true, querySelector: selector => { assert.equal(selector, ".toast-text"); return label; } };
    const document = { ...events(), hidden: false, getElementById: () => notice, querySelector: () => busy ? {} : null };
    const window = { ...events(), yap: {} }, navigator = { onLine: true, serviceWorker };
    let interval;
    const self = {};
    const context = vm.createContext({ self, window, navigator, document, location: { reload() { reloads++; } },
        Date: { now: () => now }, WeakSet, addEventListener: window.addEventListener.bind(window),
        setInterval(fn, ms) { assert.equal(ms, 60000); interval = fn; } });
    // Every registration site shares one helper, so updates cannot install a different worker than
    // push and the recovery page and leave them replacing each other at the same scope.
    vm.runInContext(read('worker-registration.js'), context);
    vm.runInContext(source, context);
    return { window, document, navigator, registration, worker, notice, label, serviceWorker, registered,
        api: window.yap.updates, tick: () => now += 60001, interval: () => interval(),
        busy: value => busy = value, fail: value => fail = value,
        counts: () => ({ checks, registrations, reloads }) };
}

test('long-lived apps check on foreground, reconnect and timer; duplicate events are throttled', async () => {
    const f = fixture();
    await f.window.emit('load');
    await f.window.emit('pageshow');
    assert.deepEqual(f.counts(), { checks: 1, registrations: 1, reloads: 0 });
    f.tick(); await f.document.emit('visibilitychange');
    f.tick(); await f.window.emit('online');
    f.tick(); await f.interval();
    assert.equal(f.counts().checks, 4);
    f.tick(); f.document.hidden = true; await f.interval();
    f.document.hidden = false; f.navigator.onLine = false; await f.interval();
    assert.equal(f.counts().checks, 4);
    f.navigator.onLine = true; f.fail(true); await f.window.emit('online');
    f.tick(); f.fail(false); await f.window.emit('online');
    assert.equal(f.counts().checks, 6);
});

test('already-installing and waiting updates show a notice without reloading', async () => {
    const f = fixture({ installing: true });
    await f.window.emit('load');
    assert.equal(f.notice.hidden, true);
    f.registration.waiting = f.worker;
    await f.worker.emit('statechange');
    assert.equal(f.notice.hidden, false);
    assert.equal(f.counts().reloads, 0);
    const existing = fixture({ waiting: true });
    await existing.window.emit('load');
    assert.equal(existing.notice.hidden, false);
});

test('recording or unsent media prevents activation; successful activation reloads exactly once', async () => {
    const f = fixture({ waiting: true });
    await f.window.emit('load');
    f.busy(true); f.api.apply();
    assert.equal(f.worker.messages.length, 0);
    f.api.notice(); // Blazor refreshes the layout after the button callback.
    assert.match(f.label.textContent, /Finish sending/);
    f.busy(false); f.window.yapRecording = {}; f.api.apply();
    assert.equal(f.worker.messages.length, 0);
    f.window.yapRecording = null; f.api.apply(); f.api.apply();
    assert.deepEqual(f.worker.messages, ['activate']);
    await f.serviceWorker.emit('controllerchange');
    await f.serviceWorker.emit('controllerchange');
    assert.equal(f.counts().reloads, 1);
});

test('another tab activating does not reload an in-progress composer', async () => {
    const f = fixture();
    await f.window.emit('load');
    await f.serviceWorker.emit('controllerchange');
    assert.equal(f.counts().reloads, 0);
    assert.equal(f.notice.hidden, false);
    f.busy(true); f.api.apply();
    assert.equal(f.counts().reloads, 0);
    f.busy(false); f.api.apply();
    assert.equal(f.counts().reloads, 1);
});

test('first installation never prompts or forces a reload', async () => {
    const f = fixture({ controlled: false });
    await f.window.emit('load');
    await f.serviceWorker.emit('controllerchange');
    assert.equal(f.notice.hidden, true);
    assert.equal(f.counts().reloads, 0);
});

test('update toast can be dismissed without activating or reloading', async () => {
    const f = fixture({ waiting: true }); await f.window.emit('load');
    assert.equal(f.notice.hidden, false); f.api.dismiss();
    assert.equal(f.notice.hidden, true); f.tick(); await f.interval();
    assert.equal(f.notice.hidden, true); assert.equal(f.counts().reloads, 0);
    assert.deepEqual(f.worker.messages, []);
});


test('installed state shows the update before waiting is populated by the browser', async () => {
    const f = fixture({ installing: true }); await f.window.emit('load');
    f.worker.state = 'installed';
    await f.worker.emit('statechange');
    assert.equal(f.registration.waiting, null);
    assert.equal(f.notice.hidden, false);
    f.api.apply();
    assert.deepEqual(f.worker.messages, ['activate']);
});

test('dismissing one update does not hide a later release', async () => {
    const f = fixture({ waiting: true }); await f.window.emit('load');
    f.api.dismiss();
    f.registration.waiting = { state: 'installed', postMessage() {} };
    f.api.notice();
    assert.equal(f.notice.hidden, false);
});
