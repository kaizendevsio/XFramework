// Browser Push API plumbing. Subscription management goes through C#; lifecycle heartbeats
// use the current account header and antiforgery token supplied by ChatState.
//
// iOS only exposes PushManager to a home-screen installed PWA on 16.4 or newer, and every browser
// requires Notification.requestPermission to run inside a user gesture. `enable` is therefore
// written to reach requestPermission without awaiting anything first: the Blazor click handler
// calls straight into it, so the transient activation is still valid.
(() => {
    const supported = () => 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    const installed = () => matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
    const encode = buffer => {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (const byte of bytes) binary += String.fromCharCode(byte);
        return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    };
    const decode = value => {
        const padded = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(value.length + (4 - value.length % 4) % 4, '=');
        return Uint8Array.from(atob(padded), character => character.charCodeAt(0));
    };
    // The app registers its worker in updates.js; reuse that registration rather than racing it.
    const registration = async () => await navigator.serviceWorker.getRegistration()
        || await navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
    const describe = subscription => subscription && {
        endpoint: subscription.endpoint,
        p256dh: encode(subscription.getKey('p256dh')),
        auth: encode(subscription.getKey('auth')),
        expiresAt: subscription.expirationTime ? new Date(subscription.expirationTime).toISOString() : null,
        // A coarse label for the settings list. Never the full user agent string.
        label: /iPhone|iPad|iPod/.test(navigator.userAgent) ? 'iPhone or iPad'
            : /Android/.test(navigator.userAgent) ? 'Android'
            : /Mac/.test(navigator.userAgent) ? 'Mac' : 'This browser'
    };

    const windowId = crypto.randomUUID();
    let account = '', token = '', lastPresence = '', lastSent = 0;
    let presenceWork = Promise.resolve();
    // Serialize visibility updates so a slow foreground request cannot overwrite a later hide.
    const publishPresence = (visible = document.visibilityState === 'visible', force = false) => {
        const owner = account, csrf = token;
        if (!owner || !csrf || !supported()) return;
        presenceWork = presenceWork.catch(() => {}).then(async () => {
            const endpoint = await window.yap.push.endpoint();
            if (!endpoint) return;
            const key = `${owner}:${endpoint}:${visible}`;
            if (!force && key === lastPresence && Date.now() - lastSent < 20000) return;
            try {
                const response = await fetch('/api/chat/push/presence', {
                    method: 'POST', credentials: 'same-origin', keepalive: true, signal: AbortSignal.timeout(5000),
                    headers: { 'Content-Type': 'application/json', 'X-Yap-Account': owner, 'RequestVerificationToken': csrf },
                    body: JSON.stringify({ endpoint, windowId, visible })
                });
                if (response.ok) { lastPresence = key; lastSent = Date.now(); }
            } catch { /* A lost lease expires automatically; never toast for background work. */ }
        });
    };
    document.addEventListener('visibilitychange', () => publishPresence(undefined, true));
    window.addEventListener('pagehide', () => publishPresence(false, true));
    window.addEventListener('pageshow', () => publishPresence(undefined, true));
    setInterval(() => { if (document.visibilityState === 'visible') publishPresence(); }, 20000);

    window.yap.push = {
        presence(nextAccount, nextToken) {
            if (account && nextAccount !== account) publishPresence(false, true);
            account = nextAccount; token = nextToken;
            publishPresence();
        },
        // 'unsupported' and 'uninstalled' let Settings explain why instead of showing a dead toggle.
        state() {
            if (!supported()) return /iPhone|iPad|iPod/.test(navigator.userAgent) && !installed() ? 'uninstalled' : 'unsupported';
            return Notification.permission;
        },
        async endpoint() {
            if (!supported()) return null;
            try { return (await (await registration()).pushManager.getSubscription())?.endpoint ?? null; }
            catch { return null; }
        },
        // Returns the subscription for the caller to register, or a string reason it could not.
        async enable(applicationServerKey) {
            if (!supported()) return { error: 'unsupported' };
            // Must be first: awaiting anything else would spend the gesture's transient activation.
            const permission = Notification.permission === 'granted'
                ? 'granted' : await Notification.requestPermission();
            if (permission !== 'granted') return { error: permission === 'denied' ? 'denied' : 'dismissed' };
            try {
                const manager = (await registration()).pushManager;
                const existing = await manager.getSubscription();
                // A redeployed VAPID key invalidates an old subscription; resubscribe rather than
                // keeping an endpoint the push service will refuse to authorise.
                if (existing && encode(existing.options?.applicationServerKey ?? new ArrayBuffer(0)) !== applicationServerKey)
                    await existing.unsubscribe().catch(() => {});
                else if (existing) return describe(existing);
                return describe(await manager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: decode(applicationServerKey)
                }));
            } catch (error) {
                window.yap.diagnostics?.record('push.subscribe-failed', { reason: error?.name ?? 'unknown' });
                return { error: 'failed' };
            }
        },
        refreshPresence() { publishPresence(undefined, true); },
        async disable() {
            if (!supported()) return null;
            try {
                const subscription = await (await registration()).pushManager.getSubscription();
                if (!subscription) return null;
                const endpoint = subscription.endpoint;
                await subscription.unsubscribe();
                return endpoint;
            } catch { return null; }
        }
    };

    // Tapping a notification focuses this window and hands it the deep link, so the app moves
    // itself rather than the worker opening a second copy.
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('message', event => {
            if (event.data?.type !== 'yap-notification-click' || !event.data.url) return;
            const target = new URL(event.data.url, location.href);
            if (target.pathname !== location.pathname) location.assign(target.href);
        });
    }
})();
