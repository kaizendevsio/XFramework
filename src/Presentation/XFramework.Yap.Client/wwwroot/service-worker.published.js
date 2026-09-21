// Both guards ask "already loaded?" rather than "am I a module?": importScripts still exists
// inside a module worker and throws the moment it is called, and service-worker.module.js imports
// both of these files before this one precisely so the guards see them.
if (!self.assetsManifest) self.importScripts('./service-worker-assets.js');
if (!self.yapNotifications) self.importScripts('./notifications.js');
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
    // Mobile browsers can suspend an install mid-download. Keep verified batches in the new
    // version's isolated cache so the next attempt resumes instead of downloading the WASM
    // runtime again. Do not activate unless every required asset is present.
    for (let offset = 0; offset < assets.length; offset += 4) {
        const missing = [];
        for (const asset of assets.slice(offset, offset + 4))
            if (!await cache.match(asset.url))
                missing.push(new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
        if (missing.length) await cache.addAll(missing);
    }
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
self.yapNotifications.install(self);
