// A tap you feel, never a buzz you hear: one pulse of a few milliseconds when a surface
// appears or a gesture commits. Patterns belong to ringing (call-audio.js), not to menus.
// navigator.vibrate does not exist on iOS Safari - on any iPhone or iPad, including an
// installed PWA - and nothing can polyfill it, so every call there no-ops silently.
(() => {
    // 8ms is at the edge of perception on a phone motor; 14ms is the most a menu may ask for.
    const pulses = { tap: 8, press: 12, commit: 14 };
    const key = 'yap-haptics';
    // One gesture can raise two surfaces (a long press fires, then the menu opens). The
    // person felt one thing, so a second pulse inside this window is dropped rather than
    // stacked into a buzz. It also caps repeat-fire from any caller that runs per frame.
    const gap = 60;
    let last = -Infinity, quiet = 0, enabled = true;
    try { enabled = localStorage.getItem(key) !== 'off'; } catch {}
    const supported = () => typeof navigator?.vibrate === 'function';
    const reduced = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
    window.yap.haptics = {
        supported,
        enabled: () => enabled,
        setEnabled(value) {
            enabled = value === true;
            // On is the default, so it is the absence of a stored choice - same shape as the theme.
            try { enabled ? localStorage.removeItem(key) : localStorage.setItem(key, 'off'); } catch {}
            if (!enabled) { try { navigator.vibrate?.(0); } catch {} }
            return enabled;
        },
        // A gesture that has already spoken owns whatever it raises: the menu that opens a
        // render after a long press must not answer with a second pulse of its own.
        mute(ms) { quiet = performance.now() + ms; },
        // Reports whether the device actually moved, so a caller can stay quiet about it.
        buzz(kind) {
            if (!enabled || reduced() || !supported() || document.hidden) return false;
            const now = performance.now();
            if (now < quiet || now - last < gap) return false;
            last = now;
            try { return navigator.vibrate(pulses[kind] ?? pulses.tap) === true; } catch { return false; }
        }
    };
})();
