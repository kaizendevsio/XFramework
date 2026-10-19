import { GlFramePainter } from './bolt-media.js';

// Capture only: the existing encoder, authenticated orientation and transport stay unchanged.
// Transfer VideoFrames, never pixel arrays. One outstanding frame bounds ownership and latency
// even when the page is busy; keep draining and closing newer frames until it returns credit.
let reader, track, stopped = false, credit = true, lastTimestamp = null, nextTimestamp = null, framerate = 30;
let painter, diagnostics = false;

async function stop() {
    if (stopped) return;
    stopped = true;
    track?.stop();
    try { await reader?.cancel(); } catch { }
    painter?.release();
    self.close();
}

async function capture(value) {
    track = value;
    try {
        reader = new MediaStreamTrackProcessor({ track, maxBufferSize: 1 }).readable.getReader();
        while (!stopped) {
            const { value: frame, done } = await reader.read();
            if (done) break;
            let normalized;
            try {
                if (stopped || !credit) continue;
                const timestamp = frame.timestamp;
                const interval = 1e6 / framerate;
                if (lastTimestamp !== null && timestamp <= lastTimestamp) continue;
                if (nextTimestamp !== null && timestamp < nextTimestamp - interval * 0.1) continue;
                nextTimestamp = nextTimestamp === null || timestamp > nextTimestamp + interval
                    ? timestamp + interval : nextTimestamp + interval;
                lastTimestamp = timestamp;
                const started = diagnostics ? performance.now() : null;
                let output = frame, path = 'worker-processor';
                if (typeof frame.rotation !== 'number' || typeof frame.flip !== 'boolean') {
                    // WebKit's texture upload applies the internal camera orientation even when
                    // VideoFrame does not expose rotation/flip to JS. Normalize on the worker's
                    // GPU canvas, not through an element callback on the page. No pixel readback
                    // or guessed portrait angle; known orientation retains the direct frame path.
                    try {
                        painter ??= GlFramePainter.create({ createElement: () => new OffscreenCanvas(1, 1) });
                        if (!painter) throw new Error();
                        const canvas = painter.paint(frame, frame.displayWidth, frame.displayHeight);
                        output = normalized = new VideoFrame(canvas, { timestamp, duration: frame.duration ?? Math.round(1e6 / framerate) });
                        path = 'worker-webgl';
                    } catch { throw new Error('orientation-normalization-unavailable'); }
                }
                credit = false;
                self.postMessage({ type: 'frame', frame: output, path,
                    prepareMs: started === null ? null : performance.now() - started }, [output]);
            } finally { normalized?.close(); frame.close(); }
        }
        if (!stopped) self.postMessage({ type: 'failed', reason: 'track-ended' });
    } catch (error) {
        if (!stopped) self.postMessage({ type: 'failed', reason: error.message === 'orientation-normalization-unavailable'
            ? error.message : 'worker-capture-failed' });
    } finally { await stop(); }
}

self.onmessage = ({ data }) => {
    if (data.type === 'stop') { void stop(); return; }
    if (data.type === 'diagnostics') { diagnostics = data.enabled === true; return; }
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
