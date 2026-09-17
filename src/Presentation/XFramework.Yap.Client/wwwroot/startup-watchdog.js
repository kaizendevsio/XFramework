// The only thing that can speak for the window before .NET renders. App.razor owns every startup
// failure from its first render onwards - the recovery link, the "Device storage unavailable" page -
// but nothing owns the window before it: a runtime that aborts, or a cached loader asking for
// framework files this deployment no longer has, leaves the static splash spinning with no message
// and no way out. That is the worst failure this app has, because it looks exactly like progress.
(() => {
    const splash = document.querySelector('#app .startup-splash');
    const notice = document.getElementById('startup-notice'), text = document.getElementById('startup-notice-text');
    if (!splash || !notice || !text) return;
    // A first visit really does spend this long fetching ~15MB of runtime, so a slow start only
    // offers recovery and keeps waiting. A boot that has already failed says so immediately.
    const patience = 20000;
    let settled = false;
    const show = (message, reason) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        text.textContent = message;
        notice.hidden = false;
        window.yap?.diagnostics?.record('startup.stalled', { reason });
    };
    const timer = setTimeout(() => show('Yap is taking longer than usual to start.', 'slow'), patience);
    // Blazor replaces #app wholesale on its first render; from that moment App.razor is on screen
    // with its own recovery link, so this watchdog has nothing left to say.
    const observer = new MutationObserver(() => {
        if (splash.isConnected) return;
        settled = true;
        clearTimeout(timer);
        observer.disconnect();
    });
    observer.observe(document.getElementById('app'), { childList: true });
    const failed = () => show('Yap could not finish starting on this device.', 'error');
    // Capture phase, because a script that fails to load never bubbles. Resource errors from an
    // image or a stylesheet are cosmetic; a script or an uncaught exception is the app dying.
    addEventListener('error', event => {
        if (event.message || event.target?.tagName === 'SCRIPT') failed();
    }, true);
    addEventListener('unhandledrejection', failed);
})();
