// Orientation end to end in a real browser (VideoOrientationBrowserTests.cs).
//
// A synthetic phone camera: sensor-oriented (landscape) pixels with the display rotation and flip as VideoFrame
// metadata, the way Android Chrome's MediaStreamTrackProcessor hands camera frames over. It reaches the production
// sender through a MediaStreamTrackGenerator posing as the camera track, so the real capture path runs: the
// processor, the orientation handling, the real VideoEncoder. The production receiver decodes with the real
// VideoDecoder and paints into a canvas, which is then read back. The expected picture is what the browser itself
// draws for the camera frame, orientation metadata included.

import { createVideoPipeline, orientationCode } from './bolt-media.js';

const SENSOR = { width: 640, height: 360 };
// Distinct corners: every one of the eight rotations and flips puts them somewhere different.
const CORNERS = [[255, 0, 0], [0, 255, 0], [0, 0, 255], [255, 255, 255]];

function sensorCanvas({ width, height } = SENSOR) {
    const canvas = new OffscreenCanvas(width, height);
    const context = canvas.getContext('2d');
    const halfW = width / 2, halfH = height / 2;
    [[0, 0], [halfW, 0], [0, halfH], [halfW, halfH]].forEach(([x, y], i) => {
        context.fillStyle = `rgb(${CORNERS[i].join(',')})`;
        context.fillRect(x, y, halfW, halfH);
    });
    return canvas;
}

/// The colour at the centre of each quadrant of a canvas, as [top-left, top-right, bottom-left, bottom-right].
function quadrants(canvas) {
    const context = canvas.getContext('2d', { willReadFrequently: true });
    const w = canvas.width, h = canvas.height;
    return [[w / 4, h / 4], [3 * w / 4, h / 4], [w / 4, 3 * h / 4], [3 * w / 4, 3 * h / 4]]
        .map(([x, y]) => [...context.getImageData(Math.floor(x), Math.floor(y), 1, 1).data.slice(0, 3)]);
}

/// What the browser shows for the camera frame itself: drawImage applies its rotation and flip.
function expected(source, rotation, flip) {
    const frame = new VideoFrame(source, { timestamp: 0, rotation, flip });
    const canvas = document.createElement('canvas');
    canvas.width = frame.displayWidth; canvas.height = frame.displayHeight;
    canvas.getContext('2d').drawImage(frame, 0, 0, canvas.width, canvas.height);
    frame.close();
    return { width: canvas.width, height: canvas.height, quadrants: quadrants(canvas) };
}

const until = async (condition, ms = 10_000) => {
    const end = performance.now() + ms;
    while (!condition()) {
        if (performance.now() > end) throw new Error('timed out');
        await new Promise(resolve => setTimeout(resolve, 10));
    }
};

export function supportsOrientation() {
    try {
        const frame = new VideoFrame(sensorCanvas(), { timestamp: 0, rotation: 90 });
        const supported = frame.rotation === 90;
        frame.close();
        return supported && typeof MediaStreamTrackGenerator === 'function' && typeof MediaStreamTrackProcessor === 'function';
    } catch { return false; }
}

export async function codecSupported(codec) {
    const { encoderConfig } = await import('./bolt-media.js');
    try { return (await VideoEncoder.isConfigSupported(encoderConfig(codec, 640, 360, 1000, 30, 'no-preference'))).supported === true; }
    catch { return false; }
}

/// Send `frames` pictures of one orientation from a phone-shaped camera, decode them on the far side, and report what the
/// far side's canvas shows next to what the browser shows for the camera frame.
export async function roundTrip({ codec, rotation, flip, metadata, frames = 6 }) {
    const source = sensorCanvas();
    const generator = new MediaStreamTrackGenerator({ kind: 'video' });
    const writer = generator.writable.getWriter();
    navigator.mediaDevices.getUserMedia = async () => new MediaStream([generator]);

    const encoded = [];
    const sendHost = { invokeMethodAsync: async (name, ...args) => { if (name === 'OnVideoEncoded') encoded.push(args); } };
    const sender = createVideoPipeline();
    sender.useCaptureStrategy('processor');
    sender.setOrientationMetadata(metadata);
    await sender.initEncoder(codec, 640, 360, 1500, 30, 2);
    await sender.startCapture(sendHost, {});
    for (let i = 0; i < frames; i++) {
        await writer.write(new VideoFrame(source, { timestamp: i * 33_334, rotation, flip }));
        await until(() => encoded.length > i || sender.stats.dropped > 0, 5_000).catch(() => {});
    }
    await until(() => encoded.length >= 2);
    const diagnostics = sender.getDiagnostics();
    const sent = { width: sender.config.width, height: sender.config.height, orientation: diagnostics.orientation,
        codes: encoded.map(x => x[5]) };

    const canvas = document.createElement('canvas');
    document.body.append(canvas);
    const receiveHost = { invokeMethodAsync: async () => {} };
    const receiver = createVideoPipeline();
    if (!receiver.addRemote('s', canvas, codec, receiveHost)) throw new Error('no remote');
    for (const [data, key, , timestamp, , orientation] of encoded) receiver.decodeFrame('s', data, timestamp, key, false, orientation);
    await until(() => receiver.remotes.get('s').rendered >= encoded.length);
    const shown = { width: canvas.width, height: canvas.height, quadrants: quadrants(canvas),
        rotation: receiver.getDiagnostics().remotes[0].rotation };

    sender.stopCapture();
    await sender.dispose(); await receiver.dispose();
    canvas.remove();
    return { sent, shown, expected: expected(source, rotation, flip), code: orientationCode(rotation, flip) };
}

/// Sender cost of a rotated 720p camera frame before it reaches the encoder: the redraw (2D canvas drawImage plus a new
/// VideoFrame from the canvas) against taking the orientation off (a new VideoFrame over the same pixels). Main-thread
/// time per frame, then the whole picture through the real encoder (submit to output, flushed per picture), where the
/// redraw's copy is paid for wherever the browser pays it.
export async function senderCost({ codec, frames = 120 }) {
    const source = sensorCanvas({ width: 1280, height: 720 });
    const result = {};
    for (const metadata of [false, true]) {
        const pipeline = createVideoPipeline();
        pipeline.setOrientationMetadata(metadata);
        const prepare = [];
        // Warm-up: the first redraw allocates the canvas and its context.
        for (let i = 0; i < frames + 10; i++) {
            const camera = new VideoFrame(source, { timestamp: i * 33_334, rotation: 90 });
            const started = performance.now();
            const { frame } = pipeline._orient(camera);
            const elapsed = performance.now() - started;
            frame.close();
            if (i >= 10) prepare.push(elapsed);
        }
        let outputs = 0;
        const encoder = new VideoEncoder({ output: () => { outputs++; }, error: error => { throw error; } });
        const { encoderConfig } = await import('./bolt-media.js');
        const width = metadata ? 1280 : 720, height = metadata ? 720 : 1280;
        encoder.configure(encoderConfig(codec, width, height, 2500, 30, 'no-preference'));
        const encode = [];
        for (let i = 0; i < 40; i++) {
            const camera = new VideoFrame(source, { timestamp: i * 33_334, rotation: 90 });
            const started = performance.now();
            const { frame } = pipeline._orient(camera);
            encoder.encode(frame, { keyFrame: i === 0 });
            frame.close();
            await encoder.flush();
            if (i >= 5) encode.push(performance.now() - started);
        }
        encoder.close();
        const mean = values => values.reduce((a, b) => a + b, 0) / values.length;
        const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
        result[metadata ? 'metadata' : 'redraw'] = { prepareMeanMs: mean(prepare), prepareMedianMs: median(prepare),
            encodeMeanMs: mean(encode), encodeMedianMs: median(encode), outputs, how: pipeline.orientationState.how };
        await pipeline.dispose();
    }
    return result;
}
