import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// The server deletes a subscription when the push service answers 404/410, and nothing else ever
// told it about this device again: the phone kept a subscription (or a granted permission) while
// every message to that account went out as in-app only. These tests pin the self-repair.
const read = name => readFileSync(new URL(`../../Presentation/XFramework.Yap.Client/wwwroot/${name}`, import.meta.url), 'utf8');
const KEY = 'BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8';
const decode = value => Uint8Array.from(Buffer.from(value, 'base64url'));

function fixture({ permission = 'granted', subscribed = true, registered = false, stored = {}, subscribeFails = false } = {}) {
    const listeners = {}, requests = [], storage = new Map(Object.entries(stored));
    let endpoints = 0, current = null;
    const subscription = endpoint => ({
        endpoint,
        options: { applicationServerKey: decode(KEY).buffer },
        expirationTime: null,
        getKey: name => new Uint8Array(name === 'p256dh' ? 65 : 16).fill(name === 'p256dh' ? 4 : 1).buffer,
        unsubscribe: async () => { if (current?.endpoint === endpoint) current = null; return true; }
    });
    if (subscribed) current = subscription('https://push.test/device-0');
    const server = { endpoints: new Set(registered && current ? [current.endpoint] : []) };
    const pushManager = {
        getSubscription: async () => current,
        subscribe: async options => {
            if (subscribeFails) throw new DOMException('no', 'AbortError');
            assert.equal(options.userVisibleOnly, true);
            current = subscription(`https://push.test/device-${++endpoints}`);
            return current;
        }
    };
    const respond = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body });
    const context = vm.createContext({
        self: {}, document: { visibilityState: 'visible', addEventListener: (name, fn) => listeners[name] = fn },
        window: { yap: {}, PushManager: {}, Notification: {}, addEventListener: (name, fn) => listeners[name] = fn },
        Notification: { permission }, crypto: { randomUUID: () => 'window-1' }, AbortSignal, Uint8Array, ArrayBuffer, btoa, atob,
        localStorage: {
            getItem: key => storage.has(key) ? storage.get(key) : null,
            setItem: (key, value) => storage.set(key, String(value)),
            removeItem: key => storage.delete(key)
        },
        navigator: { userAgent: 'Android', serviceWorker: { addEventListener() {}, getRegistration: async () => ({ pushManager }) } },
        setInterval: fn => { listeners.heartbeat = fn; },
        fetch: async (url, options = {}) => {
            const body = options.body ? JSON.parse(options.body) : undefined;
            requests.push({ url, method: options.method ?? 'GET', headers: options.headers, body });
            if (url.endsWith('/push/config')) return respond(200, { enabled: true, publicKey: KEY, devices: server.endpoints.size });
            if (url.endsWith('/push/subscribe')) { server.endpoints.add(body.endpoint); return respond(204); }
            if (url.endsWith('/push/presence')) return respond(server.endpoints.has(body.endpoint) ? 204 : 404);
            return respond(404);
        }
    });
    vm.runInContext(read('worker-registration.js'), context);
    vm.runInContext(read('push.js'), context);
    return {
        push: context.window.yap.push, requests, server, storage, listeners,
        current: () => current,
        subscribes: () => requests.filter(x => x.url.endsWith('/push/subscribe')),
        forget: endpoint => server.endpoints.delete(endpoint)
    };
}
const settle = async () => { for (let i = 0; i < 20; i++) await new Promise(resolve => setImmediate(resolve)); };

test('a device the server dropped re-registers on startup without a prompt', async () => {
    const f = fixture({ subscribed: true, registered: false });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    const [registration] = f.subscribes();
    assert.ok(registration, 'the surviving browser subscription must be sent to the server again');
    assert.equal(registration.body.endpoint, 'https://push.test/device-0');
    assert.equal(registration.headers['X-Yap-Account'], 'tenant:credential');
    assert.equal(registration.headers.RequestVerificationToken, 'csrf');
    assert.ok(f.server.endpoints.has('https://push.test/device-0'));
    assert.equal(f.requests.at(-1).url, '/api/chat/push/presence', 'the foreground lease is renewed after repair');
    assert.equal(f.requests.at(-1).body.visible, true);
});

test('a registered device costs one presence call and nothing else', async () => {
    const f = fixture({ subscribed: true, registered: true });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    assert.deepEqual(f.requests.map(x => x.url), ['/api/chat/push/presence']);
});

test('granted permission with no browser subscription subscribes again', async () => {
    const f = fixture({ subscribed: false });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    assert.equal(f.subscribes().length, 1);
    assert.equal(f.subscribes()[0].body.endpoint, 'https://push.test/device-1');
});

test('an endpoint the server deleted after this device registered it is replaced, not resent', async () => {
    // Registered once for this account, then gone from the server: the push service said 404/410.
    const f = fixture({ subscribed: true, registered: false,
        stored: { 'yap.push.registration': JSON.stringify({ owner: 'tenant:credential', endpoint: 'https://push.test/device-0' }) } });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    assert.equal(f.subscribes().length, 1);
    assert.equal(f.subscribes()[0].body.endpoint, 'https://push.test/device-1', 'a gone endpoint would only be deleted again');
    assert.equal(f.current().endpoint, 'https://push.test/device-1');
});

test('an endpoint another account on this device registered is rebound rather than replaced', async () => {
    const f = fixture({ subscribed: true, registered: false,
        stored: { 'yap.push.registration': JSON.stringify({ owner: 'tenant:other', endpoint: 'https://push.test/device-0' }) } });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    assert.equal(f.subscribes()[0].body.endpoint, 'https://push.test/device-0');
    assert.equal(JSON.parse(f.storage.get('yap.push.registration')).owner, 'tenant:credential');
});

test('turning notifications off on this device is respected', async () => {
    const f = fixture({ subscribed: false, stored: { 'yap.push.off:tenant:credential': '1' } });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    assert.equal(f.subscribes().length, 0);
    assert.equal(f.requests.length, 0);
});

test('no permission means no prompt and no network', async () => {
    for (const permission of ['default', 'denied']) {
        const f = fixture({ permission, subscribed: false });
        f.push.presence('tenant:credential', 'csrf'); await settle();
        assert.equal(f.requests.length, 0, permission);
    }
});

test('a failed repair is retried later, not on every heartbeat', async () => {
    const f = fixture({ subscribed: false, subscribeFails: true });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    f.listeners.heartbeat(); await settle();
    f.listeners.heartbeat(); await settle();
    assert.equal(f.requests.filter(x => x.url.endsWith('/push/config')).length, 1);
});

test('a mid-session removal is repaired on the next foreground check', async () => {
    const f = fixture({ subscribed: true, registered: true });
    f.push.presence('tenant:credential', 'csrf'); await settle();
    f.forget('https://push.test/device-0');
    f.listeners.visibilitychange(); await settle();
    assert.equal(f.subscribes().length, 1);
});

test('Settings reports the server truth and remembers the choice per account', async () => {
    const f = fixture({ subscribed: true, registered: false });
    assert.equal(await f.push.ensure('tenant:credential', 'csrf'), true, 'ensure repairs and reports on');
    await f.push.disable('tenant:credential');
    assert.equal(f.storage.get('yap.push.off:tenant:credential'), '1');
    assert.equal(f.current(), null);
    assert.equal(await f.push.ensure('tenant:credential', 'csrf'), false, 'off stays off');
    const enabled = await f.push.enable(KEY, 'tenant:credential');
    assert.ok(enabled.endpoint);
    assert.equal(f.storage.has('yap.push.off:tenant:credential'), false);
    f.push.registered('tenant:credential', enabled.endpoint);
    assert.equal(JSON.parse(f.storage.get('yap.push.registration')).endpoint, enabled.endpoint);
});
