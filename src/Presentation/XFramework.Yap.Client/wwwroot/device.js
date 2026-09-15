// Files stay in OPFS. SQLite owns conversations, drafts and upload receipts.
(() => {
    let listener, events, account, activeThread, installPrompt, databaseLock;
    let reconnectTimer, reconnectDelay = 1000;
    const urls = new Set();
    // Attachments too large to copy into OPFS are held as live File handles instead.
    // They do not survive a reload, which is why they also require a connection.
    const pending = new Map();
    const previews = new Map();
    const encryptedDownloads = new Map();
    const activeTemporaryFiles = new Set();
    const emptyGuid = '00000000-0000-0000-0000-000000000000';
    const maximumPlaintextBytes = 4 * 1024 * 1024 * 1024;
    const maximumCiphertextBytes = maximumPlaintextBytes + 16 * 1024 * 1024;
    let startupCleanup;
    const directory = async () => (await navigator.storage.getDirectory()).getDirectoryHandle('yap-files', { create: true });
    const write = async (key, blob) => {
        const handle = await (await directory()).getFileHandle(key, { create: true });
        const stream = await handle.createWritable();
        try { await stream.write(blob); await stream.close(); }
        catch (error) { await stream.abort().catch(() => {}); throw error; }
    };
    const read = async key => pending.get(key) ?? (await (await directory()).getFileHandle(key)).getFile();
    // WebKit's file sink can write a view's entire backing ArrayBuffer. OpenPGP
    // emits subarrays, so snapshot exactly the selected bytes before handing them
    // to OPFS. Keep stream backpressure: only one chunk is copied at a time.
    const pipeToFile = (source, sink) => source.pipeTo(new WritableStream({
        write: chunk => sink.write(new Blob([chunk])),
        close: () => sink.close(),
        abort: reason => sink.abort(reason)
    }));
    const canonical = value => JSON.stringify((function sorted(v) {
        if (Array.isArray(v)) return v.map(sorted);
        if (v && typeof v === 'object') return Object.fromEntries(Object.keys(v).sort().map(k => [k, sorted(v[k])]));
        return v;
    })(value));
    const verificationContext = async (key, scope, context) => {
        if (!/^[0-9a-f]{32}:[0-9a-f]{32}$/i.test(scope) || !key.startsWith(`${scope.replace(':', '-')}-`)
            || context.tenantId.replaceAll('-', '').toLowerCase() !== scope.split(':')[0].toLowerCase()) throw new Error('Attachment account mismatch.');
        const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(canonical(context))));
        // Version 1 could mark a file verified before WebKit expanded a subarray
        // during storage. Re-download those caches using the exact-byte writer.
        return { version: 2, scope, context: Array.from(digest, n => n.toString(16).padStart(2, '0')).join('') };
    };
    const hasVerifiedFile = async (key, scope, context) => {
        const expected = await verificationContext(key, scope, context);
        try {
            const marker = JSON.parse(await (await read(`${key}.ready`)).text());
            const file = await read(key);
            return marker.version === expected.version && marker.scope === expected.scope && marker.context === expected.context
                && Number.isSafeInteger(marker.size) && marker.size >= 0 && marker.size === file.size;
        } catch (error) { if (error.name === 'NotFoundError' || error instanceof SyntaxError) return false; throw error; }
    };
    const cleanupUnverified = async () => {
        const dir = await directory();
        for await (const [name, handle] of dir.entries()) {
            if (handle.kind === 'file' && (name.endsWith('.unverified') || name.endsWith('.encrypted')) && !activeTemporaryFiles.has(name)) await dir.removeEntry(name).catch(() => {});
        }
    };
    const notify = () => listener?.invokeMethodAsync('ConnectivityChanged', navigator.onLine).catch(() => {});
    addEventListener('online', notify);
    addEventListener('offline', notify);
    addEventListener('pageshow', notify);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) notify(); });
    addEventListener('beforeinstallprompt', event => { event.preventDefault(); installPrompt = event; });
    window.yap.device = {
        visible: () => !document.hidden,
        async uploadPhoto(input, path, scope, token) {
            const file = input.files?.[0];
            if (!file || file.size > 20 * 1024 * 1024) throw new Error('Choose a photo up to 20 MB.');
            const jpeg = await yap.imagePreviews.jpeg(file, yap.imagePreviews.isHeif(file.type, file.name));
            const bitmap = await createImageBitmap(jpeg);
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = 512;
            let bytes;
            try {
                const size = Math.min(bitmap.width, bitmap.height);
                canvas.getContext('2d').drawImage(bitmap, (bitmap.width - size) / 2, (bitmap.height - size) / 2, size, size, 0, 0, 512, 512);
                bytes = canvas.toDataURL('image/jpeg', .86).split(',')[1];
            } finally { bitmap.close(); canvas.width = canvas.height = 1; }
            const response = await fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Yap-Account': scope, 'RequestVerificationToken': token }, body: JSON.stringify({ bytes }) });
            input.value = '';
            return response.status;
        },
        acquireDatabase() {
            // Keep one document in charge of SQLite. A busy owner is a normal
            // waiting state, not a storage failure. Only destruction releases it:
            // releasing on pagehide would leave a live SQLite worker using OPFS.
            return databaseLock ??= new Promise((resolve, reject) => {
                navigator.locks.request('yap-sqlite', { signal: AbortSignal.timeout(10000) }, async () => {
                    window.yap.diagnostics?.record('storage.lock-acquired');
                    resolve(true);
                    // The browser releases this lock when the document is destroyed.
                    await new Promise(() => {});
                }).catch(error => {
                    databaseLock = undefined;
                    if (error.name === 'TimeoutError') {
                        window.yap.diagnostics?.record('storage.lock-waiting');
                        resolve(false);
                    } else {
                        window.yap.diagnostics?.record('storage.lock-failed', { type: error.name });
                        reject(error);
                    }
                });
            });
        },
        online: () => navigator.onLine,
        visibleMessages(thread, parent) {
            const route = parent ? `/thread/${thread}/${parent}` : `/chat/${thread}`;
            if (document.hidden || location.pathname.toLowerCase() !== route.toLowerCase()) return [];
            const list = document.querySelector('[data-messages]');
            if (!list) return [];
            const box = list.getBoundingClientRect(), style = getComputedStyle(list);
            const top = box.top + (parseFloat(style.paddingTop) || 0), bottom = box.bottom - (parseFloat(style.paddingBottom) || 0);
            return [...list.querySelectorAll('[data-window-row]')].filter(row => {
                const rect = row.getBoundingClientRect(); return rect.bottom > top && rect.top < bottom;
            }).map(row => row.dataset.windowRow);
        },
        watch(dotnet) {
            listener = dotnet;
            // Old temporary files are housekeeping, not a prerequisite for opening chats.
            startupCleanup ??= cleanupUnverified().catch(error =>
                window.yap.diagnostics?.record('storage.cleanup-failed', { name: error.name }));
        },
        events(scope, thread) {
            if (account === scope && activeThread === thread && events && events.readyState !== EventSource.CLOSED) return;
            clearTimeout(reconnectTimer); reconnectTimer = null;
            if (account !== scope || activeThread !== thread) reconnectDelay = 1000;
            events?.close(); events = null; account = scope; activeThread = thread;
            if (!scope) return;
            const source = events = new EventSource(`/api/chat/events?account=${encodeURIComponent(scope)}${thread ? `&thread=${thread}` : ''}`);
            events.onmessage = event => {
                if (account !== scope || activeThread !== thread) return;
                listener?.invokeMethodAsync('ChatEvent', scope, event.data).catch(() => {});
            };
            events.onopen = () => {
                if (source !== events) return;
                reconnectDelay = 1000;
                if (account === scope && activeThread === thread) listener?.invokeMethodAsync('RefreshHint').catch(() => {});
            };
            // Browsers retry interrupted streams, but a failed HTTP reconnect can
            // leave EventSource permanently CLOSED. Recover without a page reload.
            events.onerror = () => {
                if (source !== events || source.readyState !== EventSource.CLOSED || reconnectTimer) return;
                reconnectTimer = setTimeout(() => {
                    reconnectTimer = null;
                    if (source !== events) return;
                    if (!navigator.onLine || document.hidden) { source.onerror(); return; }
                    window.yap.device.events(scope, thread);
                }, reconnectDelay);
                reconnectDelay = Math.min(30000, reconnectDelay * 2);
            };
            events.addEventListener('call', event => listener?.invokeMethodAsync('VoiceEvent', event.data).catch(() => {}));
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
            return { key, name: file.name, contentType: await yap.imagePreviews.contentType(file), size: file.size, staged };
        },
        async removeFile(key) {
            pending.delete(key);
            previews.delete(key);
            await (await directory()).removeEntry(key).catch(error => { if (error.name !== 'NotFoundError') throw error; });
            await (await directory()).removeEntry(`${key}.preview-v1.jpg`).catch(error => { if (error.name !== 'NotFoundError') throw error; });
            await (await directory()).removeEntry(`${key}.preview-v2.jpg`).catch(error => { if (error.name !== 'NotFoundError') throw error; });
            await (await directory()).removeEntry(`${key}.ready`).catch(error => { if (error.name !== 'NotFoundError') throw error; });
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
        async uploadEncrypted(key, thread, token, scope, context, recipients, voice = false) {
            let source;
            try { source = await read(key); }
            catch (error) { if (error.name === 'NotFoundError') return { status: 410, id: emptyGuid }; throw error; }
            if (source.size <= 0 || source.size > maximumPlaintextBytes) return { status: 413, id: emptyGuid };
            const encryptedKey = `${key}.${crypto.randomUUID()}.encrypted`;
            activeTemporaryFiles.add(encryptedKey);
            const handle = await (await directory()).getFileHandle(encryptedKey, { create: true });
            const sink = await handle.createWritable();
            try {
                const encrypted = await yap.encryption.encryptAttachment(scope, context, source.stream(), recipients);
                const ciphertext = encrypted.stream;
                await pipeToFile(ciphertext, sink);
                const file = await handle.getFile();
                if (file.size > maximumCiphertextBytes) return { status: 413, id: emptyGuid };
                // Only the voice/attachment category is public, for conversation feature controls.
                // Original names and formats remain inside the signed message.
                const headers = { RequestVerificationToken: token, 'X-Yap-Account': scope, 'Content-Type': 'application/json' };
                const started = await fetch(`/api/chat/uploads/${thread}/session`, { method: 'POST', headers,
                    body: JSON.stringify({ fileName: voice ? 'voice.pgp' : 'attachment.pgp', contentType: 'application/octet-stream', totalBytes: file.size }) });
                if (!started.ok) return { status: started.status, id: emptyGuid };
                const ticket = await started.json();
                try {
                    for (let part = 1; part <= ticket.totalParts; part++) {
                        const offset = (part - 1) * ticket.chunkSizeBytes;
                        const response = await fetch(`/api/chat/uploads/session/${ticket.uploadId}/parts/${part}?offset=${offset}`, {
                            method: 'POST', headers: { ...headers, 'Content-Type': 'application/octet-stream' }, body: file.slice(offset, offset + ticket.chunkSizeBytes) });
                        if (!response.ok) throw Object.assign(new Error('Encrypted upload failed.'), { status: response.status });
                    }
                    const complete = await fetch(`/api/chat/uploads/session/${ticket.uploadId}/complete`, { method: 'POST', headers });
                    if (!complete.ok) throw Object.assign(new Error('Encrypted upload did not finish.'), { status: complete.status });
                    return { status: complete.status, id: (await complete.json()).id, key: encrypted.key };
                } catch (error) {
                    await fetch(`/api/chat/uploads/session/${ticket.uploadId}/abort`, { method: 'POST', headers }).catch(() => {});
                    if (error.status) return { status: error.status, id: emptyGuid };
                    throw error;
                }
            } catch (error) { await sink.abort().catch(() => {}); throw error; }
            finally { activeTemporaryFiles.delete(encryptedKey); await (await directory()).removeEntry(encryptedKey).catch(() => {}); }
        },
        hasVerifiedFile,
        async decryptFile(key, path, scope, online, context, senderDirectory, attachmentKey = null) {
            const expected = await verificationContext(key, scope, context);
            if (await hasVerifiedFile(key, scope, context)) return;
            const operationKey = `${key}:${expected.context}`;
            if (encryptedDownloads.has(operationKey)) return encryptedDownloads.get(operationKey);
            let phase = 'attachment-fetch', status;
            const operation = (async () => {
            if (!online) throw new Error('Connect to download this encrypted attachment.');
            const response = await fetch(path, { headers: { 'X-Yap-Account': scope }, cache: 'no-store' });
            status = response.status;
            if (!response.ok || !response.body) throw new Error('Encrypted attachment unavailable.');
            phase = 'attachment-store';
            const temporary = `${key}.${crypto.randomUUID()}.unverified`, dir = await directory();
            activeTemporaryFiles.add(temporary);
            await dir.removeEntry(`${key}.ready`).catch(error => { if (error.name !== 'NotFoundError') throw error; });
            const handle = await dir.getFileHandle(temporary, { create: true }), sink = await handle.createWritable();
            try {
                phase = 'attachment-decrypt';
                await yap.encryption.decryptStream(scope, context, response.body, senderDirectory, {
                    async write(chunk) {
                        phase = 'attachment-store';
                        await sink.write(new Blob([chunk]));
                        phase = 'attachment-decrypt';
                    },
                    async commit() {
                        phase = 'attachment-commit';
                        await sink.close();
                        // OPFS streams copy without materializing the whole attachment in JS memory.
                        const final = await (await dir.getFileHandle(key, { create: true })).createWritable();
                        try {
                            const verified = await handle.getFile();
                            await pipeToFile(verified.stream(), final);
                            // The marker is written after the final writable closes. A crash
                            // during either copy leaves no valid marker and forces a retry.
                            await write(`${key}.ready`, new Blob([JSON.stringify({ ...expected, size: verified.size })]));
                        }
                        catch (error) { await final.abort().catch(() => {}); await dir.removeEntry(key).catch(() => {}); throw error; }
                    },
                    abort: () => sink.abort().catch(() => {})
                }, attachmentKey);
            } finally { activeTemporaryFiles.delete(temporary); await dir.removeEntry(temporary).catch(() => {}); }
            })();
            encryptedDownloads.set(operationKey, operation);
            try { return await operation; }
            catch (error) {
                window.yap.diagnostics?.record('attachment.failed', { phase, status, type: error.name });
                throw error;
            }
            finally { encryptedDownloads.delete(operationKey); }
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
        async mediaUrl(key, path, scope, online, contentType, local, name = '') {
            const heif = yap.imagePreviews.isHeif(contentType, name);
            if (!heif && !/^(image\/(png|jpeg|gif|webp|avif)|video\/(mp4|webm|quicktime)|audio\/(mp4|mpeg|ogg|webm|wav|x-wav|aac))$/i.test(contentType.split(';')[0])) return null;
            // Let the browser range-stream received videos. Reading a multi-GB video
            // into a Blob before showing a player exhausts mobile browser memory.
            if (!local && online && contentType.startsWith('video/')) {
                const stream = new URL(path, document.baseURI);
                stream.searchParams.set('account', scope);
                stream.searchParams.set('mediaType', contentType);
                return stream.pathname + stream.search;
            }
            let file;
            try { file = await read(key); }
            catch (error) {
                if (local || error.name !== 'NotFoundError' || !online) throw error;
                const response = await fetch(path, { headers: { 'X-Yap-Account': scope }, cache: 'no-store' });
                if (!response.ok) throw new Error('The attachment is unavailable.');
                file = await response.blob(); await write(key, file);
            }
            if (heif || /^image\/jpeg$/i.test(contentType.split(';')[0])) {
                const previewKey = `${key}.preview-v2.jpg`;
                let preview = previews.get(key);
                if (!preview) {
                    preview = (async () => {
                        try { return await read(previewKey); }
                        catch (error) { if (error.name !== 'NotFoundError') throw error; }
                        const jpeg = await yap.imagePreviews.jpeg(file, heif);
                        // A full device must still be able to show the decoded image.
                        if (previews.get(key) === preview) await write(previewKey, jpeg).catch(() => {});
                        return jpeg;
                    })();
                    previews.set(key, preview);
                }
                try { file = await preview; contentType = 'image/jpeg'; }
                finally { if (previews.get(key) === preview) previews.delete(key); }
            }
            const url = URL.createObjectURL(new Blob([file], { type: contentType })); urls.add(url);
            window.yap.diagnostics?.record('media.open', { active: urls.size, bytes: file.size }); return url;
        },
        releaseMedia(url) { URL.revokeObjectURL(url); urls.delete(url); window.yap.diagnostics?.record('media.release', { active: urls.size }); },
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
            pending.clear(); previews.clear();
            await (await navigator.storage.getDirectory()).removeEntry('yap-files', { recursive: true }).catch(error => { if (error.name !== 'NotFoundError') throw error; });
        },
        async clearAccountFiles(scope, keys) {
            if (!/^[0-9a-f]{32}:[0-9a-f]{32}$/i.test(scope)) throw new Error('Invalid account scope.');
            const prefix = `${scope.replace(':', '-')}-`, owned = new Set(keys), dir = await directory();
            for (const key of owned) { pending.delete(key); previews.delete(key); }
            for await (const [name] of dir.entries())
                if (name.startsWith(prefix) || [...owned].some(key => name === key || name.startsWith(`${key}.`))) await dir.removeEntry(name);
        },
        persist: async () => navigator.storage.persist ? await navigator.storage.persist() : false,
        canInstall: () => installPrompt !== undefined && installPrompt !== null,
        isInstalled: () => matchMedia('(display-mode: standalone)').matches || navigator.standalone === true,
        async install() { if (installPrompt) { await installPrompt.prompt(); installPrompt = null; } },
        update() { window.yap.updates.apply(); },
        checkUpdate() { window.yap.updates.notice(); }
    };

    const resumeEvents = () => {
        if (account && navigator.onLine && !document.hidden && events?.readyState === EventSource.CLOSED)
            window.yap.device.events(account, activeThread);
    };
    addEventListener('online', resumeEvents);
    document.addEventListener('visibilitychange', resumeEvents);

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
