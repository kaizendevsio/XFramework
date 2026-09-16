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
    const ICON = 'yap-app-v2-192.png';
    const api = {
        preview: null,
        // Nothing is on screen while this runs any more, so the budget is a delay the person feels
        // rather than spare time spent improving a banner they are already looking at. Two
        // same-origin GETs and one OpenPGP decrypt cost a few hundred milliseconds warm; 2s covers
        // a radio waking from idle, stays far inside the push handler's allowance, and is about the
        // point past which a banner stops reading as "someone just messaged me". Overrunning it
        // delays nothing further - the generic banner goes up the instant the budget expires.
        budget: 2000,
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

    // The floor. Showing nothing at all is the one outcome a userVisibleOnly subscription cannot
    // survive - the browser puts up "this site has been updated in the background" and, repeatedly,
    // revokes the origin's push permission - so this exists to be shown when everything else fails.
    const generic = () => ({
        title: 'New message',
        options: { body: 'Open Yap to read it.', icon: ICON, badge: ICON, tag: 'yap-inbox', renotify: true, silent: false, actions: [], data: { kind: 'message', url: '/' } }
    });

    // Exactly one showNotification per push, on every path. The decrypt only ever resolves a
    // descriptor; the single call renders it, is the last statement, has no early return above it,
    // and is guarded so that a bug in the code below costs a good banner rather than the permission.
    //
    // One call rather than two because iOS does not honour tag replacement the way Android does: it
    // treats the replacement as a new notification, so #527's show-then-replace left every message
    // stacked twice in Notification Center.
    async function push(scope, event) {
        let banner = generic();
        try { banner = await describe(event) ?? banner; } catch { /* Deliberately swallowed: see above. */ }
        await scope.registration.showNotification(banner.title, banner.options);
    }

    async function describe(event) {
        let data = {};
        try { data = event.data ? event.data.json() : {}; } catch { /* A wake-up with no readable payload still notifies. */ }
        const call = data.kind === 'call';
        const thread = typeof data.threadId === 'string' ? data.threadId : null;
        // Already in the payload and still routing-only: the inbox item id, one per message per
        // recipient. It is fixed when the delivery job is queued, so a redelivery of the same push
        // replaces its own banner instead of stacking a duplicate of it.
        const message = typeof data.notificationId === 'string' ? data.notificationId : null;
        // A push service may hold a ring until the last moment of its TTL, so a call push can arrive
        // after the invite it describes has timed out. Ringing then would offer a call the
        // authenticated flow is going to refuse, so a late ring reports the missed call instead.
        const stale = call && typeof data.expiresAt === 'number' && data.expiresAt * 1000 <= Date.now();
        const ringing = call && !stale;
        const options = {
            body: ringing ? 'Tap to answer in Yap.' : stale ? 'The call ended before this device could ring.' : 'Open Yap to read it.',
            icon: ICON, badge: ICON,
            // Calls collapse on the call: a second ring, or a late one, has to replace the first
            // rather than stack under it. Messages deliberately do not - three messages are three
            // banners, the way every other messenger behaves. Only a payload carrying no message
            // id falls back to one banner per conversation.
            tag: call ? `yap-call-${data.reference ?? ''}` : message ? `yap-msg-${message}` : `yap-thread-${thread ?? 'inbox'}`,
            renotify: !stale, requireInteraction: ringing, silent: false,
            // A short double buzz, distinct from the single buzz of a message. Requested, not
            // promised: the option is ignored wherever the Vibration API is absent, iOS included.
            vibrate: ringing ? [200, 100, 200] : undefined,
            // Also advisory - a browser that renders no action buttons (iOS again) still delivers the
            // banner, and tapping its body does exactly what Open call does, so nothing is lost.
            actions: ringing ? [{ action: 'open', title: 'Open call' }, { action: 'dismiss', title: 'Dismiss' }] : [],
            // A per-message banner still deep links to the conversation: there is nowhere better to
            // land, and the app opens on the newest message anyway.
            data: { kind: ringing ? 'call' : stale ? 'missed' : 'message', url: thread ? `/chat/${thread}` : '/' }
        };
        const title = ringing ? 'Incoming call' : stale ? 'Missed call' : 'New message';
        // A ring must never wait on a decrypt, and an inbox push has no conversation to read.
        if (call || !thread || !api.preview) return { title, options };
        const content = await resolve(data);
        return content ? { title: content.title, options: { ...options, body: content.body } } : { title, options };
    }

    // Every failure is the same failure: return nothing, and the generic descriptor is what shows.
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
