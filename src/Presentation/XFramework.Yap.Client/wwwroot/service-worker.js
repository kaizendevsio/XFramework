// Development runs use the network; published builds cache the complete WASM app.
self.addEventListener('fetch', () => {});
// Push behaviour lives in notifications.js so the development, published, classic and module
// workers cannot drift apart. The guard asks "already loaded?" rather than "am I a module?":
// importScripts still exists inside a module worker and throws the moment it is called, and the
// module worker imports notifications.js before this file precisely so the guard sees it.
if (!self.yapNotifications) self.importScripts('./notifications.js');
self.yapNotifications.install(self);
