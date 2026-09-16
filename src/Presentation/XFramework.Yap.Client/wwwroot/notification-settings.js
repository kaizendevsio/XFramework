// Whether a notification may show the sender and the decrypted message text on this device.
//
// The service worker has to read it, so it cannot live in localStorage, and it must not live in
// yap-encryption-v1 either: that database is the key store and its schema belongs to encryption.mjs.
// One small database of its own, keyed per account, never synced and never sent anywhere.
//
// Parses as both a classic script and an ES module: the page loads it with a <script> tag and the
// module service worker imports it.
self.yapNotificationSettings = (() => {
    const open = () => new Promise((resolve, reject) => {
        const request = indexedDB.open('yap-notifications-v1', 1);
        request.onupgradeneeded = () => request.result.createObjectStore('settings');
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
    async function run(mode, action) {
        const db = await open();
        try {
            return await new Promise((resolve, reject) => {
                const transaction = db.transaction('settings', mode);
                const request = action(transaction.objectStore('settings'));
                transaction.oncomplete = () => resolve(request.result);
                transaction.onerror = () => reject(transaction.error);
                transaction.onabort = () => reject(transaction.error);
            });
        } finally { db.close(); }
    }
    const key = account => String(account).toLowerCase().replaceAll('-', '');
    const api = {
        // On by default. The text is decrypted on this device and never leaves it, every other
        // messenger behaves this way, and a banner that says nothing is the reason people turn
        // notifications off entirely. A storage failure reads as off: if the preference cannot be
        // confirmed, the safe answer is the generic banner.
        async preview(account) { try { return await run('readonly', store => store.get(key(account))) !== false; } catch { return false; } },
        setPreview(account, enabled) { return run('readwrite', store => store.put(enabled === true, key(account))).then(() => enabled === true); }
    };
    // The page and the worker have to agree, so Settings reads and writes this same store.
    if (typeof window !== 'undefined') (window.yap ??= {}).notifications = api;
    return api;
})();
