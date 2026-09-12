// Files stay in OPFS. SQLite owns conversations, drafts and upload receipts.
(() => {
    let listener, events, account, activeThread, installPrompt, databaseLock;
    const urls = new Set();
    // Attachments too large to copy into OPFS are held as live File handles instead.
    // They do not survive a reload, which is why they also require a connection.
    const pending = new Map();
    const directory = async () => (await navigator.storage.getDirectory()).getDirectoryHandle('yap-files', { create: true });
    const write = async (key, blob) => {
        const handle = await (await directory()).getFileHandle(key, { create: true });
        const stream = await handle.createWritable();
        try { await stream.write(blob); await stream.close(); }
        catch (error) { await stream.abort().catch(() => {}); throw error; }
    };
    const read = async key => pending.get(key) ?? (await (await directory()).getFileHandle(key)).getFile();
    const notify = () => listener?.invokeMethodAsync('ConnectivityChanged', navigator.onLine).catch(() => {});
    addEventListener('online', notify);
    addEventListener('offline', notify);
    addEventListener('pageshow', notify);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) notify(); });
    addEventListener('beforeinstallprompt', event => { event.preventDefault(); installPrompt = event; });
    window.yap.device = {
        acquireDatabase() {
            // Keep one document in charge of SQLite. A waiting navigation also evicts
            // a previous document from the back/forward cache before opening OPFS.
            return databaseLock ??= new Promise((resolve, reject) => {
                navigator.locks.request('yap-sqlite', { signal: AbortSignal.timeout(10000) }, async () => {
                    resolve();
                    // The browser releases this lock when the document is destroyed.
                    await new Promise(() => {});
                }).catch(reject);
            });
        },
        online: () => navigator.onLine,
        watch: dotnet => { listener = dotnet; },
        events(scope, thread) {
            if (account === scope && activeThread === thread && events) return;
            events?.close(); events = null; account = scope; activeThread = thread;
            if (!scope) return;
            events = new EventSource(`/api/chat/events?account=${encodeURIComponent(scope)}${thread ? `&thread=${thread}` : ''}`);
            events.onmessage = () => listener?.invokeMethodAsync('RefreshHint').catch(() => {});
            events.onopen = events.onmessage;
            events.addEventListener('typing', event => {
                if (account !== scope || activeThread !== thread) return;
                const state = JSON.parse(event.data);
                listener?.invokeMethodAsync('TypingChanged', state.ThreadId, state.CredentialId, state.IsTyping).catch(() => {});
            });
        },
        async pickFile(input, key, stageLimit, maxBytes) {
            const file = input.files[0];
            if (!file || file.size < 1) throw new Error('Choose a file to attach.');
            if (file.size > maxBytes) throw new Error(`Choose a file up to ${Math.round(maxBytes / 1073741824)} GB.`);
            // Staging copies the bytes so the message can be queued offline. Past the
            // threshold that would cost twice the file size on the device, so stream instead.
            const staged = file.size <= stageLimit;
            if (staged) await write(key, file); else pending.set(key, file);
            return { key, name: file.name, contentType: file.type || 'application/octet-stream', size: file.size, staged };
        },
        async removeFile(key) {
            pending.delete(key);
            await (await directory()).removeEntry(key).catch(error => { if (error.name !== 'NotFoundError') throw error; });
        },
        // Slices a held File straight into the resumable endpoints. One part is in flight
        // at a time, so peak memory is the part size rather than the file size.
        async uploadParts(key, uploadId, chunkSizeBytes, totalParts, token, scope, reporter) {
            const file = pending.get(key);
            if (!file) return 410;
            const headers = { RequestVerificationToken: token, 'X-Yap-Account': scope, 'Content-Type': 'application/octet-stream' };
            for (let part = 1; part <= totalParts; part++) {
                const offset = (part - 1) * chunkSizeBytes;
                const slice = file.slice(offset, Math.min(offset + chunkSizeBytes, file.size));
                const response = await fetch(`/api/chat/uploads/session/${uploadId}/parts/${part}?offset=${offset}`,
                    { method: 'POST', headers, body: slice });
                if (!response.ok) return response.status;
                await reporter?.invokeMethodAsync('UploadProgress',
                    Math.round((offset + slice.size) * 100 / file.size)).catch(() => {});
            }
            return 200;
        },
        async upload(key, name, thread, token, scope, contentType) {
            const file = await read(key);
            const data = new FormData(); data.append('file', new Blob([file], { type: contentType || 'application/octet-stream' }), name);
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
        async mediaUrl(key, path, scope, online, contentType, local) {
            if (!/^(image\/(png|jpeg|gif|webp|avif|heic|heif)|video\/(mp4|webm|quicktime)|audio\/(mp4|mpeg|ogg|webm|wav|x-wav|aac))$/i.test(contentType.split(';')[0])) return null;
            let file;
            try { file = await read(key); }
            catch (error) {
                if (local || error.name !== 'NotFoundError' || !online) throw error;
                const response = await fetch(path, { headers: { 'X-Yap-Account': scope }, cache: 'no-store' });
                if (!response.ok) throw new Error('The attachment is unavailable.');
                file = await response.blob(); await write(key, file);
            }
            const url = URL.createObjectURL(new Blob([file], { type: contentType })); urls.add(url); return url;
        },
        releaseMedia(url) { URL.revokeObjectURL(url); urls.delete(url); },
        async startRecording() {
            if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) throw new Error('Voice recording is unavailable in this browser.');
            if (window.yapRecording) throw new Error('A recording is already active.');
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            try {
                const mimeType = ['audio/mp4', 'audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus'].find(type => MediaRecorder.isTypeSupported(type));
                const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
                const state = { recorder, stream, chunks: [], size: 0, timer: null, finished: null };
                state.finished = new Promise((resolve, reject) => {
                    recorder.ondataavailable = event => { if (event.data.size) { state.chunks.push(event.data); state.size += event.data.size; } if (state.size > 19 * 1024 * 1024 && recorder.state !== 'inactive') recorder.stop(); };
                    recorder.onerror = () => reject(new Error('Recording failed.'));
                    recorder.onstop = () => { clearTimeout(state.timer); stream.getTracks().forEach(track => track.stop()); resolve(new Blob(state.chunks, { type: recorder.mimeType })); };
                });
                state.finished.catch(() => { stream.getTracks().forEach(track => track.stop()); });
                recorder.start(1000); state.timer = setTimeout(() => { if (recorder.state !== 'inactive') recorder.stop(); }, 300000);
                window.yapRecording = state;
            } catch (error) { stream.getTracks().forEach(track => track.stop()); throw error; }
        },
        async stopRecording(key, cancel, stageLimit) {
            const state = window.yapRecording; if (!state) return null;
            window.yapRecording = null;
            if (state.recorder.state !== 'inactive') state.recorder.stop();
            try {
                const blob = await state.finished;
                if (cancel) return null;
                if (!blob.size || blob.size > stageLimit) throw new Error('Recording is empty or too large.');
                await write(key, blob);
                const extension = blob.type.includes('mp4') ? 'm4a' : blob.type.includes('ogg') ? 'ogg' : 'webm';
                return { key, name: `Voice message.${extension}`, contentType: blob.type, size: blob.size };
            } finally { clearTimeout(state.timer); state.stream.getTracks().forEach(track => track.stop()); }
        },
        resizeComposer(input) {
            if (!input) return;
            const previous = input.getBoundingClientRect().height;
            input.style.height = 'auto';
            const height = Math.min(144, Math.max(44, input.scrollHeight));
            input.style.height = `${height}px`;
            if (Math.abs(height - previous) > 1 && !matchMedia('(prefers-reduced-motion: reduce)').matches)
                input.animate([{ height: `${previous}px` }, { height: `${height}px` }], { duration: 180, easing: 'ease-out' });
        },
        async clearFiles() {
            for (const url of urls) URL.revokeObjectURL(url); urls.clear();
            pending.clear();
            await (await navigator.storage.getDirectory()).removeEntry('yap-files', { recursive: true }).catch(error => { if (error.name !== 'NotFoundError') throw error; });
        },
        persist: async () => navigator.storage.persist ? await navigator.storage.persist() : false,
        canInstall: () => installPrompt !== undefined && installPrompt !== null,
        isInstalled: () => matchMedia('(display-mode: standalone)').matches || navigator.standalone === true,
        async install() { if (installPrompt) { await installPrompt.prompt(); installPrompt = null; } },
        update() { window.yap.updates.apply(); },
        checkUpdate() { window.yap.updates.notice(); }
    };

    // The visual viewport excludes the on-screen keyboard on iOS and Android.
    // Keep the app inside it; only the message body scrolls under the glass chrome.
    let frame;
    let fullHeight = window.innerHeight;
    const viewport = () => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(() => {
            const view = window.visualViewport;
            const editing = document.activeElement?.matches('input,textarea,[contenteditable=true]');
            if (!editing) fullHeight = window.innerHeight;
            const keyboard = editing && view && fullHeight - view.height > 120;
            const root = document.documentElement;
            root.dataset.keyboard = keyboard ? 'open' : 'closed';
            // Standalone Safari can report a smaller visual viewport even without
            // a keyboard. Let CSS fill the display until an editor opens the keyboard.
            if (keyboard) {
                root.style.setProperty('--viewport-height', `${view.height}px`);
                root.style.setProperty('--viewport-top', `${view.offsetTop}px`);
            } else {
                root.style.setProperty('--viewport-height', `${window.innerHeight}px`);
                root.style.removeProperty('--viewport-top');
            }
        });
    };
    visualViewport?.addEventListener('resize', viewport);
    visualViewport?.addEventListener('scroll', viewport);
    addEventListener('resize', viewport); viewport();
    document.addEventListener('focusin', viewport);
    document.addEventListener('focusout', () => { viewport(); setTimeout(viewport, 350); });
    addEventListener('pageshow', viewport);
    addEventListener('orientationchange', () => setTimeout(viewport, 350));
    document.addEventListener('contextmenu', event => { if (event.target.closest('button,a,label,nav,.avatar') && !event.target.closest('.bubble-content,input,textarea')) event.preventDefault(); });
    for (const name of ['gesturestart', 'gesturechange', 'gestureend']) document.addEventListener(name, event => event.preventDefault(), { passive: false });
    document.addEventListener('touchmove', event => { if (event.touches.length > 1) event.preventDefault(); }, { passive: false });

})();
