import { GpuFramePainter } from '/_content/Bolt.Media.Browser/bolt-video-gpu.js';

// Deliberately separate from live calls. Direct means the original camera frame:
// no canvas, orientation rewrite, pixel readback, or automatic fallback.
let reader, track, painter, stopped = false, credit = true;
async function stop() {
    stopped = true;
    track?.stop();
    try { await reader?.cancel(); } catch { }
    painter?.release();
    self.close();
}
async function start(data) {
    track = data.track;
    try {
        if (data.mode === 'webgpu') {
            painter = await GpuFramePainter.create();
            if (!painter) throw Error('WebGPU is unavailable. No alternate path was used.');
        }
        if (stopped) return;
        reader = new MediaStreamTrackProcessor({ track, maxBufferSize: 1 }).readable.getReader();
        let next = 0;
        while (!stopped) {
            const { value: frame, done } = await reader.read();
            if (done) break;
            let prepared;
            try {
                if (stopped || !credit || frame.timestamp < next) continue;
                next = frame.timestamp + 1e6 / data.fps * .9;
                const began = performance.now();
                const settings = track.getSettings();
                const source = { width: frame.codedWidth, height: frame.codedHeight,
                    displayWidth: frame.displayWidth, displayHeight: frame.displayHeight,
                    rotation: typeof frame.rotation === 'number' ? frame.rotation : null,
                    flip: typeof frame.flip === 'boolean' ? Number(frame.flip) : null,
                    cameraWidth: settings.width, cameraHeight: settings.height, cameraFps: settings.frameRate };
                let output = frame, paintMs = 0, snapshotMs = 0;
                if (painter) {
                    const canvas = painter.paint(frame, settings.width || frame.displayWidth, settings.height || frame.displayHeight);
                    const painted = performance.now();
                    prepared = new VideoFrame(canvas, { timestamp: frame.timestamp, duration: frame.duration ?? Math.round(1e6 / data.fps) });
                    paintMs = painted - began; snapshotMs = performance.now() - painted;
                    output = prepared;
                }
                credit = false;
                self.postMessage({ type: 'frame', frame: output, source, prepareMs: performance.now() - began, paintMs, snapshotMs }, [output]);
            } finally { prepared?.close(); frame.close(); }
        }
        if (!stopped) self.postMessage({ type: 'failed', message: 'Camera stream ended.' });
    } catch (error) {
        if (!stopped) self.postMessage({ type: 'failed', message: error.message });
    } finally { await stop(); }
}
self.onmessage = ({ data }) => {
    if (data.type === 'stop') void stop();
    if (data.type === 'credit') credit = true;
    if (data.type === 'start' && !track) void start(data);
};
self.postMessage({ type: 'ready', supported: typeof MediaStreamTrackProcessor !== 'undefined' });
