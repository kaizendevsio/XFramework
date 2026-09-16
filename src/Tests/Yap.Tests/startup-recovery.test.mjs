import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const read = name => readFileSync(new URL(`../../Presentation/XFramework.Yap.Client/wwwroot/${name}`, import.meta.url), 'utf8');
const source = read('startup-recovery.js');
function fixture({ waiting = true, fail = false, online = true } = {}) {
    const button = {}, notice = {}, navigations = [], messages = [], listeners = new Set(), registered = [];
    const worker = { state: 'installed', addEventListener: (_, fn) => listeners.add(fn), removeEventListener: (_, fn) => listeners.delete(fn),
        postMessage(value) { messages.push(value); worker.state = 'activated'; for (const fn of [...listeners]) fn(); } };
    const registration = { waiting: waiting ? worker : null, async update() { if (fail) throw Error('download-failed'); } };
    const self = {};
    const sandbox = { self, document: { getElementById: id => id === 'recover' ? button : notice },
        navigator: { onLine: online, serviceWorker: { async register(url, options) { registered.push(url); assert.equal(options.updateViaCache, 'none'); return registration; } } },
        window: {}, location: { replace: url => navigations.push(url) }, setTimeout, clearTimeout };
    const context = vm.createContext(sandbox);
    // The recovery page shares the app's one registration helper, so it cannot install a different
    // worker than the app decided on and leave the two fighting over the same scope.
    vm.runInContext(read('worker-registration.js'), context);
    vm.runInContext(source, context);
    return { button, notice, navigations, messages, listeners, registration, worker, registered };
}

test('explicit recovery activates a waiting update and reopens without touching account storage', async () => {
    const f = fixture();
    await f.button.onclick();
    assert.deepEqual(f.messages, ['activate']);
    assert.deepEqual(f.navigations, ['/']);
    assert.equal(f.listeners.size, 0);
});

test('recovery reopens the current app when no update is waiting', async () => {
    const f = fixture({ waiting: false }); await f.button.onclick();
    assert.deepEqual(f.navigations, ['/']); assert.deepEqual(f.messages, []);
});

test('failed and offline updates leave the page usable and do not reload', async () => {
    for (const options of [{ fail: true }, { online: false }]) {
        const f = fixture(options); await f.button.onclick();
        assert.deepEqual(f.navigations, []); assert.deepEqual(f.messages, []);
        assert.equal(f.button.disabled, false); assert.match(f.notice.textContent, /saved data has not been cleared/);
    }
});

test('stalled temporary-file cleanup cannot hold device watch open', () => {
    const deviceSource = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/device.js', import.meta.url), 'utf8');
    const window = { yap: {} };
    vm.runInNewContext(deviceSource, { window, navigator: { storage: { getDirectory: () => new Promise(() => {}) } },
        addEventListener() {}, document: { addEventListener() {} }, visualViewport: null,
        cancelAnimationFrame() {}, requestAnimationFrame() {}, Map, Set });
    assert.equal(window.yap.device.watch({}), undefined);
});

function lockFixture(request) {
    const records = [], window = { yap: { diagnostics: { record: (...args) => records.push(args) } } };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/device.js', import.meta.url), 'utf8'), {
        window, navigator: { locks: { request } }, AbortSignal,
        addEventListener() {}, document: { addEventListener() {} }, visualViewport: null,
        cancelAnimationFrame() {}, requestAnimationFrame() {}, Map, Set
    });
    return { acquire: () => window.yap.device.acquireDatabase(), records };
}

test('a busy database can time out repeatedly then open automatically without stealing the owner lock', async () => {
    let attempts = 0, owner;
    const f = lockFixture((name, options, callback) => {
        assert.equal(name, 'yap-sqlite');
        assert.equal(options.steal, undefined);
        assert.ok(options.signal);
        if (++attempts < 3) return Promise.reject(Object.assign(new Error('private data'), { name: 'TimeoutError' }));
        owner = callback(); return owner;
    });
    assert.equal(await f.acquire(), false);
    assert.equal(await f.acquire(), false);
    assert.equal(await f.acquire(), true);
    assert.equal(await f.acquire(), true);
    assert.equal(attempts, 3, 'successful acquisition stays held across subsequent calls');
    assert.equal(await Promise.race([owner.then(() => 'released'), Promise.resolve('held')]), 'held');
    assert.deepEqual(f.records.map(r => r[0]), ['storage.lock-waiting', 'storage.lock-waiting', 'storage.lock-acquired']);
    assert.equal(JSON.stringify(f.records).includes('private data'), false);
});

test('real lock errors remain errors and concurrent acquisition calls share one request', async () => {
    let fail, attempts = 0;
    const error = Object.assign(new Error('private url'), { name: 'SecurityError' });
    const f = lockFixture(() => { attempts++; return new Promise((_, reject) => fail = reject); });
    const first = f.acquire(), second = f.acquire();
    assert.equal(first, second); assert.equal(attempts, 1);
    fail(error);
    await assert.rejects(first, { name: 'SecurityError' });
    assert.equal(f.records[0][0], 'storage.lock-failed');
    assert.equal(JSON.stringify(f.records).includes('private url'), false);
});
