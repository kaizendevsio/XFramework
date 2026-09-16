// Push and notification rendering, shared by every worker variant so development, published,
// classic and module builds cannot drift apart.
//
// Written to parse as both a classic script and an ES module - no import, no export, no top-level
// await - because the classic workers reach it through importScripts and the module worker through
// a static import, and there must only ever be one copy of this logic.
//
// The push payload carries routing identifiers only: message bodies are end-to-end encrypted and
// the server cannot read them. A module worker can decrypt on the device, so `preview` is an
// optional resolver it installs after loading. Without one - every browser too old for module
// workers, and every failure of the resolver - the banner stays exactly as generic as it was.
self.yapNotifications = (() => {
    const api = {
        preview: null,
        // A decrypt that is slow can never cost the notification, so the budget only decides how
        // long the worker stays alive hoping to improve a banner that is already on screen.
        budget: 5000,
        install(scope) {
            scope.addEventListener('push', event => event.waitUntil(push(scope, event)));
            scope.addEventListener('notificationclick', event => {
                event.notification.close();
                // Dismiss silences this device and nothing else: no request is sent, so the call keeps
                // ringing on the person's other devices and no participant is told anything. Declining for
                // real is an authenticated action that belongs to the app, not to a worker.
                if (event.action === 'dismiss') return;
                // Open call and a tap on the body are the same thing: surface the app and let it decide. The
                // worker never checks or joins a call itself - it holds no credentials, and the microphone
                // stays untouched until the person accepts in the app.
                const url = new URL(event.notification.data?.url || '/', scope.location).href;
                event.waitUntil(open(scope, url));
            });
        }
    };

    async function push(scope, event) {
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
        const options = {
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
        };
        // Foreground devices are filtered before sending. A delivered Web Push must remain
        // user-visible, including races where the app becomes visible while it is in transit.
        // This banner goes up before anything that can fail, so every path below is free to give up.
        await scope.registration.showNotification(ringing ? 'Incoming call' : stale ? 'Missed call' : 'New message', options);
        if (call || !thread || !api.preview) return;
        const content = await resolve(data);
        if (!content) return;
        // Same tag, so the OS replaces the banner in place rather than stacking a second one, and
        // silent because the device already buzzed for this message a moment ago.
        await scope.registration.showNotification(content.title, { ...options, body: content.body, renotify: false, silent: true });
    }

    // Every failure is the same failure: return nothing and leave the generic banner alone.
    async function resolve(data) {
        const controller = new AbortController();
        let timer;
        try {
            const work = Promise.resolve().then(() => api.preview(data, controller.signal));
            // The race abandons this promise when the budget wins; an abandoned rejection must
            // still be observed or it surfaces as an unhandled rejection in the worker.
            work.catch(() => {});
            return await Promise.race([work, new Promise(done => { timer = setTimeout(done, api.budget); })]) ?? null;
        } catch { return null; }
        finally { clearTimeout(timer); controller.abort(); }
    }

    async function open(scope, url) {
        const windows = await scope.clients.matchAll({ type: 'window', includeUncontrolled: true });
        // Reuse a window that is already open rather than starting a second copy of the app. The
        // page decides whether it actually needs to move, so opening the conversation already on
        // screen costs nothing. WindowClient.navigate is not used: it is unavailable for
        // uncontrolled clients and would force a full reload even when the app is already there.
        const mine = windows.find(client => new URL(client.url).origin === scope.location.origin);
        if (mine) {
            if (mine.focus) await mine.focus();
            mine.postMessage({ type: 'yap-notification-click', url });
            return;
        }
        await scope.clients.openWindow(url);
    }

    return api;
})();
