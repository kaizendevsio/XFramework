// Native View Transitions bracket the Blazor render; Pointer Events reuse the
// same buttons as keyboard/mouse input. No duplicate messaging state in JS.
(() => {
    const reduced = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
    let pending = null;
    const depth = url => {
        const route = new URL(url, location.href).pathname.replace(/\.html$/, '').replace(/^\//, '').split('/')[0];
        return ({'':0,dashboard:0,screens:0,login:1,signup:2,settings:1,chat:1,group:1,thread:2,call:2,'video-call':3})[route] ?? 1;
    };
    const complete = () => { if (pending) { clearTimeout(pending.timer); pending.resolve(); pending = null; } };
    window.yap.motion = {
        begin(from, to) {
            if (new URL(from, location.href).pathname === new URL(to, location.href).pathname) return;
            const previous = pending;
            complete();
            previous?.transition?.skipTransition();
            document.documentElement.dataset.navDirection = depth(to) < depth(from) ? 'back' : 'forward';
            if (reduced() || !document.startViewTransition) {
                document.documentElement.classList.toggle('motion-fallback', !reduced());
                return;
            }
            document.documentElement.classList.remove('motion-fallback');
            return new Promise(ready => {
                const entry = { resolve: () => {}, timer: null, transition: null };
                // Do not await transition.finished here: Blazor must be allowed
                // to navigate after the old snapshot and before the new one.
                try {
                    entry.transition = document.startViewTransition(() => new Promise(resolve => {
                        entry.resolve = resolve;
                        pending = entry;
                        entry.timer = setTimeout(complete, 2000);
                        ready();
                    }));
                    entry.transition.ready.catch(() => ready());
                    entry.transition.finished.catch(() => {}).finally(() => {
                        if (pending === entry) complete();
                    });
                } catch { ready(); }
            });
        },
        complete,
        // Kept pure for boundary checks without a browser or device.
        intent(dx, dy) {
            if (Math.max(Math.abs(dx), Math.abs(dy)) < 10) return 'pending';
            return Math.abs(dx) > Math.abs(dy) * 1.3 ? 'horizontal' : 'vertical';
        }
    };

    let gesture = null, suppressUntil = 0;
    const clear = () => {
        if (!gesture) return;
        clearTimeout(gesture.timer);
        gesture.element.classList.remove('is-dragging', 'is-holding', 'gesture-ready');
        gesture.element.style.removeProperty('--reply-drag');
        gesture.element.style.removeProperty('--tab-drag');
        gesture.element.style.removeProperty('--sheet-drag');
        gesture = null;
    };
    const suppressClick = () => { suppressUntil = performance.now() + 650; };
    document.addEventListener('click', e => {
        if (e.detail !== 0 && performance.now() < suppressUntil) {
            e.preventDefault(); e.stopImmediatePropagation();
        }
    }, true);
    document.addEventListener('pointerdown', e => {
        if (e.pointerType === 'mouse' || !e.isPrimary || e.button !== 0) { clear(); return; }
        if (e.target.closest('input,textarea,select')) return;
        clear();
        const handle = e.target.closest('[data-sheet-drag]');
        const bubble = e.target.closest('.bub');
        const message = bubble?.closest('[data-swipe-reply]');
        const panel = e.target.closest('[data-swipe-tabs]');
        const element = handle?.closest('.sheet') || message || panel;
        if (!element) return;
        const g = gesture = {element, bubble, kind:handle?'sheet':message?'message':'tabs', id:e.pointerId, x:e.clientX, y:e.clientY, dx:0, dy:0, axis:'pending', fired:false, timer:null};
        if (g.kind === 'message') {
            g.timer = setTimeout(() => {
                if (gesture !== g || g.axis !== 'pending') return;
                g.fired = true;
                element.classList.add('is-holding');
                suppressClick();
                bubble.click();
            }, 450);
        }
        if (handle) element.setPointerCapture(e.pointerId);
    });
    document.addEventListener('pointermove', e => {
        const g = gesture;
        if (!g || e.pointerId !== g.id) return;
        g.dx = e.clientX - g.x; g.dy = e.clientY - g.y;
        if (g.kind === 'sheet') {
            e.preventDefault();
            g.element.classList.add('is-dragging');
            g.element.style.setProperty('--sheet-drag', Math.max(0, g.dy) + 'px');
            return;
        }
        if (g.axis === 'pending') g.axis = yap.motion.intent(g.dx,g.dy);
        if (g.axis !== 'pending') clearTimeout(g.timer);
        if (g.axis === 'vertical' || g.fired) { clear(); return; }
        if (g.axis !== 'horizontal') return;
        e.preventDefault();
        g.element.classList.add('is-dragging');
        if (g.kind === 'message') {
            g.element.style.setProperty('--reply-drag', Math.max(0,Math.min(84,g.dx * .75)) + 'px');
            g.element.classList.toggle('gesture-ready',g.dx >= 60);
        } else g.element.style.setProperty('--tab-drag',Math.max(-28,Math.min(28,g.dx * .3)) + 'px');
    }, {passive:false});
    document.addEventListener('pointerup', e => {
        const g = gesture;
        if (!g || e.pointerId !== g.id) return;
        if (g.fired || g.axis === 'horizontal' || (g.kind === 'sheet' && g.dy > 10)) suppressClick();
        if (!g.fired && g.kind === 'message' && g.axis === 'horizontal' && g.dx >= 60) {
            g.element.querySelector('.gesture-reply')?.click();
        } else if (g.kind === 'tabs' && g.axis === 'horizontal' && Math.abs(g.dx) >= 60) {
            const tabs = [...document.querySelectorAll('.tabs [role="tab"]')];
            const active = tabs.findIndex(tab => tab.getAttribute('aria-selected') === 'true');
            tabs[active + (g.dx < 0 ? 1 : -1)]?.click();
        } else if (g.kind === 'sheet' && g.dy >= 70) {
            g.element.querySelector('[data-sheet-drag]')?.click();
        }
        clear();
    });
    document.addEventListener('pointercancel', clear);
    document.addEventListener('visibilitychange', () => { if (document.hidden) { clear(); complete(); } });
    document.addEventListener('contextmenu', e => { if (e.target.closest('.bub') && gesture) e.preventDefault(); });
})();
