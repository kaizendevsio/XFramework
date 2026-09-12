// Check long-lived installed apps as well as ordinary page loads. Activation is
// explicit so a deployment cannot reload a recording or an attachment in progress.
(() => {
    let registration, checking, lastCheck = -Infinity, changed = false, applying = false;
    let reloading = false, blocked = false;
    const workers = new WeakSet();
    const busy = () => !!(document.querySelector('[data-update-busy="true"]') || window.yapRecording);
    const notice = () => {
        const element = document.getElementById('app-update');
        if (!element) return;
        blocked = blocked && busy();
        element.hidden = !(registration?.waiting || changed);
        element.querySelector('span').textContent = blocked
            ? 'Finish sending, or remove your attachment or recording, before updating.'
            : 'A new version of Yap is ready.';
    };
    const reload = () => { if (!reloading) { reloading = true; location.reload(); } };
    const observeWorker = () => {
        const worker = registration?.installing;
        if (worker && !workers.has(worker)) {
            workers.add(worker);
            worker.addEventListener('statechange', () => notice());
        }
        notice();
    };
    async function check() {
        notice();
        if (!('serviceWorker' in navigator) || !navigator.onLine || document.hidden || checking || Date.now() - lastCheck < 60000) return;
        lastCheck = Date.now();
        checking = (async () => {
            try {
                if (!registration) {
                    registration = await navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
                    registration.addEventListener('updatefound', observeWorker);
                    observeWorker(); // Registration may already have an installing worker.
                } else await registration.update();
                notice();
            } catch { /* Offline, an incomplete deployment or disabled workers: keep the current app. */ }
        })();
        try { await checking; } finally { checking = null; }
    }
    window.yap.updates = {
        check,
        notice,
        apply() {
            if (busy()) {
                blocked = true;
                notice();
                return;
            }
            if (changed) { reload(); return; }
            if (!registration?.waiting || applying) return;
            applying = true;
            registration.waiting.postMessage('activate');
        }
    };
    if ('serviceWorker' in navigator) {
        const controlled = !!navigator.serviceWorker.controller;
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            // A different tab can activate an update. Let this document save its
            // own draft through the Update app button before it reloads as well.
            if (applying) reload();
            else if (controlled) { changed = true; notice(); }
        });
        addEventListener('load', check);
        addEventListener('pageshow', check);
        addEventListener('online', check);
        document.addEventListener('visibilitychange', check);
        setInterval(check, 5 * 60000);
    }
})();
