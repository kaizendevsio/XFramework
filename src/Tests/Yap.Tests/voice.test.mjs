import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';

const source = (await fs.readFile(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/voice.js', import.meta.url), 'utf8'))
    .replaceAll('export function ', 'function ');

class Clip extends EventTarget {
    paused = true; currentTime = 0; duration = Infinity;
    play() { this.paused = false; this.dispatchEvent(new Event('play')); return Promise.resolve(); }
    pause() { this.paused = true; this.dispatchEvent(new Event('pause')); }
}

function fixture({ bytes = 4000, duration = 7.5, decode = true } = {}) {
    const frames = []; let clock = 100000;
    // A quiet clip: normalisation must still produce a readable trace.
    const channel = Float32Array.from({ length: 800 }, (_, index) => index < 400 ? .02 : .001);
    const window = {
        AudioContext: class { decodeAudioData() { return decode ? Promise.resolve({ duration, getChannelData: () => channel }) : Promise.reject(new Error('corrupt')); } }
    };
    const context = vm.createContext({ window, EventTarget, Event, AbortController, Float32Array, Uint8Array,
        performance: { now: () => clock },
        fetch: async () => ({ arrayBuffer: async () => new ArrayBuffer(bytes) }),
        requestAnimationFrame: callback => frames.push(callback), cancelAnimationFrame: handle => { frames[handle - 1] = null; } });
    vm.runInContext(source, context);
    return { context, window, frames, advance: ms => clock += ms, run: () => { for (const frame of frames.splice(0)) frame?.(); } };
}

test('a clip decodes into normalised buckets and yields the length MediaRecorder will not report', async () => {
    const wave = await fixture().context.analyse('blob:voice', 4);
    assert.equal(wave.duration, 7.5);
    assert.deepEqual([...wave.peaks], [1, 1, .05, .05]);
});

test('unreadable or oversized clips fall back to an empty waveform instead of throwing', async () => {
    for (const options of [{ decode: false }, { bytes: 20 * 1024 * 1024 }]) {
        const wave = await fixture(options).context.analyse('blob:voice', 4);
        assert.equal(wave.duration, 0);
        assert.equal(wave.peaks.length, 0);
    }
});

test('playback reports position to Blazor and only one clip plays at a time', () => {
    const f = fixture();
    const first = new Clip(), second = new Clip(), reports = [];
    const ref = { invokeMethodAsync: (name, time, length, active) => { reports.push([name, time, length, active]); return Promise.resolve(); } };
    f.context.attach(first, ref); f.context.attach(second, ref);
    // An Infinity duration must never reach the player as a length.
    assert.deepEqual(reports.at(-1), ['Sync', 0, 0, false]);
    first.play();
    second.play();
    assert.equal(first.paused, true);
    assert.equal(second.paused, false);
    second.currentTime = 3; second.duration = 9; second.dispatchEvent(new Event('timeupdate'));
    assert.deepEqual(reports.at(-1), ['Sync', 3, 9, true]);
    f.context.detach(second);
    assert.equal(second.paused, true);
    second.dispatchEvent(new Event('timeupdate'));
    assert.deepEqual(reports.at(-1), ['Sync', 3, 9, true]);
});

test('the recording meter writes the clock and level trace, and stops with the recorder', () => {
    const f = fixture();
    const levels = [], elapsed = { textContent: '' };
    const bars = Array.from({ length: 3 }, () => ({ style: { setProperty: (name, value) => levels.push(value) } }));
    const bar = { querySelector: () => elapsed, querySelectorAll: () => bars };
    f.window.yapRecording = { started: 100000 - 65000, recorder: { state: 'recording' },
        analyser: { fftSize: 8, getByteTimeDomainData: array => array.fill(170) } };
    f.context.meter(bar);
    assert.equal(elapsed.textContent, '1:05');
    assert.deepEqual(levels, ['0.06', '0.06', '1']);
    f.advance(1000); f.run();
    assert.equal(elapsed.textContent, '1:06');
    f.window.yapRecording.recorder.state = 'inactive';
    f.run();
    assert.equal(f.frames.length, 0);
});
