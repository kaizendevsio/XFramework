// Safari versions without VideoFrame.rotation/flip still expose sensor pixels through copyTo.
// Compare those pixels with the native, unmirrored video element ONLY while calibrating.
// Never infer a camera's mounting angle from its facingMode or stretch landscape into portrait.
const SIZE = 24;

export function matchOrientation(raw, reference) {
    const n = SIZE * SIZE;
    const mean = a => a.reduce((sum, x) => sum + x, 0) / n;
    const a = mean(raw), b = mean(reference);
    let aa = 0, bb = 0;
    for (let i = 0; i < n; i++) { aa += (raw[i] - a) ** 2; bb += (reference[i] - b) ** 2; }
    if (aa / n < 36 || bb / n < 36) return null; // A blank wall cannot establish orientation.
    const scores = [];
    for (let code = 0; code < 8; code++) {
        let sum = 0;
        for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
            const u = code & 4 ? SIZE - 1 - x : x, v = y;
            const [sx, sy] = [[u, v], [v, SIZE - 1 - u], [SIZE - 1 - u, SIZE - 1 - v], [SIZE - 1 - v, u]][code & 3];
            sum += (raw[sy * SIZE + sx] - a) * (reference[y * SIZE + x] - b);
        }
        scores.push({ code, score: sum / Math.sqrt(aa * bb) });
    }
    scores.sort((x, y) => y.score - x.score);
    return scores[0].score >= .85 && scores[0].score - scores[1].score >= .04 ? scores[0].code : null;
}

export async function rawThumbnail(frame) {
    const format = frame.format;
    const yuv = ['I420', 'I420A', 'I422', 'I444', 'NV12'].includes(format);
    const rgba = ['RGBA', 'RGBX', 'BGRA', 'BGRX'].includes(format);
    if (!yuv && !rgba) throw Error('direct-orientation-pixels-unavailable');
    // copyTo deliberately reads unrotated pixels; drawing a VideoFrame may apply hidden metadata.
    // This allocation is temporary (two successful probes per camera/orientation), never per frame.
    const rect = { x: 0, y: 0, width: frame.codedWidth, height: frame.codedHeight };
    const options = { rect };
    const bytes = new Uint8Array(frame.allocationSize(options));
    const [plane] = await frame.copyTo(bytes, options);
    const visible = frame.visibleRect || rect;
    const result = new Float32Array(SIZE * SIZE);
    for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
        let sum = 0;
        // Average a small grid within each cell to reduce aliasing against the native preview.
        for (let j = 0; j < 4; j++) for (let i = 0; i < 4; i++) {
            const px = visible.x + Math.min(visible.width - 1, Math.floor((x + (i + .5) / 4) * visible.width / SIZE));
            const py = visible.y + Math.min(visible.height - 1, Math.floor((y + (j + .5) / 4) * visible.height / SIZE));
            const at = plane.offset + py * plane.stride + px * (rgba ? 4 : 1);
            sum += yuv ? bytes[at] : .2126 * bytes[at + (format.startsWith('BG') ? 2 : 0)] +
                .7152 * bytes[at + 1] + .0722 * bytes[at + (format.startsWith('BG') ? 0 : 2)];
        }
        result[y * SIZE + x] = sum / 16;
    }
    return result;
}

export class CameraOrientation {
    constructor() { this.signature = ''; this.code = null; this.candidate = null; this.matches = 0; this.lastProbe = -Infinity; }
    async resolve(frame, video, angle = globalThis.screen?.orientation?.angle ?? globalThis.orientation ?? 0) {
        const signature = `${frame.codedWidth}/${frame.codedHeight}/${video.videoWidth}/${video.videoHeight}/${angle}`;
        const now = performance.now();
        if (signature !== this.signature) {
            this.signature = signature; this.code = this.candidate = null; this.matches = 0;
            this.started = now; this.lastProbe = -Infinity;
        }
        if (this.code !== null) return this.code;
        if (now - this.started > 10000) throw Error('direct-orientation-unresolved');
        if (video.readyState < 2 || !video.videoWidth || now - this.lastProbe < 150) return null;
        this.lastProbe = now;
        const canvas = this.canvas ??= new OffscreenCanvas(SIZE, SIZE);
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        ctx.drawImage(video, 0, 0, SIZE, SIZE);
        const pixels = ctx.getImageData(0, 0, SIZE, SIZE).data;
        const reference = Float32Array.from({ length: SIZE * SIZE }, (_, i) =>
            .2126 * pixels[i * 4] + .7152 * pixels[i * 4 + 1] + .0722 * pixels[i * 4 + 2]);
        const code = matchOrientation(await rawThumbnail(frame), reference);
        if (signature !== this.signature) return null;
        this.matches = code !== null && code === this.candidate ? this.matches + 1 : 1;
        this.candidate = code;
        if (code !== null && this.matches >= 2) { this.code = code; this.canvas = null; }
        return this.code;
    }
}
