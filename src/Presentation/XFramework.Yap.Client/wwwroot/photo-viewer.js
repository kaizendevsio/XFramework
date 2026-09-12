(() => {
    const viewers = new WeakMap();
    const clamp = (value, min, max) => Math.min(max, Math.max(min, value));
    const paint = state => {
        const box = state.stage.getBoundingClientRect();
        state.x = clamp(state.x, -box.width * (state.scale - 1) / 2, box.width * (state.scale - 1) / 2);
        state.y = clamp(state.y, -box.height * (state.scale - 1) / 2, box.height * (state.scale - 1) / 2);
        state.image.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.scale})`;
    };
    const gesture = state => {
        const points = [...state.points.values()];
        if (!points.length) return null;
        const [a, b = a] = points;
        return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2, distance: points.length > 1 ? Math.hypot(a.x - b.x, a.y - b.y) : 0 };
    };
    window.yap.photoViewer = {
        open(dialog, ref) {
            const state = { stage: dialog.querySelector('.photo-stage'), image: dialog.querySelector('img'), points: new Map(), scale: 1, x: 0, y: 0, lastTap: 0 };
            const controller = new AbortController(); state.controller = controller;
            const on = (target, name, handler, options = {}) => target.addEventListener(name, handler, { ...options, signal: controller.signal });
            const toggle = () => { state.scale = state.scale > 1 ? 1 : 2.5; state.x = state.y = 0; paint(state); };
            on(dialog, 'cancel', event => { event.preventDefault(); ref.invokeMethodAsync('CloseAsync').catch(() => {}); });
            on(state.stage, 'pointerdown', event => {
                state.points.set(event.pointerId, { x: event.clientX, y: event.clientY });
                state.stage.setPointerCapture(event.pointerId);
                state.previous = gesture(state); state.origin = state.previous; state.moved = state.points.size > 1;
            });
            on(state.stage, 'pointermove', event => {
                if (!state.points.has(event.pointerId)) return;
                state.points.set(event.pointerId, { x: event.clientX, y: event.clientY });
                const next = gesture(state), previous = state.previous;
                if (previous) {
                    if (Math.hypot(next.x - state.origin.x, next.y - state.origin.y) > 6) state.moved = true;
                    if (next.distance && previous.distance) state.scale = clamp(state.scale * next.distance / previous.distance, 1, 6);
                    state.x += next.x - previous.x; state.y += next.y - previous.y; paint(state);
                }
                state.previous = next;
            });
            const end = event => {
                if (event.type === 'pointerup' && !state.moved && event.pointerType === 'touch') {
                    const now = performance.now(); if (state.lastTap && now - state.lastTap < 300) { toggle(); state.lastTap = 0; } else state.lastTap = now;
                }
                state.points.delete(event.pointerId); state.previous = gesture(state);
            };
            on(state.stage, 'pointerup', end); on(state.stage, 'pointercancel', end);
            on(state.stage, 'dblclick', event => { if (event.pointerType !== 'touch') toggle(); });
            on(state.stage, 'wheel', event => { event.preventDefault(); state.scale = clamp(state.scale * Math.exp(-event.deltaY * .002), 1, 6); paint(state); }, { passive: false });
            on(window, 'resize', () => paint(state));
            viewers.set(dialog, state); dialog.showModal();
        },
        zoom(dialog, direction) { const state = viewers.get(dialog); if (state) { state.scale = clamp(state.scale + direction * .5, 1, 6); paint(state); } },
        reset(dialog) { const state = viewers.get(dialog); if (state) { state.scale = 1; state.x = state.y = 0; paint(state); } },
        dispose(dialog) { const state = viewers.get(dialog); state?.controller.abort(); if (dialog?.open) dialog.close(); viewers.delete(dialog); }
    };
})();
