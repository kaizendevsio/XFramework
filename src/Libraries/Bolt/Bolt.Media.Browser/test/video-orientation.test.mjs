import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const source = readFileSync(new URL('../wwwroot/bolt-video-orientation.js', import.meta.url), 'utf8').replaceAll('export ', '');
const size = 24;
const pattern = Float32Array.from({ length: size * size }, (_, i) => 30 + ((i * 41 + Math.floor(i / size) * 17) % 190));
function rotate(raw, code) {
    const out = new Float32Array(raw.length);
    // Forward mapping, independent of the inverse lookup in the implementation.
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
        let dx = x, dy = y;
        for (let r = 0; r < (code & 3); r++) [dx, dy] = [size - 1 - dy, dx];
        if (code & 4) dx = size - 1 - dx;
        out[dy * size + dx] = raw[y * size + x];
    }
    return out;
}
function fixture() {
    let now = 0, reference = pattern;
    const sandbox = { Float32Array, Uint8Array, Math, Error, performance: { now: () => now },
        OffscreenCanvas: class { getContext() { return { drawImage() {}, getImageData() {
            return { data: Uint8Array.from({ length: size * size * 4 }, (_, i) => i % 4 === 3 ? 255 : reference[Math.floor(i / 4)]) };
        } }; } } };
    vm.runInNewContext(source + ';globalThis.api = { CameraOrientation, matchOrientation, rawThumbnail };', sandbox);
    return { ...sandbox.api, advance() { now += 200; }, expire() { now += 11000; }, reference(value) { reference = value; } };
}
function frame(format = 'NV12') {
    const f = { format, codedWidth: size, codedHeight: size, visibleRect: { x: 0, y: 0, width: size, height: size }, reads: 0,
        allocationSize() { return 8 + (size + 8) * size * (format === 'RGBA' ? 4 : 1); },
        async copyTo(bytes) {
            f.reads++;
            const stride = (size + 8) * (format === 'RGBA' ? 4 : 1);
            for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
                const at = 8 + stride * y + x * (format === 'RGBA' ? 4 : 1);
                if (format === 'RGBA') bytes.fill(pattern[y * size + x], at, at + 4);
                else bytes[at] = pattern[y * size + x];
            }
            return [{ offset: 8, stride }];
        } };
    return f;
}
test('front/rear mirrored and unmirrored fixtures recover all four rotations', () => {
    const f = fixture();
    for (let code = 0; code < 8; code++) assert.equal(f.matchOrientation(pattern, rotate(pattern, code)), code);
    assert.equal(f.matchOrientation(new Float32Array(size * size).fill(100), pattern), null);
    assert.equal(f.matchOrientation(pattern, new Float32Array(size * size)), null);
});
test('raw NV12 and RGBA reads honor plane offsets and padded strides', async () => {
    const f = fixture();
    for (const format of ['NV12', 'RGBA']) {
        const result = await f.rawThumbnail(frame(format));
        for (let i = 0; i < pattern.length; i++) assert.ok(Math.abs(result[i] - pattern[i]) < .001);
    }
    await assert.rejects(() => f.rawThumbnail(frame('unknown')), /pixels-unavailable/);
});
test('calibration requires two matches; steady state has zero pixel reads; rotation invalidates', async () => {
    const f = fixture(), calibration = new f.CameraOrientation(), raw = frame();
    const video = { readyState: 2, videoWidth: 1080, videoHeight: 1920 };
    for (const code of [1, 0, 3, 2, 5, 4, 7, 6]) {
        f.reference(rotate(pattern, code)); f.advance();
        assert.equal(await calibration.resolve(raw, video, code * 90), null);
        f.advance(); assert.equal(await calibration.resolve(raw, video, code * 90), code);
        const reads = raw.reads;
        for (let i = 0; i < 60; i++) assert.equal(await calibration.resolve(raw, video, code * 90), code);
        assert.equal(raw.reads, reads);
    }
});
test('ambiguous scenes fail explicitly and camera restarts do not reuse calibration', async () => {
    const f = fixture(), raw = frame(), video = { readyState: 2, videoWidth: 24, videoHeight: 24 };
    const first = new f.CameraOrientation();
    await first.resolve(raw, video, 0); f.advance(); assert.equal(await first.resolve(raw, video, 0), 0);
    const second = new f.CameraOrientation();
    f.reference(new Float32Array(size * size));
    assert.equal(await second.resolve(raw, video, 0), null);
    f.expire(); await assert.rejects(() => second.resolve(raw, video, 0), /unresolved/);
});
