// One service worker registration for the whole app. Registering a different script URL at the
// same scope replaces the registration, so every caller - updates, push and the recovery page -
// has to ask through here or they would fight over which worker is installed.
(() => {
    let work = null;
    // A real feature test rather than a user-agent guess: a browser without module service workers
    // ignores the `type` member, loads the module file as a classic script, and register() rejects
    // on its first `import`. Module service workers are Chrome 91+, Safari 16.4+, Firefox 147+;
    // everything older keeps today's worker - offline shell, update prompt, generic push banner -
    // rather than being left with no service worker at all.
    //
    // A network failure rejects the same way and also falls back. That costs decrypted previews
    // until the next attempt, never the app, and the next call retries the module worker first.
    const attempt = async () => {
        // The top-level module must change with the published asset manifest too. This also
        // makes an explicit foreground check independent of imported-module update caching.
        let suffix = '';
        try {
            const response = await fetch('service-worker-assets.js', { cache: 'no-store', signal: AbortSignal.timeout(15000) });
            if (response.ok) {
                const match = (await response.text()).match(/"version"\s*:\s*"([^"\\]{1,128})"/);
                if (match) suffix = '?build=' + encodeURIComponent(match[1]);
            }
        } catch { /* An offline page keeps its current worker. */ }
        if (!suffix) {
            const existing = await navigator.serviceWorker.getRegistration();
            if (existing) return existing;
        }
        try { return await navigator.serviceWorker.register('service-worker.module.js' + suffix, { type: 'module', updateViaCache: 'none' }); }
        catch { return await navigator.serviceWorker.register('service-worker.js' + suffix, { updateViaCache: 'none' }); }
    };
    self.yapWorker = {
        register() {
            if (work) return work;
            let timer;
            work = Promise.race([attempt(), new Promise((_, reject) => {
                timer = setTimeout(() => reject(new Error('Worker registration timed out')), 20000);
            })]).finally(() => { clearTimeout(timer); work = null; });
            return work;
        },
        // Push reuses whatever is installed rather than racing the app's own registration.
        async existing() { return await navigator.serviceWorker.getRegistration() ?? await this.register(); }
    };
})();
