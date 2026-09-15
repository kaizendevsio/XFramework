// Voice clips decode once for their waveform. MediaRecorder blobs routinely report an
// Infinity duration, so the decoded buffer is also the only reliable length we have.
const players = new WeakMap();
const meters = new WeakMap();
const maximumDecodeBytes = 12 * 1024 * 1024;
let shared = null, queue = Promise.resolve(), active = null;

const context = () => shared ??= new (window.AudioContext || window.webkitAudioContext)();
const empty = { duration: 0, peaks: [] };

/// Decodes one clip at a time so a thread full of voice messages cannot stall the UI thread.
export function analyse(url, buckets = 44) {
    queue = queue.then(async () => {
        const data = await (await fetch(url)).arrayBuffer();
        if (!data.byteLength || data.byteLength > maximumDecodeBytes) return empty;
        const buffer = await context().decodeAudioData(data);
        const samples = buffer.getChannelData(0), span = Math.max(1, Math.floor(samples.length / buckets));
        const peaks = [];
        for (let bucket = 0; bucket < buckets; bucket++) {
            const start = bucket * span, end = Math.min(samples.length, start + span);
            let sum = 0;
            for (let index = start; index < end; index++) sum += samples[index] * samples[index];
            peaks.push(Math.sqrt(sum / Math.max(1, end - start)));
        }
        // Normalise: a quiet recording still deserves a readable waveform.
        const loudest = Math.max(...peaks, 1e-5);
        return { duration: buffer.duration, peaks: peaks.map(peak => Math.round(Math.min(1, peak / loudest) * 100) / 100) };
    }).catch(() => empty);
    return queue;
}

export function attach(audio, ref) {
    detach(audio);
    const controller = new AbortController();
    const signal = controller.signal;
    const report = () => ref.invokeMethodAsync('Sync', audio.currentTime || 0,
        Number.isFinite(audio.duration) ? audio.duration : 0, !audio.paused).catch(() => {});
    for (const name of ['timeupdate', 'play', 'pause', 'ended', 'loadedmetadata', 'durationchange'])
        audio.addEventListener(name, report, { signal });
    // One voice message at a time, the way a native thread behaves.
    audio.addEventListener('play', () => { if (active && active !== audio) active.pause(); active = audio; }, { signal });
    players.set(audio, controller);
    report();
}

export function detach(audio) {
    players.get(audio)?.abort();
    players.delete(audio);
    if (active === audio) { audio.pause(); active = null; }
}

export function toggle(audio) { if (audio.paused) audio.play().catch(() => {}); else audio.pause(); }

export function seek(audio, seconds) {
    if (!Number.isFinite(seconds) || seconds < 0) return;
    // Seeking before metadata arrives throws on some engines; the next play still starts at 0.
    try { audio.currentTime = seconds; } catch { /* Not seekable yet. */ }
}

/// Animates the live recording bar from the analyser so Blazor never renders per frame.
export function meter(bar) {
    stopMeter(bar);
    const recording = window.yapRecording;
    if (!recording || !bar) return;
    const elapsed = bar.querySelector('[data-voice-elapsed]');
    const bars = [...bar.querySelectorAll('[data-voice-bar]')];
    const analyser = recording.analyser;
    const samples = analyser ? new Uint8Array(analyser.fftSize) : null;
    const state = { frame: 0, sampled: 0, levels: new Array(bars.length).fill(.06) };
    const tick = () => {
        if (window.yapRecording !== recording || recording.recorder?.state === 'inactive') return;
        const seconds = (performance.now() - recording.started) / 1000;
        if (elapsed) elapsed.textContent = `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;
        // One column per ~70ms keeps the trace readable instead of a blur at display rate.
        if (samples && performance.now() - state.sampled > 70) {
            state.sampled = performance.now();
            analyser.getByteTimeDomainData(samples);
            let sum = 0;
            for (const sample of samples) sum += (sample - 128) * (sample - 128);
            state.levels.push(Math.min(1, Math.sqrt(sum / samples.length) / 42));
            state.levels.shift();
            bars.forEach((element, index) => element.style.setProperty('--level', String(Math.max(.06, state.levels[index]))));
        }
        state.frame = requestAnimationFrame(tick);
    };
    meters.set(bar, state);
    tick();
}

export function stopMeter(bar) {
    const state = meters.get(bar);
    if (state) cancelAnimationFrame(state.frame);
    meters.delete(bar);
}
