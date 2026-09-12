// Opt-in, bounded breadcrumbs. Never capture request bodies, headers, queries,
// console arguments, message text, file names, or raw exception messages.
(() => {
    window.yap ??= {};
    const key = 'yap-diagnostics-v1', flag = 'yap-diagnostics-enabled';
    const get = key => { try { return localStorage.getItem(key); } catch { return null; } };
    let enabled = get(flag) === 'true', entries = [], version = 'unknown';
    try { entries = JSON.parse(get(key) || '[]').slice(-200); } catch {}
    version = entries.findLast(e => e.version)?.version || version;
    const tokens = new Set('api chat auth session conversations messages attachments uploads initialize thread-actions read settings login register logout events health ready parts members actions search deleted'.split(' '));
    for (const name of ['diagnostics.html', 'diagnostics.js', 'device.js', 'image-previews.js', 'photo-viewer.js', 'app.js', 'motion.js', 'updates.js', '_framework', 'blazor.webassembly.js', 'dotnet.native.js']) tokens.add(name);
    const route = value => {
        try { return new URL(value, location.origin).pathname.split('/').map(s => !s || tokens.has(s) ? s : ':id').join('/'); }
        catch { return '/unknown'; }
    };
    const types = /\b(?:[A-Za-z0-9_.]*(?:Exception|Error))\b/;
    const errorType = error => {
        const value = typeof error === 'string' ? error : error?.name || error?.constructor?.name || '';
        return value.match(types)?.[0]?.slice(0, 100) || 'UnknownError';
    };
    const save = () => {
        entries = entries.slice(-200);
        while (JSON.stringify(entries).length > 60000) entries.shift();
        try { localStorage.setItem(key, JSON.stringify(entries)); } catch {}
    };
    function record(event, details = {}) {
        if (!enabled) return;
        const data = {};
        for (const name of ['status', 'eventId', 'ms', 'bytes', 'width', 'height', 'active', 'pending', 'line', 'column', 'persisted', 'online', 'count'])
            if (typeof details[name] === 'number' || typeof details[name] === 'boolean') data[name] = details[name];
        if (details.route) data.route = route(details.route);
        if (details.type) data.type = errorType(details.type);
        if (Array.isArray(details.frames)) data.frames = details.frames.filter(f => typeof f === 'string' && /^[A-Za-z0-9_.+<>`-]{1,200}$/.test(f)).slice(0, 8);
        for (const name of ['phase', 'kind', 'reason', 'method', 'version'])
            if (typeof details[name] === 'string' && /^[a-zA-Z0-9._-]{1,40}$/.test(details[name])) data[name] = details[name];
        entries.push({ time: new Date().toISOString(), event: /^[a-z.-]{1,40}$/.test(event) ? event : 'event', ...data }); save();
    }
    const snapshot = () => ({ route: location.href, online: navigator.onLine, width: innerWidth, height: innerHeight,
        count: document.querySelectorAll('.message-media img').length });
    window.yap.diagnostics = {
        enabled: () => enabled,
        setEnabled(value) { enabled = value; try { localStorage.setItem(flag, String(value)); } catch {} if (value) record('logging.enabled', { ...snapshot(), version }); },
        record,
        error: (phase, error) => record('error', { phase, type: errorType(error) }),
        version(value) { version = value; record('app.ready', { version }); },
        clear() { entries = []; save(); },
        report() {
            record('snapshot', snapshot());
            return JSON.stringify({ app: 'Yap', version, browser: navigator.userAgent, enabled,
                note: 'Breadcrumbs only. A browser process crash may end without a JavaScript error. No message contents or credentials are recorded.', entries }, null, 2);
        },
        async copy() { const report = this.report(); try { await navigator.clipboard.writeText(report); return true; } catch { return false; } }
    };
    record('page.start', { ...snapshot(), kind: performance.getEntriesByType('navigation')[0]?.type || 'unknown' });
    addEventListener('pageshow', e => record('page.show', { ...snapshot(), persisted: e.persisted }));
    addEventListener('pagehide', e => record('page.hide', { persisted: e.persisted }));
    for (const event of ['online', 'offline']) addEventListener(event, () => record('network.' + event));
    document.addEventListener('visibilitychange', () => record('page.visibility', { kind: document.visibilityState }));
    addEventListener('error', e => record('error', { type: errorType(e.error || e.message), route: e.filename, line: e.lineno, column: e.colno }));
    addEventListener('unhandledrejection', e => record('promise.error', { type: errorType(e.reason) }));
    document.addEventListener('load', e => {
        if (e.target?.matches?.('.message-media img,dialog.photo-viewer img'))
            record('image.loaded', { width: e.target.naturalWidth, height: e.target.naturalHeight, count: document.querySelectorAll('.message-media img').length });
    }, true);
    const fetch = window.fetch.bind(window);
    window.fetch = async (...args) => {
        const path = args[0]?.url || String(args[0]);
        let api = false;
        try { const url = new URL(path, location.origin); api = url.origin === location.origin && url.pathname.startsWith('/api/'); } catch {}
        const start = performance.now();
        try {
            const response = await fetch(...args);
            if (api) record('request', { route: path, method: args[1]?.method || args[0]?.method || 'GET', status: response.status, ms: Math.round(performance.now() - start) });
            return response;
        } catch (error) {
            if (api) record('request.failed', { route: path, type: errorType(error), ms: Math.round(performance.now() - start) });
            throw error;
        }
    };
})();
