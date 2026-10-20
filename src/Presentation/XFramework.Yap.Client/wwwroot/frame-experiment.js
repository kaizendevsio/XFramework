import { CameraOrientation } from './_content/Bolt.Media.Browser/bolt-video-orientation.js';
import { orientedSize, paintOriented } from './_content/Bolt.Media.Browser/bolt-media.js';
// Local camera -> encoder -> decoder loop. No media leaves the device.
// The production path's worker/main-thread boundary is retained for a fair preparation comparison.
export async function create(camera, decoded, callback) {
    let stream, worker, encoder, decoder, timer, deadline, alive = true, running = false, configuring = false;
    let orientation, orientationCodes = new Map();
    let generation = 0, decoderNeedsKey = true, rear = false;
    let mode, requestedFps, requestedHeight, source = {}, codec = '', shape = '', startTime, lastTime;
    let captured = 0, encoded = 0, rendered = 0, drops = 0, bytes = 0, key = true, previous = {}, dimensions = {};
    let prepare = [], paint = [], snapshot = [], encode = [], decode = [], render = [];
    const encodeStarts = new Map(), decodeStarts = new Map();
    let report = {}, reports = [], lastFrameAt = 0;
    const invoke = (name, value) => { if (alive) void callback.invokeMethodAsync(name, value).catch(() => {}); };
    const mean = a => a.length ? a.reduce((s, x) => s + x, 0) / a.length : null;
    const percentile = (a, p) => a.length ? [...a].sort((a,b) => a-b)[Math.floor((a.length - 1) * p)] : null;
    const bounded = (map, timestamp) => { map.set(timestamp, performance.now()); if (map.size > 120) map.delete(map.keys().next().value); };
    function sample() {
        if (!running) return;
        const now = performance.now(), seconds = Math.max(.001, (now - lastTime) / 1000);
        report = { mode: mode === 'direct' ? 1 : 2, elapsedMs: now - startTime,
            requestedHeight, requestedFps, rear: Number(rear), ...source, ...dimensions, codec,
            captureFps: (captured - (previous.captured || 0)) / seconds,
            encodedFps: (encoded - (previous.encoded || 0)) / seconds,
            renderedFps: (rendered - (previous.rendered || 0)) / seconds,
            encodedKbps: (bytes - (previous.bytes || 0)) * .008 / seconds, drops,
            prepareMs: mean(prepare), prepareP95Ms: percentile(prepare, .95), paintMs: mean(paint), snapshotMs: mean(snapshot),
            encodeMs: mean(encode), decodeMs: mean(decode), renderMs: mean(render),
            encoderQueue: encoder?.encodeQueueSize || 0, decoderQueue: decoder?.decodeQueueSize || 0,
            screenAngle: screen.orientation?.angle ?? null };
        reports.push(report); if (reports.length > 20) reports.shift();
        invoke('Sample', report);
        previous = { captured, encoded, rendered, bytes }; lastTime = now;
        prepare = []; paint = []; snapshot = []; encode = []; decode = []; render = [];
    }
    async function stop(message = 'Stopped') {
        if (!running && !stream && !worker) return;
        if (running) sample();
        running = false; generation++;
        clearInterval(timer); clearTimeout(deadline);
        // Stop both camera clones, including on navigation, hiding, or codec errors.
        stream?.getTracks().forEach(t => t.stop()); stream = null; camera.srcObject = null;
        if (worker) {
            const closing = worker; worker = null;
            closing.onmessage = ({ data }) => data.frame?.close(); closing.postMessage({ type: 'stop' });
            setTimeout(() => closing.terminate(), 250);
        }
        try { encoder?.close(); } catch { } try { decoder?.close(); } catch { }
        encoder = decoder = null;
        encodeStarts.clear(); decodeStarts.clear(); orientationCodes.clear(); orientation = null;
        invoke('Finished', message);
    }
    function fail(error) { void stop(`Test stopped: ${error.message || error}`); }
    async function configure(frame, current) {
        // No resizing: configure to the source dimensions and record what the decoder actually outputs.
        const width = frame.codedWidth, height = frame.codedHeight;
        const newShape = `${width}x${height}`;
        if (newShape === shape) return;
        if (encoder?.state === 'configured') { await encoder.flush(); if (!running || current !== generation) return; }
        let config;
        for (const candidate of ['avc1.4d0033', 'avc1.420033']) {
            const value = { codec: candidate, width, height, framerate: requestedFps, bitrate: 5_000_000,
                latencyMode: 'realtime', hardwareAcceleration: 'prefer-hardware', avc: { format: 'avc' } };
            if ((await VideoEncoder.isConfigSupported(value)).supported) { config = value; break; }
        }
        if (!config) throw Error('Hardware-preferred H.264 configuration is unsupported. No software retry was used.');
        if (!running || current !== generation) return;
        encoder.configure(config); codec = config.codec; shape = newShape; key = true;
        dimensions.encoderWidth = width; dimensions.encoderHeight = height;
    }
    async function accept(data) {
        const frame = data.frame, current = generation;
        try {
            if (!running || configuring || encoder.encodeQueueSize >= 2) { drops++; return; }
            configuring = true; captured++; lastFrameAt = performance.now(); source = data.source;
            prepare.push(data.prepareMs); paint.push(data.paintMs); snapshot.push(data.snapshotMs);
            let code = 0;
            if (mode === 'direct') {
                code = await orientation.resolve(frame, camera);
                if (!running || current !== generation || code === null) return;
            }
            await configure(frame, current);
            if (!running || current !== generation) return;
            dimensions.appliedOrientation = code;
            orientationCodes.set(frame.timestamp, code);
            if (orientationCodes.size > 120) orientationCodes.delete(orientationCodes.keys().next().value);
            bounded(encodeStarts, frame.timestamp);
            encoder.encode(frame, { keyFrame: key || captured % (requestedFps * 2) === 0 }); key = false;
        } catch (error) { if (current === generation) fail(error); }
        finally { frame.close(); if (current === generation) { configuring = false; worker?.postMessage({ type: 'credit' }); } }
    }
    async function start(options) {
        await stop();
        if (!alive) return;
        if (!['direct', 'webgpu'].includes(options.mode)) throw Error('Choose a test mode.');
        const current = ++generation;
        rear = options.rear === true;
        mode = options.mode; requestedFps = options.fps === 60 ? 60 : 30; requestedHeight = options.height === 720 ? 720 : 1080;
        orientation = new CameraOrientation(); orientationCodes.clear();
        source = {}; dimensions = {}; shape = ''; codec = ''; previous = {}; reports = []; report = {};
        prepare = []; paint = []; snapshot = []; encode = []; decode = []; render = [];
        captured = encoded = rendered = drops = bytes = 0; key = true; decoderNeedsKey = true; configuring = false; running = true;
        startTime = lastTime = lastFrameAt = performance.now();
        deadline = setTimeout(() => void stop('Finished: 30-second sample. Check the decoded picture before trying the other mode.'), 30000);
        try {
            if (!globalThis.VideoEncoder || !globalThis.VideoDecoder) throw Error('WebCodecs is unavailable.');
            const acquired = await navigator.mediaDevices.getUserMedia({ audio: false, video: {
                facingMode: { ideal: rear ? 'environment' : 'user' },
                width: { ideal: Math.round(requestedHeight * 16 / 9) }, height: { ideal: requestedHeight },
                frameRate: { ideal: requestedFps, max: requestedFps } } });
            if (!running || current !== generation) { acquired.getTracks().forEach(t => t.stop()); return; }
            stream = acquired;
            camera.srcObject = stream; await camera.play();
            if (!running || current !== generation) return;
            decoder = new VideoDecoder({ error: fail, output(frame) {
                try {
                    if (!running) return;
                    const began = performance.now(), then = decodeStarts.get(frame.timestamp);
                    if (then !== undefined) { decode.push(began - then); decodeStarts.delete(frame.timestamp); }
                    const code = orientationCodes.get(frame.timestamp) ?? 0; orientationCodes.delete(frame.timestamp);
                    const shown = orientedSize(frame.displayWidth, frame.displayHeight, code);
                    dimensions.decodedWidth = shown.width; dimensions.decodedHeight = shown.height;
                    if (decoded.width !== shown.width) decoded.width = shown.width;
                    if (decoded.height !== shown.height) decoded.height = shown.height;
                    paintOriented(decoded.getContext('2d', { alpha: false }), frame, frame.displayWidth, frame.displayHeight, code);
                    render.push(performance.now() - began); rendered++;
                } catch (error) { fail(error); } finally { frame.close(); }
            } });
            encoder = new VideoEncoder({ error: fail, output(chunk, metadata) {
                if (!running) return;
                const then = encodeStarts.get(chunk.timestamp);
                if (then !== undefined) { encode.push(performance.now() - then); encodeStarts.delete(chunk.timestamp); }
                encoded++; bytes += chunk.byteLength;
                try {
                    if (metadata.decoderConfig) { decoder.configure({ ...metadata.decoderConfig, rotation: 0, flip: false, hardwareAcceleration: 'prefer-hardware', optimizeForLatency: true }); decoderNeedsKey = true; }
                    if (decoder.state !== 'configured') throw Error('Encoder did not supply decoder configuration.');
                    if (decoder.decodeQueueSize >= 2) { drops++; key = true; decoderNeedsKey = true; return; }
                    // After any skipped dependent frame, wait for a keyframe before decoding again.
                    if (decoderNeedsKey && chunk.type !== 'key') return;
                    decoderNeedsKey = false;
                    bounded(decodeStarts, chunk.timestamp); decoder.decode(chunk);
                } catch (error) { fail(error); }
            } });
            worker = new Worker(new URL('./frame-experiment-worker.js', import.meta.url), { type: 'module' });
            worker.onerror = () => fail(Error('Camera worker could not start. No fallback was used.'));
            worker.onmessage = ({ data }) => {
                if (data.type === 'ready') {
                    if (!data.supported) { fail(Error('Camera track processor is unavailable in this worker.')); return; }
                    const clone = stream.getVideoTracks()[0].clone();
                    try { worker.postMessage({ type: 'start', mode, fps: requestedFps, track: clone }, [clone]); }
                    catch (error) { clone.stop(); fail(error); }
                } else if (data.type === 'frame') void accept(data);
                else if (data.type === 'failed') fail(Error(data.message));
            };
            startTime = lastTime = lastFrameAt = performance.now();
            clearTimeout(deadline);
            deadline = setTimeout(() => void stop('Finished: 30-second sample. Check orientation, then try the other mode.'), 30000);
            timer = setInterval(() => { sample(); if (performance.now() - lastFrameAt > 5000) void stop('No camera frames for five seconds. Test stopped.'); }, 2000);
        } catch (error) { if (current === generation) fail(error); }
    }
    const hidden = () => { if (document.hidden) void stop('Stopped because the page was hidden.'); };
    document.addEventListener('visibilitychange', hidden);
    return { start, stop, download() {
        const url = URL.createObjectURL(new Blob([JSON.stringify({ kind: 'yap-direct-frame-experiment', reports }, null, 2)], { type: 'application/json' }));
        const a = document.createElement('a'); a.href = url; a.download = 'yap-frame-experiment.json'; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    }, async dispose() { alive = false; document.removeEventListener('visibilitychange', hidden); await stop(); } };
}
