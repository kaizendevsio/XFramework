import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const read = name => readFileSync(new URL(`../../Presentation/XFramework.Yap.Client/wwwroot/${name}`, import.meta.url), 'utf8');
const source = read('service-worker.published.js');
const workers = { 'service-worker.published.js': source, 'service-worker.js': read('service-worker.js') };

function context(script, { failDownload = false, windows = [] } = {}) {
    const handlers = {}, downloads = [], deleted = [], shown = [], opened = [];
    const paths = ['index.html', '_framework/app.wasm', 'vendor/openpgp/openpgp.min.mjs', 'vendor/openpgp/LICENSE.txt',
        '_content/Bolt.Media.Browser/sframe/.gitattributes', '_content/Bolt.Media.Browser/sframe/SHA256SUMS',
        'vendor/openpgp/LICENSE', 'api/private.json', 'service-worker-assets.js'];
    const self = { importScripts() {}, assetsManifest: { version: 'new', assets: paths.map(url => ({ url, hash: 'sha256-test' })) },
        location: new URL('https://yap.test/service-worker.js'), addEventListener: (name, handler) => handlers[name] = handler,
        clients: { matchAll: async () => windows, openWindow: async url => { opened.push(url); } },
        registration: { showNotification: async (title, options) => { shown.push({ title, options }); } } };
    vm.runInNewContext(script, { self, URL, Set,
        Request: class { constructor(url, options) { this.url = url; Object.assign(this, options); } },
        caches: { open: async () => ({ addAll: async requests => { downloads.push(...requests); if (failDownload) throw Error('Integrity mismatch'); } }),
            keys: async () => ['yap-shell-old', 'yap-shell-new', 'private-media'], delete: async key => { deleted.push(key); } } });
    const run = async (event, detail) => { let work; handlers[event]({ ...detail, waitUntil: value => work = value }); return work; };
    return { run, handlers, downloads, deleted, shown, opened };
}

const fixture = options => context(source, options);
const windowClient = (url, visibilityState = 'hidden') => {
    const posted = [], navigated = [];
    return { url, visibilityState, posted, navigated, focused: false,
        postMessage: value => posted.push(value), focus: async function () { this.focused = true; },
        navigate: async value => { navigated.push(value); } };
};
const pushEvent = payload => ({ data: payload === undefined ? null : { json: () => { if (payload === null) throw Error('not json'); return payload; } } });

test('install verifies runtime assets and the served license, excluding unservable metadata', async () => {
    const f = fixture(); await f.run('install');
    assert.deepEqual(f.downloads.map(r => r.url), ['index.html', '_framework/app.wasm', 'vendor/openpgp/openpgp.min.mjs', 'vendor/openpgp/LICENSE.txt']);
    assert.ok(f.downloads.every(r => r.integrity === 'sha256-test' && r.cache === 'no-cache'));
});

test('a broken runtime download still rejects installation and preserves existing caches', async () => {
    const f = fixture({ failDownload: true });
    await assert.rejects(f.run('install'), /Integrity mismatch/);
    assert.deepEqual(f.deleted, []);
});

test('activation removes only older app shells, preserving private media caches', async () => {
    const f = fixture(); await f.run('activate');
    assert.deepEqual(f.deleted, ['yap-shell-old']);
});

// Push has to behave identically in development and in published builds. A worker that only
// handles push in one of the two files fails silently in exactly the environment it was not tested in.
for (const [name, script] of Object.entries(workers)) {
    test(`${name}: a message push shows a generic notification deep linked to the conversation`, async () => {
        const f = context(script);
        await f.run('push', pushEvent({ version: 1, kind: 'message', threadId: '5f2b8f3c-0000-4000-8000-000000000001', notificationId: 'n1' }));
        assert.equal(f.shown.length, 1);
        assert.equal(f.shown[0].title, 'New message');
        assert.equal(f.shown[0].options.renotify, true);
        assert.equal(f.shown[0].options.data.url, '/chat/5f2b8f3c-0000-4000-8000-000000000001');
        assert.equal(f.shown[0].options.tag, 'yap-thread-5f2b8f3c-0000-4000-8000-000000000001');
    });

    test(`${name}: a notification never repeats content the server cannot read`, async () => {
        const f = context(script);
        await f.run('push', pushEvent({ version: 1, kind: 'message', threadId: 't', body: 'secret text', title: 'Alice' }));
        const rendered = JSON.stringify(f.shown[0]);
        assert.ok(!rendered.includes('secret text'), 'a push payload field must never reach the banner');
        assert.ok(!rendered.includes('Alice'));
        assert.equal(f.shown[0].options.body, 'Open Yap to read it.');
    });

    test(`${name}: a call push rings urgently and collapses on the call reference`, async () => {
        const f = context(script);
        await f.run('push', pushEvent({ version: 1, kind: 'call', threadId: 'abc', reference: 'call1' }));
        assert.equal(f.shown[0].title, 'Incoming call');
        assert.equal(f.shown[0].options.tag, 'yap-call-call1');
        assert.equal(f.shown[0].options.renotify, true);
        assert.equal(f.shown[0].options.requireInteraction, true);
    });

    test(`${name}: a ringing call asks for a short buzz and offers Open call and Dismiss`, async () => {
        const f = context(script);
        await f.run('push', pushEvent({ version: 1, kind: 'call', threadId: 't1', reference: 'call1', expiresAt: Math.floor(Date.now() / 1000) + 30 }));
        assert.equal(f.shown[0].title, 'Incoming call');
        assert.equal(JSON.stringify(f.shown[0].options.vibrate), '[200,100,200]');
        assert.equal(JSON.stringify(f.shown[0].options.actions), JSON.stringify([{ action: 'open', title: 'Open call' }, { action: 'dismiss', title: 'Dismiss' }]));
    });

    test(`${name}: a message push never buzzes like a call or offers call actions`, async () => {
        const f = context(script);
        await f.run('push', pushEvent({ version: 1, kind: 'message', threadId: 't1' }));
        assert.equal(f.shown[0].options.vibrate, undefined);
        assert.equal(JSON.stringify(f.shown[0].options.actions), '[]');
    });

    // A push service may hold a ring to the last second of its TTL. Showing nothing would risk the
    // origin's push permission; showing "Incoming call" would offer a call that no longer exists.
    test(`${name}: a call delivered after its deadline reports a missed call, not an invitation`, async () => {
        const f = context(script);
        await f.run('push', pushEvent({ version: 1, kind: 'call', threadId: 't1', reference: 'call1', expiresAt: Math.floor(Date.now() / 1000) - 1 }));
        assert.equal(f.shown.length, 1, 'a handler that shows nothing can cost the origin its push permission');
        assert.equal(f.shown[0].title, 'Missed call');
        assert.equal(f.shown[0].options.data.kind, 'missed');
        assert.equal(f.shown[0].options.requireInteraction, false);
        assert.equal(f.shown[0].options.renotify, false);
        assert.equal(f.shown[0].options.vibrate, undefined);
        assert.equal(JSON.stringify(f.shown[0].options.actions), '[]', 'nothing is left to open or dismiss');
        // Still the call's tag, so it replaces a stale ringing banner rather than stacking on it.
        assert.equal(f.shown[0].options.tag, 'yap-call-call1');
    });

    test(`${name}: Dismiss closes the banner on this device and tells no one`, async () => {
        const open = windowClient('https://yap.test/chat/t1');
        const f = context(script, { windows: [open] });
        let closed = false;
        await f.run('notificationclick', { action: 'dismiss', notification: { close: () => { closed = true; }, data: { kind: 'call', url: '/chat/t1' } } });
        assert.ok(closed);
        // Declining is an authenticated action: a dismissed banner must not reach the app, the
        // gateway or the other participants, so the call keeps ringing everywhere else.
        assert.deepEqual(open.posted, []);
        assert.equal(open.focused, false);
        assert.deepEqual(f.opened, []);
    });

    test(`${name}: Open call focuses the window already running Yap`, async () => {
        const open = windowClient('https://yap.test/');
        const f = context(script, { windows: [open] });
        await f.run('notificationclick', { action: 'open', notification: { close: () => {}, data: { kind: 'call', url: '/chat/t1' } } });
        assert.ok(open.focused);
        assert.equal(JSON.stringify(open.posted), JSON.stringify([{ type: 'yap-notification-click', url: 'https://yap.test/chat/t1' }]));
        assert.deepEqual(f.opened, []);
    });

    test(`${name}: Open call opens Yap when no window is running`, async () => {
        const f = context(script);
        await f.run('notificationclick', { action: 'open', notification: { close: () => {}, data: { kind: 'call', url: '/chat/t1' } } });
        // The worker holds no credentials: it only surfaces the app, which checks the call over
        // the authenticated flow. No call is joined and no microphone is touched here.
        assert.deepEqual(f.opened, ['https://yap.test/chat/t1']);
    });

    test(`${name}: an unreadable or empty payload still wakes the device`, async () => {
        for (const payload of [undefined, null]) {
            const f = context(script);
            await f.run('push', pushEvent(payload));
            assert.equal(f.shown.length, 1);
            assert.equal(f.shown[0].options.data.url, '/');
        }
    });

    test(`${name}: an already-dispatched push remains visible even if a window opens during delivery`, async () => {
        const visible = windowClient('https://yap.test/', 'visible');
        const f = context(script, { windows: [visible] });
        await f.run('push', pushEvent({ version: 1, kind: 'message', threadId: 't1' }));
        assert.equal(f.shown.length, 1);
    });

    test(`${name}: a hidden window still gets a banner, because its socket is torn down`, async () => {
        const hidden = windowClient('https://yap.test/chat/x');
        const f = context(script, { windows: [hidden] });
        await f.run('push', pushEvent({ version: 1, kind: 'message', threadId: 't1' }));
        assert.equal(f.shown.length, 1);
    });

    test(`${name}: tapping a notification focuses the open window and hands it the deep link`, async () => {
        const open = windowClient('https://yap.test/');
        const f = context(script, { windows: [open] });
        let closed = false;
        await f.run('notificationclick', { notification: { close: () => { closed = true; }, data: { kind: 'message', url: '/chat/t1' } } });
        assert.ok(closed);
        assert.ok(open.focused);
        assert.equal(JSON.stringify(open.posted), JSON.stringify([{ type: 'yap-notification-click', url: 'https://yap.test/chat/t1' }]));
        assert.deepEqual(f.opened, [], 'a second window would start a second copy of the app');
    });

    test(`${name}: tapping with nothing open launches the deep link`, async () => {
        const f = context(script);
        await f.run('notificationclick', { notification: { close: () => {}, data: { kind: 'call', url: '/chat/t9' } } });
        assert.deepEqual(f.opened, ['https://yap.test/chat/t9']);
    });

    test(`${name}: a window on another origin is never focused or navigated`, async () => {
        const foreign = windowClient('https://elsewhere.test/');
        const f = context(script, { windows: [foreign] });
        await f.run('notificationclick', { notification: { close: () => {}, data: { url: '/chat/t1' } } });
        assert.equal(foreign.focused, false);
        assert.deepEqual(f.opened, ['https://yap.test/chat/t1']);
    });
}
