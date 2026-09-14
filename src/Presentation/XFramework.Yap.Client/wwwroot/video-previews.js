// Decode one preview at a time, only for mounted messages. Encrypted clips stay
// on-device: their source is the already decrypted, account-scoped media URL.
const states = new WeakMap();
let queue = Promise.resolve();

function event(target, name, signal) {
    return new Promise((resolve, reject) => {
        const finish = error => {
            target.removeEventListener(name, ready);
            target.removeEventListener('error', failed);
            signal.removeEventListener('abort', aborted);
            error ? reject(error) : resolve();
        };
        const ready = () => finish();
        const failed = () => finish(new Error('Video preview unavailable'));
        const aborted = () => finish(new DOMException('Preview cancelled', 'AbortError'));
        if (signal.aborted) { aborted(); return; }
        target.addEventListener(name, ready, { once: true });
        target.addEventListener('error', failed, { once: true });
        signal.addEventListener('abort', aborted, { once: true });
    });
}

export function release(video) {
    const state = states.get(video);
    if (!state) return;
    state.controller.abort();
    video.removeEventListener('play', state.play);
    if (state.poster) URL.revokeObjectURL(state.poster);
    video.removeAttribute('poster');
    states.delete(video);
}

export function mount(video) {
    release(video);
    const state = { controller: new AbortController(), poster: null, play: null };
    const signal = state.controller.signal;
    state.play = () => state.controller.abort();
    video.addEventListener('play', state.play, { once: true });
    states.set(video, state);
    const source = video.getAttribute('src');
    queue = queue.then(async () => {
        if (signal.aborted) return;
        const probe = document.createElement('video');
        probe.muted = true; probe.playsInline = true; probe.preload = 'auto';
        const timer = setTimeout(() => state.controller.abort(), 15000);
        try {
            const metadata = event(probe, 'loadedmetadata', signal);
            probe.src = source; probe.load();
            await metadata;
            // Avoid the often empty frame at t=0, including Safari's metadata-only placeholder.
            const sought = event(probe, 'seeked', signal);
            probe.currentTime = Math.min(1, probe.duration / 4);
            await sought;
            if (!probe.videoWidth || !probe.videoHeight) return;
            const canvas = document.createElement('canvas');
            const scale = Math.min(1, 640 / Math.max(probe.videoWidth, probe.videoHeight));
            canvas.width = Math.max(1, Math.round(probe.videoWidth * scale));
            canvas.height = Math.max(1, Math.round(probe.videoHeight * scale));
            canvas.getContext('2d').drawImage(probe, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .8));
            canvas.width = canvas.height = 0;
            if (blob && !signal.aborted && states.get(video) === state) {
                state.poster = URL.createObjectURL(blob);
                video.poster = state.poster;
            }
        } catch { /* Native playback/download remains available for unsupported codecs. */ }
        finally {
            clearTimeout(timer);
            probe.pause(); probe.removeAttribute('src'); probe.load();
        }
    }).catch(() => {});
}
