import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/startup-recovery.js', import.meta.url), 'utf8');
function fixture({ waiting = true, fail = false, online = true } = {}) {
    const button = {}, notice = {}, navigations = [], messages = [], listeners = new Set();
    const worker = { state: 'installed', addEventListener: (_, fn) => listeners.add(fn), removeEventListener: (_, fn) => listeners.delete(fn),
        postMessage(value) { messages.push(value); worker.state = 'activated'; for (const fn of [...listeners]) fn(); } };
    const registration = { waiting: waiting ? worker : null, async update() { if (fail) throw Error('download-failed'); } };
    vm.runInNewContext(source, { document: { getElementById: id => id === 'recover' ? button : notice },
        navigator: { onLine: online, serviceWorker: { async register(url, options) { assert.equal(url, '/service-worker.js'); assert.equal(options.updateViaCache, 'none'); return registration; } } },
        window: {}, location: { replace: url => navigations.push(url) }, setTimeout, clearTimeout });
    return { button, notice, navigations, messages, listeners, registration, worker };
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
