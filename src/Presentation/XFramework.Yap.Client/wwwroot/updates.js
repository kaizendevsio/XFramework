// Check long-lived installed apps as well as ordinary page loads. Activation is
// explicit so a deployment cannot reload a recording or an attachment in progress.
(() => {
    let registration, checking, lastCheck = -Infinity, changed = false, applying = false;
    let reloading = false, blocked = false, dismissed = null, installed = null;
    const workers = new WeakSet();
    const candidate = () => registration?.waiting || (installed?.state === 'installed' ? installed : null);
    const available = () => candidate() || (changed ? navigator.serviceWorker.controller : null);
    const record = (phase, type) => window.yap.diagnostics?.record('update.state', { phase, type });
    const busy = () => !!(document.querySelector('[data-update-busy="true"]') || window.yapRecording);
    const notice = () => {
        const element = document.getElementById('app-update');
        if (!element) return;
        blocked = blocked && busy();
        const update = available();
        element.hidden = !update || dismissed === update;
        element.querySelector('.toast-text').textContent = blocked
            ? 'Finish sending, or remove your attachment or recording, before updating.'
            : 'A new version of Yap is ready.';
    };
    const reload = () => { if (!reloading) { reloading = true; window.yap.diagnostics?.record('page.reload-requested', { reason: 'app-update' }); location.reload(); } };
    const observeWorker = () => {
        const worker = registration?.installing;
        if (worker && !workers.has(worker)) {
            workers.add(worker);
            worker.addEventListener('statechange', () => {
                if (worker.state === 'installed' && (navigator.serviceWorker.controller || registration.active)) installed = worker;
                if (worker.state === 'redundant') lastCheck = -Infinity;
                record(worker.state);
                notice();
            });
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
                    registration = await self.yapWorker.register();
                    registration.addEventListener('updatefound', observeWorker);
                    observeWorker(); // Registration may already have an installing worker.
                }
                // Also check when another feature registered this worker earlier in this page.
                if (!registration.installing) await registration.update();
                notice();
            } catch (error) {
                record('check-failed', error?.name || 'Error');
                // Keep the current shell and account data. The next foreground/timer retries.
            }
        })();
        try { await checking; } finally { checking = null; }
    }
    window.yap.updates = {
        check,
        dismiss() { dismissed = available(); notice(); },
        notice,
        apply() {
            if (busy()) {
                blocked = true;
                notice();
                return;
            }
            if (changed) { reload(); return; }
            const worker = candidate();
            if (!worker || applying) return;
            applying = true;
            worker.postMessage('activate');
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
        setInterval(check, 60000);
        // Deferred scripts normally run before load, but restored/dynamically loaded pages may not.
        if (document.readyState === 'complete') void check();
    }
})();
