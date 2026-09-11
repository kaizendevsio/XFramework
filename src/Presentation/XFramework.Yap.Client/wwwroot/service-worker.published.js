self.importScripts('./service-worker-assets.js');
const prefix = 'yap-shell-';
const name = prefix + self.assetsManifest.version;
const assets = self.assetsManifest.assets.filter(asset => !/^(?:api|auth|health)\//.test(asset.url) && !/^service-worker/.test(asset.url));
const known = new Set(assets.map(asset => new URL(asset.url, self.location).href));
self.addEventListener('install', event => event.waitUntil((async () => {
    const cache = await caches.open(name);
    await cache.addAll(assets.map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' })));
})()));
self.addEventListener('activate', event => event.waitUntil((async () => {
    for (const key of await caches.keys()) if (key.startsWith(prefix) && key !== name) await caches.delete(key);
})()));
self.addEventListener('message', event => { if (event.data === 'activate') self.skipWaiting(); });
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);
    if (event.request.method !== 'GET' || url.origin !== self.location.origin || /^\/(?:api|auth|health)(?:\/|$)/.test(url.pathname)) return;
    if (event.request.mode === 'navigate') {
        event.respondWith(caches.open(name).then(cache => cache.match('index.html')).then(response => response || fetch(event.request)));
    } else if (known.has(url.href)) {
        event.respondWith(caches.open(name).then(cache => cache.match(event.request)).then(response => response || fetch(event.request)));
    }
});
