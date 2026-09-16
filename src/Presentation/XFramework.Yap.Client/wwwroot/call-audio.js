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
    // Ringback is what the *caller* hears, and it is deliberately not in TONES: the picker
    // dresses up "somebody is calling you", while this says "their phone is ringing". It is
    // the conventional slow double-beep on a single 425 Hz telephone frequency - no musical
    // interval, no beating pair - so it can never be mistaken for anyone's chosen ringtone.
    const RINGBACK = { id: 'ringback', wave: 'sine', level: .09, period: 4, vibrate: [],
        beeps: [{ at: 0, dur: .4, freq: [425] }, { at: .7, dur: .4, freq: [425] }] };
    const TONE_KEY = 'yap-ringtone', VOLUME_KEY = 'yap-ring-volume', DEFAULT_VOLUME = .7;
    // resume() under an autoplay policy settles neither way until the page earns a gesture,
    // so every wait on it is capped; the ring falls back rather than hanging on the promise.
    const RESUME_MS = 350;
    const find = id => TONES.find(tone => tone.id === id) ?? TONES[0];

    let context = null, master = null, cycle = null, ending = null, mode = '', unlocked = false, lastResume = -Infinity;
    let starting = 0, generation = 0;
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
        // A closed context can never be resumed again. iOS closes one after a long
        // interruption, so it is replaced rather than kept as a permanently dead handle.
        if (context && context.state !== 'closed') return context;
        const Audio = globalThis.AudioContext ?? globalThis.webkitAudioContext;
        if (!Audio) return null;
        try { context = new Audio({ latencyHint: 'interactive' }); } catch { return null; }
        context.onstatechange = () => {
            if (!mode || starting) return;
            // Android suspends Web Audio during permission prompts and route changes;
            // a ring that is still wanted has to climb back out on its own.
            if (context?.state !== 'running') { void resume(false); return; }
            // The other direction: audio that was refused at ring time is unlocked later
            // (a tap, an iOS interruption ending). The ring is still wanted, so play it now
            // instead of waiting for the next invitation.
            if (!master) start(mode).catch(() => {});
        };
        return context;
    };

    const resume = async (userAction = true) => {
        const audio = ensure();
        if (!audio) return false;
        if (audio.state !== 'running') {
            if (!userAction && Date.now() - lastResume < 1000) return false;
            lastResume = Date.now();
            // Autoplay refusal is not an error and not a rejection: it is a promise that never
            // settles. Racing it keeps vibration, the retry button and stop() from queueing
            // behind a wait that may last until the user taps Accept.
            try { await Promise.race([Promise.resolve(audio.resume()).catch(() => {}), new Promise(done => setTimeout(done, RESUME_MS))]); } catch {}
        }
        const running = context === audio && audio.state === 'running';
        if (running) unlocked = true;
        return running;
    };

    // An incoming call brings no gesture of its own, so taps anywhere in the session prime
    // the context; it is parked again until something rings. Priming repeats whenever the
    // context is not resumable - once is not enough across a backgrounded PWA, an iOS
    // interruption or a context the browser closed under us.
    const gesture = () => {
        if (mode) { void resume(true); return; }
        const audio = ensure();
        if (!audio || (unlocked && audio.state === 'suspended')) return;
        void resume(true).then(ok => { if (ok && !mode) context?.suspend().catch(() => {}); });
    };
    document.addEventListener('pointerdown', gesture, true);
    document.addEventListener('keydown', gesture, true);
    // Returning to a ringing tab is the one moment the browser may have relaxed on its own.
    document.addEventListener('visibilitychange', () => { if (mode && !document.hidden) void resume(false); });

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
        const token = ++generation;
        starting++;
        try {
            stop(false); // A second invitation replaces the first ring; rings never stack.
            const { tone: id, volume } = read();
            // The personal ringtone belongs to the person being called. A caller gets ringback,
            // so the tone they picked never doubles as "the phone at the other end is ringing".
            const tone = wanted === 'ringback' ? RINGBACK : find(id);
            mode = wanted;
            // Vibration is scheduled before any audio wait: navigator.vibrate needs no autoplay
            // permission, so it must not be held hostage by a resume() that may never answer.
            const vibrating = wanted === 'ringtone' && buzz(tone);
            // Ringback ignores the ring-volume dial. That dial answers "how loudly should this
            // phone shout at me from across the room", which is the opposite of a caller holding
            // it to an ear; loudness there belongs to the device's own volume buttons. Zero is
            // still zero, because "off" is the one promise the setting has to keep.
            const level = wanted === 'ringback' ? (volume > 0 ? tone.level : 0) : volume * volume * tone.level;
            const audible = level > 0 && await resume();
            // Stopped, or replaced by a newer ring, while the context was resuming.
            if (generation !== token || mode !== wanted) return { audible: false, vibrating: false };
            if (audible) {
                master = context.createGain();
                master.gain.value = 1;
                master.connect(context.destination);
            }
            let repeat = false;
            const emit = () => { if (audible && master) play(tone, level, context.currentTime + .06); if (vibrating && repeat) buzz(tone); repeat = true; };
            emit();
            if (wanted === 'preview') ending = setTimeout(stop, tone.period * 1000);
            else if (audible || vibrating) cycle = setInterval(emit, tone.period * 1000);
            window.yap?.diagnostics?.record('call.ring', { phase: wanted, tone: tone.id, audible, vibrating, state: context?.state ?? 'none' });
            return { audible, vibrating };
        }
        finally { starting--; }
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
