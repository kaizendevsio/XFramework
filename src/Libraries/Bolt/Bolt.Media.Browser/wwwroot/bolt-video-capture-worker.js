import { GpuFramePainter } from './bolt-video-gpu.js';

// Capture only: the existing encoder, authenticated orientation and transport stay unchanged.
// Transfer VideoFrames, never pixel arrays. One outstanding frame bounds ownership and latency
// even when the page is busy; keep draining and closing newer frames until it returns credit.
let reader, track, stopped = false, credit = true, lastTimestamp = null, nextTimestamp = null, framerate = 30;
let gpuPainter, gpuProbed = false, diagnostics = false, width = 0, height = 0;

async function stop() {
    if (stopped) return;
    stopped = true;
    track?.stop();
    try { await reader?.cancel(); } catch { }
    gpuPainter?.release();
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
                if ((typeof frame.rotation !== 'number' || typeof frame.flip !== 'boolean') && !gpuProbed) {
                    gpuProbed = true;
                    gpuPainter = await GpuFramePainter.create();
                    if (stopped) { gpuPainter?.release(); break; }
                }
                const started = diagnostics ? performance.now() : null;
                let paintMs = null, frameMs = null;
                let output = frame, path = 'worker-processor';
                if (typeof frame.rotation !== 'number' || typeof frame.flip !== 'boolean') {
                    // Normalize hidden orientation through an external GPU texture. Never silently
                    // switch to a texture-upload path if WebGPU is unavailable or fails.
                    if (!gpuPainter) throw new Error('webgpu-unavailable');
                    const init = { timestamp, duration: frame.duration ?? Math.round(1e6 / framerate) };
                    const settings = track.getSettings?.() ?? {};
                    const w = settings.width || width || frame.displayWidth;
                    const h = settings.height || height || frame.displayHeight;
                    const probe = !gpuPainter.checked;
                    let scopeOpen = probe;
                    try {
                        if (probe) gpuPainter.device.pushErrorScope('validation');
                        const canvas = gpuPainter.paint(frame, w, h);
                        const paintedAt = diagnostics ? performance.now() : null;
                        normalized = new VideoFrame(canvas, init);
                        if (diagnostics) { paintMs = paintedAt - started; frameMs = performance.now() - paintedAt; }
                        if (probe) {
                            scopeOpen = false;
                            if (await gpuPainter.device.popErrorScope()) throw Error('webgpu-validation');
                        }
                        gpuPainter.checked = true;
                        path = 'worker-webgpu';
                        output = normalized;
                    } catch {
                        if (scopeOpen) { try { await gpuPainter.device.popErrorScope(); } catch { } }
                        throw new Error('webgpu-failed');
                    }
                }
                if (stopped) break;
                credit = false;
                self.postMessage({ type: 'frame', frame: output, path,
                    prepareMs: started === null ? null : performance.now() - started, paintMs, frameMs }, [output]);
            } finally { normalized?.close(); frame.close(); }
        }
        if (!stopped) self.postMessage({ type: 'failed', reason: 'track-ended' });
    } catch (error) {
        if (!stopped) self.postMessage({ type: 'failed', reason: ['webgpu-unavailable', 'webgpu-failed'].includes(error.message)
            ? error.message : 'worker-capture-failed' });
    } finally { await stop(); }
}

self.onmessage = ({ data }) => {
    if (data.type === 'stop') { void stop(); return; }
    if (data.type === 'diagnostics') { diagnostics = data.enabled === true; return; }
    if (data.type === 'credit') {
        framerate = Math.max(1, Math.min(60, data.framerate || 30));
        width = data.width || width; height = data.height || height;
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
        width = data.width || 0; height = data.height || 0;
        void capture(data.track);
    }
};
self.postMessage({ type: 'ready', supported: typeof MediaStreamTrackProcessor !== 'undefined' });
