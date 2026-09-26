// Browser Push API plumbing. Settings turns push on and off through C#; lifecycle heartbeats and
// the self-repair below use the current account header and antiforgery token supplied by ChatState.
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
    // worker-registration.js decides module or classic, so nothing here picks a script URL.
    const registration = () => self.yapWorker.existing();
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

    // Browser storage is a convenience here: without it, repair still runs but cannot tell a gone
    // endpoint from a rebound one, or remember an explicit "off".
    const stored = key => { try { return localStorage.getItem(key); } catch { return null; } };
    const store = (key, value) => {
        try { value === null ? localStorage.removeItem(key) : localStorage.setItem(key, value); } catch { /* See above. */ }
    };
    // "Off" is a per-device, per-account decision made in Settings; repair must never overrule it.
    const offKey = owner => `yap.push.off:${owner}`;
    const optedOut = owner => stored(offKey(owner)) === '1';
    // One browser holds one push subscription, so one record: which account it was last registered for.
    const RECORD = 'yap.push.registration';
    const record = () => { try { return JSON.parse(stored(RECORD) ?? 'null'); } catch { return null; } };
    const headers = (owner, csrf) => ({ 'Content-Type': 'application/json', 'X-Yap-Account': owner, 'RequestVerificationToken': csrf });
    const endpointOf = async () => {
        try { return (await (await registration()).pushManager.getSubscription())?.endpoint ?? null; }
        catch { return null; }
    };
    // Shared by Settings and repair. Only a key the browser actually reports can prove a mismatch:
    // treating a missing `options` as one would replace a working subscription for nothing.
    const subscribeWith = async (applicationServerKey, replace = false) => {
        const manager = (await registration()).pushManager;
        const existing = await manager.getSubscription();
        const key = existing?.options?.applicationServerKey;
        // A redeployed VAPID key invalidates an old subscription; resubscribe rather than
        // keeping an endpoint the push service will refuse to authorise.
        if (existing && (replace || (key && encode(key) !== applicationServerKey)))
            await existing.unsubscribe().catch(() => {});
        else if (existing) return existing;
        return await manager.subscribe({ userVisibleOnly: true, applicationServerKey: decode(applicationServerKey) });
    };

    // The server is what delivers, and it deletes a subscription the moment the push service answers
    // 404/410 (NotificationPushService). Nothing re-registered the device after that, so a phone could
    // keep a granted permission - and Settings could keep saying "On" - while every message went out
    // as in-app only. Repair re-sends the subscription whenever the server does not know it.
    //
    // Only with a permission granted earlier: no prompt, so no user gesture is needed. A subscription
    // this device registered for this same account that the server has since dropped was reported
    // gone by the push service, so it is replaced rather than resent. Anything else - a rebind by
    // another account on this device, an upload lost to an expired session - is resent as it is.
    const attempts = new Map();
    const repair = async (owner, csrf, immediate = false) => {
        if (!owner || !csrf || !supported() || Notification.permission !== 'granted' || optedOut(owner)) return false;
        if (!immediate && Date.now() - (attempts.get(owner) ?? -Infinity) < 300000) return false;
        attempts.set(owner, Date.now());
        try {
            const response = await fetch('/api/chat/push/config', {
                credentials: 'same-origin', headers: headers(owner, csrf), signal: AbortSignal.timeout(15000)
            });
            const config = response.ok ? await response.json() : null;
            if (!config?.enabled || typeof config.publicKey !== 'string') return false;
            const current = await endpointOf();
            const last = record();
            const gone = !!current && last?.owner === owner && last?.endpoint === current;
            const subscription = await subscribeWith(config.publicKey, gone);
            const saved = await fetch('/api/chat/push/subscribe', {
                method: 'POST', credentials: 'same-origin', headers: headers(owner, csrf), signal: AbortSignal.timeout(15000),
                body: JSON.stringify(describe(subscription))
            });
            if (!saved.ok) return false;
            store(RECORD, JSON.stringify({ owner, endpoint: subscription.endpoint }));
            window.yap.diagnostics?.record('push.repaired', { replaced: gone, resubscribed: !current });
            return true;
        } catch (error) {
            window.yap.diagnostics?.record('push.repair-failed', { reason: error?.name ?? 'unknown' });
            return false;
        }
    };

    const windowId = crypto.randomUUID();
    let account = '', token = '', lastPresence = '', lastSent = 0;
    let presenceWork = Promise.resolve();
    const sendPresence = async (owner, csrf, endpoint, visible) => {
        try {
            return await fetch('/api/chat/push/presence', {
                method: 'POST', credentials: 'same-origin', keepalive: true, signal: AbortSignal.timeout(5000),
                headers: headers(owner, csrf), body: JSON.stringify({ endpoint, windowId, visible })
            });
        } catch { return null; /* A lost lease expires automatically; never toast for background work. */ }
    };
    // Serialize visibility updates so a slow foreground request cannot overwrite a later hide.
    // The presence call doubles as the registration check: 404 means the server holds no such
    // subscription for this account. Repair runs only in the foreground, never on the way out.
    const publishPresence = (visible = document.visibilityState === 'visible', force = false) => {
        const owner = account, csrf = token;
        if (!owner || !csrf || !supported()) return;
        presenceWork = presenceWork.catch(() => {}).then(async () => {
            let endpoint = await endpointOf();
            if (!endpoint) {
                if (!visible || !await repair(owner, csrf) || !(endpoint = await endpointOf())) return;
                force = true;
            }
            if (!force && `${owner}:${endpoint}:${visible}` === lastPresence && Date.now() - lastSent < 20000) return;
            let response = await sendPresence(owner, csrf, endpoint, visible);
            if (response?.status === 404 && visible && await repair(owner, csrf) && (endpoint = await endpointOf()))
                response = await sendPresence(owner, csrf, endpoint, visible);
            if (response?.ok) { lastPresence = `${owner}:${endpoint}:${visible}`; lastSent = Date.now(); }
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
            return await endpointOf();
        },
        // What the Settings toggle shows: whether the server can reach this device for this account,
        // repairing first when it cannot. A browser-side subscription alone proves nothing - the
        // server may have deleted its copy. Offline or any answer but 404 keeps the browser's view.
        async ensure(owner, csrf) {
            if (!owner || !csrf || !supported() || Notification.permission !== 'granted' || optedOut(owner)) return false;
            let result = false;
            presenceWork = presenceWork.catch(() => {}).then(async () => {
                const endpoint = await endpointOf();
                const response = endpoint ? await sendPresence(owner, csrf, endpoint, document.visibilityState === 'visible') : null;
                result = response?.ok || (!!endpoint && response?.status !== 404) || await repair(owner, csrf, true);
            });
            await presenceWork;
            return result;
        },
        // Returns the subscription for the caller to register, or a string reason it could not.
        async enable(applicationServerKey, owner = account) {
            if (!supported()) return { error: 'unsupported' };
            // Must be first: awaiting anything else would spend the gesture's transient activation.
            const permission = Notification.permission === 'granted'
                ? 'granted' : await Notification.requestPermission();
            if (permission !== 'granted') return { error: permission === 'denied' ? 'denied' : 'dismissed' };
            try {
                const subscription = describe(await subscribeWith(applicationServerKey));
                if (owner) store(offKey(owner), null);
                return subscription;
            } catch (error) {
                window.yap.diagnostics?.record('push.subscribe-failed', { reason: error?.name ?? 'unknown' });
                return { error: 'failed' };
            }
        },
        // Called once the server has accepted `endpoint` for `owner`, so repair can later tell a
        // subscription the push service reported gone from one another account registered.
        registered(owner, endpoint) {
            if (owner && endpoint) store(RECORD, JSON.stringify({ owner, endpoint }));
            publishPresence(undefined, true);
        },
        async disable(owner = account) {
            if (owner) store(offKey(owner), '1');
            store(RECORD, null);
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
