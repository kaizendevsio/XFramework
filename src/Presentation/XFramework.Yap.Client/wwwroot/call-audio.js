// Call ring audio. Tones are synthesised from oscillators instead of shipped as
// audio files: nothing extra to download or cache, and "choose a ringtone"
// becomes real variety rather than one asset. Playback uses its own
// AudioContext so a ring never disturbs the call pipeline's context in
// bolt-media.js, which owns device routing and suspend/resume recovery.
(() => {
    const TONES = [
        // dur/at are seconds inside one cadence; period is the gap before it repeats.
        { id: 'classic', label: 'Classic', wave: 'sine', level: .5, period: 4,
            beeps: [{ at: 0, dur: 1.2, freq: [440, 480] }], vibrate: [900, 3100] },
        { id: 'double', label: 'Double ring', wave: 'sine', level: .5, period: 3,
            beeps: [{ at: 0, dur: .4, freq: [400, 450] }, { at: .6, dur: .4, freq: [400, 450] }], vibrate: [400, 200, 400, 2000] },
        { id: 'chime', label: 'Chime', wave: 'triangle', level: .55, period: 3, decay: true,
            beeps: [{ at: 0, dur: .5, freq: [659.25] }, { at: .2, dur: .5, freq: [830.61] }, { at: .4, dur: .9, freq: [1046.5] }], vibrate: [700, 2300] },
        { id: 'sparkle', label: 'Sparkle', wave: 'sine', level: .6, period: 3, decay: true,
            beeps: [{ at: 0, dur: .45, freq: [784] }, { at: .15, dur: .45, freq: [1046.5] }, { at: .3, dur: .45, freq: [1318.5] }, { at: .45, dur: .8, freq: [1046.5] }], vibrate: [600, 2400] },
        { id: 'pulse', label: 'Pulse', wave: 'triangle', level: .5, period: 2.4,
            beeps: [{ at: 0, dur: .12, freq: [523.25] }, { at: .22, dur: .12, freq: [523.25] }, { at: .44, dur: .12, freq: [523.25] }], vibrate: [120, 110, 120, 110, 120, 1820] },
        { id: 'silent', label: 'Silent (vibrate only)', wave: 'sine', level: 0, period: 3, beeps: [], vibrate: [500, 400, 500, 1600] }
    ];
    const TONE_KEY = 'yap-ringtone', VOLUME_KEY = 'yap-ring-volume', DEFAULT_VOLUME = .7;
    const find = id => TONES.find(tone => tone.id === id) ?? TONES[0];

    let context = null, master = null, cycle = null, ending = null, mode = '', unlocked = false, lastResume = -Infinity;
    const voices = new Set();

    const read = () => {
        let tone = TONES[0].id, volume = DEFAULT_VOLUME;
        try {
            const saved = localStorage.getItem(TONE_KEY);
            if (TONES.some(item => item.id === saved)) tone = saved;
            const stored = localStorage.getItem(VOLUME_KEY);
            // A missing key is not a zero volume: Number(null) is 0 and would silence every call.
            if (stored !== null && stored !== '') {
                const level = Number(stored);
                if (Number.isFinite(level) && level >= 0 && level <= 1) volume = level;
            }
        } catch {}
        return { tone, volume };
    };

    const ensure = () => {
        if (context) return context;
        const Audio = globalThis.AudioContext ?? globalThis.webkitAudioContext;
        if (!Audio) return null;
        try { context = new Audio({ latencyHint: 'interactive' }); } catch { return null; }
        // Android suspends Web Audio during permission prompts and route changes;
        // a ring that is still wanted has to climb back out on its own.
        context.onstatechange = () => { if (mode && context?.state !== 'running') void resume(false); };
        return context;
    };

    const resume = async (userAction = true) => {
        const audio = ensure();
        if (!audio || audio.state === 'closed') return false;
        if (audio.state !== 'running') {
            if (!userAction && Date.now() - lastResume < 1000) return false;
            lastResume = Date.now();
            try { await audio.resume(); } catch {} // Autoplay policy, not an error: the caller reports silence.
        }
        const running = context === audio && audio.state === 'running';
        if (running) unlocked = true;
        return running;
    };

    // An incoming call brings no gesture of its own, so the first tap anywhere in
    // the session primes the context; it is parked again until something rings.
    const gesture = () => {
        if (mode) { void resume(true); return; }
        if (unlocked || !ensure()) return;
        void resume(true).then(ok => { if (ok && !mode) context?.suspend().catch(() => {}); });
    };
    document.addEventListener('pointerdown', gesture, true);
    document.addEventListener('keydown', gesture, true);

    const buzz = tone => { try { return navigator.vibrate?.(tone.vibrate) === true; } catch { return false; } };

    const play = (tone, level, at) => {
        for (const beep of tone.beeps) {
            const gain = context.createGain(), peak = Math.max(.0001, level / beep.freq.length);
            const from = at + beep.at, to = from + beep.dur;
            gain.connect(master);
            // Ramps, never steps: a bare start/stop on an oscillator clicks.
            gain.gain.setValueAtTime(.0001, from);
            gain.gain.linearRampToValueAtTime(peak, from + .012);
            if (tone.decay) gain.gain.exponentialRampToValueAtTime(.0001, to);
            else { gain.gain.setValueAtTime(peak, Math.max(from + .013, to - .04)); gain.gain.linearRampToValueAtTime(.0001, to); }
            for (const frequency of beep.freq) {
                const osc = context.createOscillator();
                osc.type = tone.wave; osc.frequency.value = frequency;
                osc.connect(gain); osc.start(from); osc.stop(to + .02);
                osc.onended = () => { voices.delete(osc); try { osc.disconnect(); gain.disconnect(); } catch {} };
                voices.add(osc);
            }
        }
    };

    function stop(park = true) {
        mode = '';
        if (cycle !== null) { clearInterval(cycle); cycle = null; }
        if (ending !== null) { clearTimeout(ending); ending = null; }
        try { navigator.vibrate?.(0); } catch {}
        for (const osc of voices) { try { osc.onended = null; osc.stop(); osc.disconnect(); } catch {} }
        voices.clear();
        if (master) { try { master.disconnect(); } catch {} master = null; }
        // Park the context rather than close it: closing would need a fresh gesture
        // to unlock, and the next call may arrive without one.
        if (park && context?.state === 'running') context.suspend().catch(() => {});
    }

    async function start(kind) {
        const wanted = kind === 'ringback' || kind === 'preview' ? kind : 'ringtone';
        stop(false); // A second invitation replaces the first ring; rings never stack.
        const { tone: id, volume } = read(), tone = find(id);
        mode = wanted;
        const vibrating = wanted === 'ringtone' && typeof navigator.vibrate === 'function';
        const audible = tone.level > 0 && volume > 0 && await resume();
        if (mode !== wanted) return { audible: false, vibrating: false }; // Stopped while the context was resuming.
        if (audible) {
            master = context.createGain();
            master.gain.value = 1;
            master.connect(context.destination);
        }
        // Ringback is quieter: the caller is usually already holding the phone to an ear.
        const level = volume * volume * tone.level * (wanted === 'ringback' ? .55 : 1);
        const emit = () => { if (audible && master) play(tone, level, context.currentTime + .06); if (vibrating) buzz(tone); };
        emit();
        if (wanted === 'preview') ending = setTimeout(stop, tone.period * 1000);
        else if (audible || vibrating) cycle = setInterval(emit, tone.period * 1000);
        window.yap?.diagnostics?.record('call.ring', { phase: wanted, tone: tone.id, audible, vibrating, state: context?.state ?? 'none' });
        return { audible, vibrating };
    }

    (window.yap ??= {}).ring = {
        tones: () => TONES.map(tone => ({ id: tone.id, label: tone.label })),
        preference: read,
        setTone(id) { const tone = find(id); try { localStorage.setItem(TONE_KEY, tone.id); } catch {} return tone.id; },
        setVolume(value) {
            const level = Math.min(1, Math.max(0, Number(value) || 0));
            try { localStorage.setItem(VOLUME_KEY, String(level)); } catch {}
            return level;
        },
        start, stop,
        // Previewing is also the gesture that proves this device can ring at all.
        preview(id) { if (mode === 'ringtone' || mode === 'ringback') return Promise.resolve({ audible: false, vibrating: false }); if (id) this.setTone(id); return start('preview'); },
        unlock: () => resume(true),
        state: () => ({ mode, unlocked, context: context?.state ?? 'closed' })
    };
    // Leaving the page must never leave a ring (or a vibration) running behind it.
    addEventListener('pagehide', stop);
})();
