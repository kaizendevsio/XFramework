(() => {
    const viewers = new WeakMap();
    const clamp = (value, min, max) => Math.min(max, Math.max(min, value));
    const paint = state => {
        const box = state.base;
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
        open(dialog, ref, sourceId) {
            const state = { stage: dialog.querySelector('.photo-stage'), image: dialog.querySelector('img'), shade: dialog.querySelector('.photo-shade'), toolbar: dialog.querySelector('.photo-toolbar'), points: new Map(), scale: 1, x: 0, y: 0, lastTap: 0 };
            const controller = new AbortController(); state.controller = controller;
            const on = (target, name, handler, options = {}) => target.addEventListener(name, handler, { ...options, signal: controller.signal });
            const toggle = () => { state.scale = state.scale > 1 ? 1 : 2.5; state.x = state.y = 0; paint(state); };
            const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
            const thumbnailTransform = () => {
                const source = document.getElementById(sourceId)?.querySelector('img');
                if (!source || !state.image.naturalWidth) return null;
                const sourceBox = source.getBoundingClientRect(), to = state.base;
                const sourceFit = Math.min(sourceBox.width / (source.naturalWidth || state.image.naturalWidth), sourceBox.height / (source.naturalHeight || state.image.naturalHeight));
                const width = (source.naturalWidth || state.image.naturalWidth) * sourceFit;
                const height = (source.naturalHeight || state.image.naturalHeight) * sourceFit;
                // Animate the visible image, excluding any object-fit letterboxing.
                const from = { width, height, left: sourceBox.left + (sourceBox.width - width) / 2, top: sourceBox.top + (sourceBox.height - height) / 2 };
                if (!from.width || !to.width) return null;
                const fit = Math.min(to.width / state.image.naturalWidth, to.height / state.image.naturalHeight);
                state.thumbnailScale = from.width / (state.image.naturalWidth * fit);
                return `translate(${from.left + from.width / 2 - to.left - to.width / 2}px, ${from.top + from.height / 2 - to.top - to.height / 2}px) scale(${state.thumbnailScale})`;
            };
            const animateTo = async (transform, opacity, duration = 240) => {
                if (reduced) { state.image.style.transform = transform; state.shade.style.opacity = state.toolbar.style.opacity = opacity; return; }
                const from = getComputedStyle(state.image).transform;
                state.animation?.cancel();
                state.image.style.transform = transform;
                const animations = [state.image.animate([{ transform: from }, { transform }], { duration: reduced ? 0 : duration, easing: 'cubic-bezier(.22,1,.36,1)' })];
                for (const element of [state.shade, state.toolbar]) {
                    animations.push(element.animate([{ opacity: getComputedStyle(element).opacity }, { opacity }], { duration: reduced ? 0 : duration }));
                    element.style.opacity = opacity;
                }
                state.animation = animations[0];
                await Promise.all(animations.map(animation => animation.finished.catch(() => {})));
            };
            state.close = async () => {
                if (state.closing) return;
                state.closing = true;
                // Continue from the dragged or zoomed transform, never reset to center.
                const transform = thumbnailTransform() || `translateY(${state.swipeY || 60}px) scale(.85)`;
                await animateTo(transform, '0');
                if (!controller.signal.aborted) await ref.invokeMethodAsync('CloseAsync').catch(() => {});
            };
            on(dialog, 'cancel', event => { event.preventDefault(); state.close(); });
            on(state.stage, 'pointerdown', event => {
                if (state.closing) return;
                state.animation?.cancel();
                state.points.set(event.pointerId, { x: event.clientX, y: event.clientY });
                state.stage.setPointerCapture(event.pointerId);
                state.previous = gesture(state); state.origin = state.previous; state.moved = state.points.size > 1;
                state.swipe = state.scale === 1 && state.points.size === 1;
                if (state.points.size > 1 && state.swipeY) {
                    state.shade.style.opacity = state.toolbar.style.opacity = '1'; paint(state);
                }
                state.swipeY = 0;
            });
            on(state.stage, 'pointermove', event => {
                if (state.closing || !state.points.has(event.pointerId)) return;
                state.points.set(event.pointerId, { x: event.clientX, y: event.clientY });
                const next = gesture(state), previous = state.previous;
                if (state.points.size > 1) state.swipe = false;
                if (state.swipe && state.points.size === 1 && (state.swipeY || Math.abs(next.y - state.origin.y) > Math.abs(next.x - state.origin.x) * 1.2)) {
                    state.swipeY = next.y - state.origin.y; state.moved = true;
                    const progress = clamp(Math.abs(state.swipeY) / 100, 0, 1);
                    const scale = 1 - progress * (1 - Math.min(1, state.thumbnailScale || .65));
                    state.image.style.transform = `translate(${(next.x - state.origin.x) * .5}px,${state.swipeY}px) scale(${scale})`;
                    state.shade.style.opacity = String(1 - progress * .45);
                    state.toolbar.style.opacity = String(1 - progress);
                    return;
                }
                if (previous) {
                    if (Math.hypot(next.x - state.origin.x, next.y - state.origin.y) > 6) state.moved = true;
                    if (next.distance && previous.distance) state.scale = clamp(state.scale * next.distance / previous.distance, 1, 6);
                    state.x += next.x - previous.x; state.y += next.y - previous.y; paint(state);
                }
                state.previous = next;
            });
            const end = event => {
                if (state.swipe && event.type === 'pointerup' && Math.abs(state.swipeY) >= 80) { state.points.clear(); state.close(); return; }
                if (state.swipeY) { state.swipeY = 0; void animateTo('none', '1', 200); }
                if (event.type === 'pointerup' && !state.moved && event.pointerType === 'touch') {
                    const now = performance.now(); if (state.lastTap && now - state.lastTap < 300) { toggle(); state.lastTap = 0; } else state.lastTap = now;
                }
                state.points.delete(event.pointerId); state.previous = gesture(state);
            };
            on(state.stage, 'pointerup', end); on(state.stage, 'pointercancel', end);
            on(state.stage, 'dblclick', event => { if (event.pointerType !== 'touch') toggle(); });
            on(state.stage, 'wheel', event => { event.preventDefault(); state.scale = clamp(state.scale * Math.exp(-event.deltaY * .002), 1, 6); paint(state); }, { passive: false });
            on(window, 'resize', () => { state.image.style.transform = 'none'; state.base = state.image.getBoundingClientRect(); thumbnailTransform(); paint(state); });
            viewers.set(dialog, state); dialog.showModal();
            state.base = state.image.getBoundingClientRect();
            const enter = () => {
                if (controller.signal.aborted || state.closing || reduced) return;
                const transform = thumbnailTransform();
                if (transform) {
                    state.image.style.transform = transform;
                    state.shade.style.opacity = state.toolbar.style.opacity = '0';
                    void animateTo('none', '1', 280);
                }
            };
            if (state.image.complete) enter(); else on(state.image, 'load', enter, { once: true });
        },
        close(dialog) { return viewers.get(dialog)?.close(); },
        zoom(dialog, direction) { const state = viewers.get(dialog); if (state) { state.scale = clamp(state.scale + direction * .5, 1, 6); paint(state); } },
        reset(dialog) { const state = viewers.get(dialog); if (state) { state.scale = 1; state.x = state.y = 0; paint(state); } },
        dispose(dialog) { const state = viewers.get(dialog); state?.controller.abort(); state?.animation?.cancel(); if (dialog?.open) dialog.close(); viewers.delete(dialog); }
    };
})();
