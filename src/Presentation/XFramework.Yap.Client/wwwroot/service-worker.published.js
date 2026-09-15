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
    if (event.request.method !== 'GET' || url.origin !== self.location.origin || /^\/(?:api|auth|health)(?:\/|$)/.test(url.pathname)) return;
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
    // A visible tab still holds its live socket and renders the message itself; a banner would
    // only duplicate it. Every other state - hidden, suspended, closed - needs the notification.
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    if (windows.some(client => client.visibilityState === 'visible')) return;
    await self.registration.showNotification(call ? 'Incoming call' : 'New message', {
        body: call ? 'Tap to answer in Yap.' : 'Open Yap to read it.',
        icon: 'yap-app-v2-192.png', badge: 'yap-app-v2-192.png',
        // One banner per conversation, but every call ring replaces and re-alerts.
        tag: call ? `yap-call-${data.reference ?? ''}` : `yap-thread-${thread ?? 'inbox'}`,
        renotify: call, requireInteraction: call, silent: false,
        data: { kind: call ? 'call' : 'message', url: thread ? `/chat/${thread}` : '/' }
    });
})()));
self.addEventListener('notificationclick', event => {
    event.notification.close();
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
