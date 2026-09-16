import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const read = name => readFileSync(new URL(`../../Presentation/XFramework.Yap.Client/wwwroot/${name}`, import.meta.url), 'utf8');
const source = read('service-worker.published.js');
const workers = { 'service-worker.published.js': source, 'service-worker.js': read('service-worker.js') };

// A response the worker can store and hand back, which is the whole point of the photo cache:
// what comes out of Cache Storage has to be usable without the network being consulted again.
class FakeResponse {
    constructor(status, body) { this.status = status; this.ok = status >= 200 && status < 300; this.body = body; }
    clone() { return new FakeResponse(this.status, this.body); }
}

function context(script, { failDownload = false, windows = [], network = () => new FakeResponse(200, 'jpeg'), preview = null, budget, moduleWorker = false } = {}) {
    const handlers = {}, downloads = [], deleted = [], shown = [], opened = [], fetched = [];
    const paths = ['index.html', '_framework/app.wasm', 'vendor/openpgp/openpgp.min.mjs', 'vendor/openpgp/LICENSE.txt',
        '_content/Bolt.Media.Browser/sframe/.gitattributes', '_content/Bolt.Media.Browser/sframe/SHA256SUMS',
        'vendor/openpgp/LICENSE', 'api/private.json', 'service-worker-assets.js'];
    const self = { assetsManifest: { version: 'new', assets: paths.map(url => ({ url, hash: 'sha256-test' })) },
        location: new URL('https://yap.test/service-worker.js'), addEventListener: (name, handler) => handlers[name] = handler,
        clients: { matchAll: async () => windows, openWindow: async url => { opened.push(url); } },
        registration: { showNotification: async (title, options) => { shown.push({ title, options }); } } };
    // Real bucket contents, not a call log: a cache that only records writes cannot tell a
    // second visit that was served locally from one that quietly went back to the network.
    const buckets = new Map([['yap-shell-old', new Map()], ['yap-shell-new', new Map()], ['private-media', new Map()]]);
    const key = request => request.url ?? request;
    const sandbox = { self, URL, Set, Map, setTimeout, clearTimeout, AbortController, console,
        Request: class { constructor(url, options) { this.url = url; Object.assign(this, options); } },
        fetch: async request => { fetched.push(key(request)); return network(key(request)); },
        caches: {
            open: async name => {
                if (!buckets.has(name)) buckets.set(name, new Map());
                const entries = buckets.get(name);
                return {
                    addAll: async requests => { downloads.push(...requests); if (failDownload) throw Error('Integrity mismatch'); },
                    match: async request => entries.get(key(request)),
                    put: async (request, response) => { entries.set(key(request), response); },
                    keys: async () => [...entries.keys()].map(url => ({ url })),
                    delete: async request => entries.delete(key(request))
                };
            },
            keys: async () => [...buckets.keys()],
            delete: async name => { deleted.push(name); return buckets.delete(name); }
        } };
    const sandboxed = vm.createContext(sandbox);
    // importScripts really loads, the way the browser does: a worker whose shared push module is
    // stubbed out would prove nothing about the file that actually ships.
    self.importScripts = moduleWorker
        ? () => { throw new TypeError('importScripts is not available in modules'); }
        : (...names) => { for (const name of names) vm.runInContext(read(name.replace('./', '')), sandboxed, { filename: name }); };
    // A module worker resolves the same files through static imports before the worker body runs.
    if (moduleWorker) vm.runInContext(read('notifications.js'), sandboxed, { filename: 'notifications.js' });
    vm.runInContext(script, sandboxed, { filename: 'worker' });
    // What the module worker does after its imports resolve. Classic workers never get one.
    if (preview) self.yapNotifications.preview = preview;
    if (budget !== undefined) self.yapNotifications.budget = budget;
    const run = async (event, detail) => { let work; handlers[event]({ ...detail, waitUntil: value => work = value }); return work; };
    // Returns null when the worker never called respondWith - the request was left to the
    // browser, which is the only correct outcome for anything the worker must not touch.
    const fetchEvent = async (url, method = 'GET') => {
        let responded = null;
        handlers.fetch({ request: { url, method }, respondWith: value => { responded = value; }, waitUntil: () => {} });
        return responded === null ? null : await responded;
    };
    return { run, fetchEvent, handlers, downloads, deleted, shown, opened, fetched, buckets, self };
}

const fixture = options => context(source, options);
const windowClient = (url, visibilityState = 'hidden') => {
    const posted = [], navigated = [];
    return { url, visibilityState, posted, navigated, focused: false,
        postMessage: value => posted.push(value), focus: async function () { this.focused = true; },
        navigate: async value => { navigated.push(value); } };
};
const pushEvent = payload => ({ data: payload === undefined ? null : { json: () => { if (payload === null) throw Error('not json'); return payload; } } });
const account = `${'a'.repeat(32)}:${'b'.repeat(32)}`;
const thread = '5f2b8f3c-0000-4000-8000-000000000002';

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

// The defect these cover: an installed app loses the HTTP cache whenever the system reclaims it,
// so the only photos that survive a cold start are the ones the worker put in Cache Storage.
const photoUrl = (person, version, account = 'a'.repeat(32) + ':' + 'b'.repeat(32)) =>
    `https://yap.test/api/chat/people/${person}/photo?account=${account}&v=${version}`;
const person = '5f2b8f3c-0000-4000-8000-000000000001';

test('a photo is fetched once and served from the account bucket afterwards', async () => {
    const f = fixture();
    const first = await f.fetchEvent(photoUrl(person, 'v1'));
    const second = await f.fetchEvent(photoUrl(person, 'v1'));
    assert.equal(first.status, 200);
    assert.equal(second.status, 200);
    assert.equal(f.fetched.length, 1, 'the second visit must not reach the network');
    assert.deepEqual([...f.buckets.get(`yap-media-${'a'.repeat(32)}-${'b'.repeat(32)}`).keys()], [photoUrl(person, 'v1')]);
});

test('a changed photo is downloaded again and replaces the version it supersedes', async () => {
    const f = fixture();
    await f.fetchEvent(photoUrl(person, 'v1'));
    await f.fetchEvent(photoUrl(person, 'v2'));
    assert.equal(f.fetched.length, 2, 'a new version is a new URL and must miss');
    assert.deepEqual([...f.buckets.get(`yap-media-${'a'.repeat(32)}-${'b'.repeat(32)}`).keys()], [photoUrl(person, 'v2')]);
});

test('two accounts on one device never read each other photos', async () => {
    const other = 'c'.repeat(32) + ':' + 'd'.repeat(32);
    const f = fixture();
    await f.fetchEvent(photoUrl(person, 'v1'));
    await f.fetchEvent(photoUrl(person, 'v1', other));
    assert.equal(f.fetched.length, 2, 'the second account must not be served the first account copy');
    assert.ok(f.buckets.has(`yap-media-${'a'.repeat(32)}-${'b'.repeat(32)}`));
    assert.ok(f.buckets.has(`yap-media-${'c'.repeat(32)}-${'d'.repeat(32)}`));
});

test('a rejected photo is never stored, so access is re-checked every time', async () => {
    const f = fixture({ network: () => new FakeResponse(401, '') });
    const first = await f.fetchEvent(photoUrl(person, 'v1'));
    await f.fetchEvent(photoUrl(person, 'v1'));
    assert.equal(first.status, 401);
    assert.equal(f.fetched.length, 2);
    assert.equal(f.buckets.has(`yap-media-${'a'.repeat(32)}-${'b'.repeat(32)}`), true);
    assert.equal(f.buckets.get(`yap-media-${'a'.repeat(32)}-${'b'.repeat(32)}`).size, 0);
});

test('an unattributable photo and every other private request are left to the browser', async () => {
    const f = fixture();
    assert.equal(await f.fetchEvent(`https://yap.test/api/chat/people/${person}/photo`), null, 'no account scope, no bucket');
    assert.equal(await f.fetchEvent(`https://yap.test/api/chat/people/${person}/photo?account=nonsense&v=v1`), null);
    assert.equal(await f.fetchEvent('https://yap.test/api/chat/conversations'), null);
    assert.equal(await f.fetchEvent('https://yap.test/api/session'), null);
    assert.equal(await f.fetchEvent('https://yap.test/auth/login'), null);
    assert.equal(await f.fetchEvent(photoUrl(person, 'v1'), 'POST'), null);
    assert.equal(f.fetched.length, 0, 'the worker must not fetch anything it does not answer');
});

test('a group photo caches on the same account-scoped terms as a person', async () => {
    const f = fixture();
    const url = `https://yap.test/api/chat/conversations/${person}/photo?account=${'a'.repeat(32)}:${'b'.repeat(32)}&v=v1`;
    await f.fetchEvent(url);
    await f.fetchEvent(url);
    assert.equal(f.fetched.length, 1);
    assert.deepEqual([...f.buckets.get(`yap-media-${'a'.repeat(32)}-${'b'.repeat(32)}`).keys()], [url]);
});

// The invariant that keeps development, published, classic and module workers from drifting: one
// copy of the push logic, in a file that has to parse as both a classic script and an ES module.
test('the push handling is written once and loaded by every worker', () => {
    const shared = read('notifications.js');
    assert.ok(!/^\s*(?:import|export)\s/m.test(shared), 'importScripts and a module import must both be able to load it');
    assert.ok(!/^\s*(?:import|export)\s/m.test(read('notification-settings.js')));
    for (const worker of Object.values(workers)) {
        assert.ok(!worker.includes('showNotification'), 'a second copy of the banner is how the two workers drift apart');
        assert.ok(worker.includes("self.importScripts('./notifications.js')") && worker.includes('yapNotifications.install(self)'));
    }
    // Order matters: each import satisfies an "already loaded?" guard in the file after it, and a
    // service worker may not use import(), so the decryptor has to be resolved at startup.
    const module = read('service-worker.module.js');
    assert.deepEqual([...module.matchAll(/^import .*'(\.\/[^']+)'/gm)].map(m => m[1]),
        ['./service-worker-assets.js', './notifications.js', './service-worker.js', './notification-preview.mjs']);
    assert.ok(module.includes('self.yapNotifications.preview = preview'));
});

// importScripts still exists inside a module worker and throws the moment it is called, so the
// guards have to be satisfied by the module worker's imports rather than by a type check.
test('the published worker never calls importScripts once its imports are already loaded', async () => {
    const f = fixture({ moduleWorker: true, preview: async () => ({ title: 'Ada Lovelace', body: 'Dinner at eight?' }) });
    await f.run('install');
    assert.equal(f.downloads.length, 4, 'the offline shell still installs');
    await f.run('push', pushEvent({ version: 1, kind: 'message', threadId: thread, account }));
    assert.deepEqual(f.shown.map(x => x.title), ['New message', 'Ada Lovelace']);
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

    // Show-then-replace. The generic banner is on screen before anything that can fail starts, so
    // every path below that does not produce plaintext simply stops and leaves it there.
    const message = { version: 1, kind: 'message', threadId: thread, account };

    test(`${name}: a classic worker has no resolver, so the banner stays generic`, async () => {
        const f = context(script);
        await f.run('push', pushEvent(message));
        assert.equal(f.shown.length, 1);
        assert.equal(f.shown[0].title, 'New message');
    });

    test(`${name}: a decrypted preview replaces the generic banner in place and silently`, async () => {
        const f = context(script, { preview: async () => ({ title: 'Ada Lovelace', body: 'Dinner at eight?' }) });
        await f.run('push', pushEvent(message));
        assert.equal(f.shown.length, 2, 'the generic banner must appear first, then be replaced');
        assert.equal(f.shown[0].title, 'New message');
        assert.equal(f.shown[1].title, 'Ada Lovelace');
        assert.equal(f.shown[1].options.body, 'Dinner at eight?');
        // Same tag replaces rather than stacks; the device already buzzed a moment ago.
        assert.equal(f.shown[1].options.tag, f.shown[0].options.tag);
        assert.equal(f.shown[1].options.renotify, false);
        assert.equal(f.shown[1].options.silent, true);
        assert.equal(f.shown[1].options.data.url, `/chat/${thread}`, 'a replaced banner still deep links');
    });

    // Every one of these is a real state: an unenrolled device, an envelope this device cannot
    // open, no network, a budget overrun, the setting off, and a push for an account this device
    // is not currently signed into. The resolver reports them all the same way - nothing.
    for (const [reason, resolver] of [
        ['this device holds no encryption identity', async () => null],
        ['the envelope cannot be decrypted', async () => { throw new Error('This account is not an encrypted recipient.'); }],
        ['the device is offline', async () => { throw new TypeError('Failed to fetch'); }],
        ['the account cannot be resolved', async () => null],
        ['the message decrypts to nothing worth showing', async () => null]
    ]) {
        test(`${name}: the generic banner is left alone when ${reason}`, async () => {
            const f = context(script, { preview: resolver });
            await f.run('push', pushEvent(message));
            assert.equal(f.shown.length, 1);
            assert.equal(f.shown[0].title, 'New message');
            assert.equal(f.shown[0].options.body, 'Open Yap to read it.');
        });
    }

    test(`${name}: a decrypt that overruns its budget never replaces the banner`, async () => {
        const f = context(script, { budget: 5, preview: () => new Promise(done => setTimeout(() => done({ title: 'Too late', body: 'x' }), 60)) });
        await f.run('push', pushEvent(message));
        assert.equal(f.shown.length, 1);
        assert.equal(f.shown[0].title, 'New message');
    });

    test(`${name}: a resolver is asked for a message and never for a call`, async () => {
        const asked = [];
        const f = context(script, { preview: async payload => { asked.push(payload); return null; } });
        await f.run('push', pushEvent({ version: 1, kind: 'call', threadId: thread, reference: 'call1', account }));
        await f.run('push', pushEvent({ version: 1, kind: 'message', account }));
        assert.deepEqual(asked, [], 'a ring must not wait on anything, and an inbox push has no thread to read');
        assert.equal(f.shown.length, 2);
    });

    test(`${name}: a resolver is aborted once the banner is settled`, async () => {
        let signal = null;
        const f = context(script, { preview: async (_payload, aborts) => { signal = aborts; return null; } });
        await f.run('push', pushEvent(message));
        assert.equal(signal.aborted, true, 'an abandoned fetch must not outlive the push event');
    });
}
