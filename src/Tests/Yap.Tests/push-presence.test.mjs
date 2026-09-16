import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const read = name => readFileSync(new URL(`../../Presentation/XFramework.Yap.Client/wwwroot/${name}`, import.meta.url), 'utf8');
const source = read('push.js');
function fixture() {
    const listeners = {}, requests = [];
    const document = { visibilityState: 'visible', addEventListener: (name, fn) => listeners[name] = fn };
    const window = { yap: {}, PushManager: {}, Notification: {}, addEventListener: (name, fn) => listeners[name] = fn };
    let fail = false;
    const self = {};
    const context = vm.createContext({ self, window, document, crypto: { randomUUID: () => 'window-1' }, AbortSignal,
        navigator: { serviceWorker: { addEventListener() {}, getRegistration: async () => ({
            pushManager: { getSubscription: async () => ({ endpoint: 'https://push.test/device' }) }
        }) } }, setInterval: fn => { listeners.heartbeat = fn; },
        fetch: async (url, options) => { requests.push({ url, ...options, body: JSON.parse(options.body) });
            if (fail) throw new TypeError('offline'); return { ok: true }; }
    });
    // Push reuses the app's registration through the shared helper rather than choosing a script.
    vm.runInContext(read('worker-registration.js'), context);
    vm.runInContext(source, context);
    return { push: window.yap.push, document, listeners, requests, offline: () => { fail = true; } };
}
const drain = () => new Promise(resolve => setImmediate(resolve));

test('foreground and hide updates are ordered, scoped and bounded by request timeout', async () => {
    const f = fixture(); f.push.presence('account', 'csrf'); await drain();
    assert.equal(f.requests[0].body.visible, true);
    assert.equal(f.requests[0].headers['X-Yap-Account'], 'account');
    assert.equal(f.requests[0].headers.RequestVerificationToken, 'csrf');
    assert.ok(f.requests[0].signal);
    f.document.visibilityState = 'hidden'; f.listeners.visibilitychange(); await drain();
    assert.equal(f.requests[1].body.visible, false);
    assert.equal(f.requests[0].body.windowId, f.requests[1].body.windowId);
    f.listeners.heartbeat(); await drain();
    assert.equal(f.requests.length, 2, 'background tabs must not extend a lease');
});

test('switching accounts clears the old lease, and offline failures stay quiet', async () => {
    const f = fixture(); f.push.presence('old', 'csrf'); await drain();
    f.push.presence('new', 'new-csrf'); await drain();
    assert.deepEqual(f.requests.slice(1).map(x => [x.headers['X-Yap-Account'], x.body.visible]), [['old', false], ['new', true]]);
    f.offline(); f.listeners.pagehide(); await drain();
    f.push.presence('', ''); await drain();
    assert.equal(f.requests.at(-1).body.visible, false);
});
