self.importScripts('./service-worker-assets.js');
const prefix = 'yap-shell-';
const name = prefix + self.assetsManifest.version;
// Static-file middleware does not serve hidden or extensionless package metadata.
// Including it in addAll would reject the whole update and leave the old app active.
const assets = self.assetsManifest.assets.filter(asset => !/^(?:api|auth|health)\//.test(asset.url)
    && !/^service-worker/.test(asset.url)
    && !asset.url.split('/').some(part => part.startsWith('.'))
    && asset.url.split('/').at(-1).includes('.'));
const known = new Set(assets.map(asset => new URL(asset.url, self.location).href));
// Photos are the only images the app serves from /api, so the shell cache above cannot hold
// them and they fall back to the HTTP cache - the one store an installed app loses when the
// system reclaims it, which is why every face reloads on a cold start. Cache Storage survives
// that. The URL names an immutable storage version, so a stored entry can only ever be the
// photo that URL stands for, and a replaced photo arrives as a different URL that misses.
const photos = /^\/api\/chat\/(?:people|conversations)\/[0-9a-f-]{36}\/photo$/i;
// One bucket per account, so a device two people share never serves one of them the other's
// face, and signing out drops that person's photos whole.
const photoCache = account => `yap-media-${account.replace(':', '-')}`;
async function photo(request, account) {
    const cache = await caches.open(photoCache(account));
    const stored = await cache.match(request);
    if (stored) return stored;
    const response = await fetch(request);
    // Only a complete, authorized body is worth keeping: a 401, a 404 or a bodiless 304 is not.
    if (response.status !== 200) return response;
    await cache.put(request, response.clone());
    // A replaced photo arrives under a new version, so the one it replaced would otherwise sit
    // in this bucket for good. Dropping every other version of the same person keeps the bucket
    // the size of the faces on screen and keeps a deleted photo actually deleted.
    const path = new URL(request.url).pathname;
    for (const key of await cache.keys())
        if (key.url !== request.url && new URL(key.url).pathname === path) await cache.delete(key);
    return response;
}
self.addEventListener('install', event => event.waitUntil((async () => {
    const cache = await caches.open(name);
    await cache.addAll(assets.map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' })));
})()));
self.addEventListener('activate', event => event.waitUntil((async () => {
    for (const key of await caches.keys()) if (key.startsWith(prefix) && key !== name) await caches.delete(key);
})()));
self.addEventListener('message', event => { if (event.data === 'activate') event.waitUntil(self.skipWaiting()); });
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);
    if (event.request.method !== 'GET' || url.origin !== self.location.origin) return;
    if (photos.test(url.pathname)) {
        const account = url.searchParams.get('account') ?? '';
        // No account in the URL means the request cannot be attributed to anyone, and an
        // unattributed photo has no bucket it would be safe to store in: let it go to the
        // network, where the endpoint rejects it anyway.
        if (/^[0-9a-f]{32}:[0-9a-f]{32}$/i.test(account)) event.respondWith(photo(event.request, account));
        return;
    }
    if (/^\/(?:api|auth|health)(?:\/|$)/.test(url.pathname)) return;
    if (event.request.mode === 'navigate') {
        const page = url.pathname === '/diagnostics.html' ? 'diagnostics.html' : 'index.html';
        event.respondWith(caches.open(name).then(cache => cache.match(page)).then(response => response || fetch(event.request)));
    } else if (known.has(url.href)) {
        event.respondWith(caches.open(name).then(cache => cache.match(event.request)).then(response => response || fetch(event.request)));
    }
});
// Push must behave identically in development and in published builds, so these handlers are
// duplicated verbatim in the other worker file and both copies are covered by the same test.
// The payload carries routing identifiers only: message bodies are end-to-end encrypted and the
// server cannot read them, so the worker renders a generic notice and the app fills in the rest.
self.addEventListener('push', event => event.waitUntil((async () => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch { /* A wake-up with no readable payload still notifies. */ }
    const call = data.kind === 'call';
    const thread = typeof data.threadId === 'string' ? data.threadId : null;
    // A push service may hold a ring until the last moment of its TTL, so a call push can arrive
    // after the invite it describes has timed out. Ringing then would offer a call the
    // authenticated flow is going to refuse, so a late ring reports the missed call instead.
    // It still shows something: a userVisibleOnly subscription that handles a push without
    // showing a notification can cost the whole origin its push permission, and "you missed a
    // call" is both honest and the one thing the person actually needs to know.
    const stale = call && typeof data.expiresAt === 'number' && data.expiresAt * 1000 <= Date.now();
    const ringing = call && !stale;
    // Foreground devices are filtered before sending. A delivered Web Push must remain
    // user-visible, including races where the app becomes visible while it is in transit.
    await self.registration.showNotification(ringing ? 'Incoming call' : stale ? 'Missed call' : 'New message', {
        body: ringing ? 'Tap to answer in Yap.' : stale ? 'The call ended before this device could ring.' : 'Open Yap to read it.',
        icon: 'yap-app-v2-192.png', badge: 'yap-app-v2-192.png',
        // One banner per conversation, but every call ring replaces and re-alerts. A late ring
        // keeps the call's tag so it replaces that call's stale banner instead of stacking a
        // second one under it.
        tag: call ? `yap-call-${data.reference ?? ''}` : `yap-thread-${thread ?? 'inbox'}`,
        renotify: !stale, requireInteraction: ringing, silent: false,
        // A short double buzz, distinct from the single buzz of a message. Requested, not
        // promised: the option is ignored wherever the Vibration API is absent, iOS included.
        vibrate: ringing ? [200, 100, 200] : undefined,
        // Also advisory - a browser that renders no action buttons (iOS again) still delivers the
        // banner, and tapping its body does exactly what Open call does, so nothing is lost.
        actions: ringing ? [{ action: 'open', title: 'Open call' }, { action: 'dismiss', title: 'Dismiss' }] : [],
        data: { kind: ringing ? 'call' : stale ? 'missed' : 'message', url: thread ? `/chat/${thread}` : '/' }
    });
})()));
self.addEventListener('notificationclick', event => {
    event.notification.close();
    // Dismiss silences this device and nothing else: no request is sent, so the call keeps
    // ringing on the person's other devices and no participant is told anything. Declining for
    // real is an authenticated action that belongs to the app, not to a worker.
    if (event.action === 'dismiss') return;
    // Open call and a tap on the body are the same thing: surface the app and let it decide. The
    // worker never checks or joins a call itself - it holds no credentials, and the microphone
    // stays untouched until the person accepts in the app.
    const url = new URL(event.notification.data?.url || '/', self.location).href;
    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        // Reuse a window that is already open rather than starting a second copy of the app. The
        // page decides whether it actually needs to move, so opening the conversation already on
        // screen costs nothing. WindowClient.navigate is not used: it is unavailable for
        // uncontrolled clients and would force a full reload even when the app is already there.
        const mine = windows.find(client => new URL(client.url).origin === self.location.origin);
        if (mine) {
            if (mine.focus) await mine.focus();
            mine.postMessage({ type: 'yap-notification-click', url });
            return;
        }
        await self.clients.openWindow(url);
    })());
});
