// Capture only: the existing encoder, authenticated orientation and transport stay unchanged.
// Transfer VideoFrames, never pixel arrays. One outstanding frame bounds ownership and latency
// even when the page is busy; keep draining and closing newer frames until it returns credit.
let reader, track, stopped = false, credit = true, lastTimestamp = null, framerate = 30;

async function stop() {
    if (stopped) return;
    stopped = true;
    track?.stop();
    try { await reader?.cancel(); } catch { }
    self.close();
}

async function capture(value) {
    track = value;
    try {
        reader = new MediaStreamTrackProcessor({ track, maxBufferSize: 1 }).readable.getReader();
        while (!stopped) {
            const { value: frame, done } = await reader.read();
            if (done) break;
            try {
                if (stopped || !credit) continue;
                // Older WebKit can hide sensor orientation. Do not reintroduce sideways video:
                // use the proven upright WebGL path when orientation cannot be inspected.
                if (typeof frame.rotation !== 'number' || typeof frame.flip !== 'boolean')
                    throw new Error('orientation-unavailable');
                const timestamp = frame.timestamp;
                if (lastTimestamp !== null && timestamp >= lastTimestamp &&
                    timestamp - lastTimestamp < 1e6 / framerate - 500) continue;
                lastTimestamp = timestamp;
                credit = false;
                self.postMessage({ type: 'frame', frame }, [frame]);
            } finally { frame.close(); }
        }
        if (!stopped) self.postMessage({ type: 'failed' });
    } catch {
        if (!stopped) self.postMessage({ type: 'failed' });
    } finally { await stop(); }
}

self.onmessage = ({ data }) => {
    if (data.type === 'stop') { void stop(); return; }
    if (data.type === 'credit') {
        framerate = Math.max(1, Math.min(60, data.framerate || 30));
        credit = true;
    }
    if (data.type === 'constraints') {
        framerate = Math.max(1, Math.min(60, data.framerate || 30));
        // Both clones must request the reduced size, otherwise this clone could keep the sensor
        // at its previous high-power configuration after the visible track was reduced.
        void track?.applyConstraints?.(data.constraints)?.catch(() => { });
    }
    if (data.type === 'start' && !track && !stopped) {
        framerate = Math.max(1, Math.min(60, data.framerate || 30));
        void capture(data.track);
    }
};
self.postMessage({ type: 'ready', supported: typeof MediaStreamTrackProcessor !== 'undefined' });
