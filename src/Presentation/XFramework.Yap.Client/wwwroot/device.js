// Files stay in OPFS. SQLite owns conversations, drafts and upload receipts.
(() => {
    let listener, events, account, installPrompt, registration;
    const urls = new Set();
    const directory = async () => (await navigator.storage.getDirectory()).getDirectoryHandle('yap-files', { create: true });
    const write = async (key, blob) => {
        const handle = await (await directory()).getFileHandle(key, { create: true });
        const stream = await handle.createWritable();
        try { await stream.write(blob); await stream.close(); }
        catch (error) { await stream.abort().catch(() => {}); throw error; }
    };
    const read = async key => (await (await directory()).getFileHandle(key)).getFile();
    const notify = () => listener?.invokeMethodAsync('ConnectivityChanged', navigator.onLine).catch(() => {});
    addEventListener('online', notify);
    addEventListener('offline', notify);
    addEventListener('pageshow', notify);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) notify(); });
    addEventListener('beforeinstallprompt', event => { event.preventDefault(); installPrompt = event; });
    window.yap.device = {
        online: () => navigator.onLine,
        watch: dotnet => { listener = dotnet; },
        events(scope) {
            if (account === scope && events) return;
            events?.close(); events = null; account = scope;
            if (!scope) return;
            events = new EventSource(`/api/chat/events?account=${encodeURIComponent(scope)}`);
            events.onmessage = () => listener?.invokeMethodAsync('RefreshHint').catch(() => {});
        },
        async pickFile(input, key) {
            const file = input.files[0];
            if (!file || file.size < 1 || file.size > 20 * 1024 * 1024) throw new Error('Choose a file up to 20 MB.');
            await write(key, file);
            return { key, name: file.name, contentType: file.type || 'application/octet-stream', size: file.size };
        },
        async removeFile(key) { await (await directory()).removeEntry(key).catch(error => { if (error.name !== 'NotFoundError') throw error; }); },
        async upload(key, name, thread, token, scope) {
            const file = await read(key);
            const data = new FormData(); data.append('file', file, name);
            const response = await fetch(`/api/chat/uploads/${thread}`, { method: 'POST', body: data,
                headers: { RequestVerificationToken: token, 'X-Yap-Account': scope } });
            return { status: response.status, id: response.ok ? (await response.json()).id : '00000000-0000-0000-0000-000000000000' };
        },
        async openFile(key, name, path, scope, online) {
            let file;
            try { file = await read(key); }
            catch (error) {
                if (error.name !== 'NotFoundError' || !online) throw error;
                const response = await fetch(path, { headers: { 'X-Yap-Account': scope }, cache: 'no-store' });
                if (!response.ok) throw new Error('The attachment is unavailable.');
                file = await response.blob();
                await write(key, file);
            }
            const url = URL.createObjectURL(file); urls.add(url);
            // Download untrusted files rather than execute HTML/SVG inside this origin.
            const link = document.createElement('a'); link.href = url; link.download = name; link.click();
            setTimeout(() => { URL.revokeObjectURL(url); urls.delete(url); }, 60000);
        },
        async clearFiles() {
            for (const url of urls) URL.revokeObjectURL(url); urls.clear();
            await (await navigator.storage.getDirectory()).removeEntry('yap-files', { recursive: true }).catch(error => { if (error.name !== 'NotFoundError') throw error; });
        },
        persist: async () => navigator.storage.persist ? await navigator.storage.persist() : false,
        async install() { if (installPrompt) { await installPrompt.prompt(); installPrompt = null; } },
        update() { registration?.waiting?.postMessage('activate'); },
        checkUpdate() { const notice = document.getElementById('app-update'); if (notice && registration?.waiting) notice.hidden = false; }
    };

    // The visual viewport excludes the on-screen keyboard on iOS and Android.
    // Keep the app inside it; only the message body scrolls under the glass chrome.
    let frame;
    const viewport = () => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(() => {
            const view = window.visualViewport;
            document.documentElement.style.setProperty('--viewport-height', `${view?.height ?? innerHeight}px`);
            document.documentElement.style.setProperty('--viewport-top', `${view?.offsetTop ?? 0}px`);
        });
    };
    visualViewport?.addEventListener('resize', viewport);
    visualViewport?.addEventListener('scroll', viewport);
    addEventListener('resize', viewport); viewport();
    for (const name of ['gesturestart', 'gesturechange', 'gestureend']) document.addEventListener(name, event => event.preventDefault(), { passive: false });
    document.addEventListener('touchmove', event => { if (event.touches.length > 1) event.preventDefault(); }, { passive: false });

    if ('serviceWorker' in navigator) {
        let reloading = false;
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (reloading) return; reloading = true; location.reload();
        });
        const showUpdate = () => { const notice = document.getElementById('app-update'); if (notice) notice.hidden = false; };
        addEventListener('load', async () => {
            try {
                registration = await navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
                if (registration.waiting) showUpdate();
                registration.addEventListener('updatefound', () => {
                    const worker = registration.installing;
                    worker?.addEventListener('statechange', () => { if (worker.state === 'installed' && navigator.serviceWorker.controller) showUpdate(); });
                });
            } catch { /* Browser settings may disable installation; the app still loads online. */ }
        });
    }
})();
