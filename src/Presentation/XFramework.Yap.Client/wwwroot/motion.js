// Native View Transitions bracket the Blazor render; Pointer Events reuse the
// same buttons as keyboard/mouse input. No duplicate messaging state in JS.
(() => {
    const reduced = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
    let pending = null;
    const depth = url => {
        const path = new URL(url, location.href).pathname;
        if (/^\/chat\/[^/]+\/details$/.test(path) || /^\/settings\//.test(path)) return 2;
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
        // The window exists to eat the click that trails a long press on the page beneath.
        // A dialog the press just opened is new DOM, reachable only by a deliberate tap.
        if (e.detail !== 0 && performance.now() < suppressUntil && !e.target.closest?.('dialog[open]')) {
            e.preventDefault(); e.stopImmediatePropagation();
        }
    }, true);
    document.addEventListener('pointerdown', e => {
        if (e.target.closest('.conversation-menu')) { suppressUntil = 0; clear(); return; }
        if (e.pointerType === 'mouse' || !e.isPrimary || e.button !== 0) { clear(); return; }
        if (e.target.closest('input,textarea,select,.photo-viewer')) return;
        clear();
        const handle = e.target.closest('[data-sheet-drag]');
        const bubble = e.target.closest('.bub,.photo-open');
        const message = bubble?.closest('[data-swipe-reply]');
        const panel = e.target.closest('[data-swipe-tabs]');
        const conversation = e.target.closest('[data-conversation-menu]');
        const element = handle?.closest('.sheet') || message || conversation || panel;
        if (!element) return;
        const g = gesture = {element, bubble, kind:handle?'sheet':message?'message':conversation?'conversation':'tabs', id:e.pointerId, x:e.clientX, y:e.clientY, dx:0, dy:0, axis:'pending', fired:false, ready:false, timer:null};
        if (g.kind === 'message' || g.kind === 'conversation') {
            g.timer = setTimeout(() => {
                if (gesture !== g || g.axis !== 'pending') return;
                g.fired = true;
                // The hold has been recognised. That pulse is the answer, so the menu it opens
                // a render later stays silent rather than landing a second one on top.
                window.yap.haptics?.buzz('press');
                window.yap.haptics?.mute(600);
                element.classList.add('is-holding');
                suppressClick();
                if (g.kind === 'conversation') element.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX:g.x, clientY:g.y }));
                else if (bubble.matches('.photo-open')) bubble.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true }));
                else bubble.click();
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
        if (g.kind === 'conversation' || g.axis !== 'horizontal') return;
        e.preventDefault();
        g.element.classList.add('is-dragging');
        if (g.kind === 'message') {
            g.element.style.setProperty('--reply-drag', Math.max(0,Math.min(84,g.dx * .75)) + 'px');
            // Fire on the rising edge only. pointermove runs every frame; the reply commits once,
            // the moment the drag crosses the threshold, which is where the tap belongs.
            const ready = g.dx >= 60;
            if (ready && !g.ready) window.yap.haptics?.buzz('tap');
            g.ready = ready;
            g.element.classList.toggle('gesture-ready',ready);
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
    document.addEventListener('contextmenu', e => { if (e.target.closest('.bub,.photo-open') && gesture) e.preventDefault(); });

    // Blazor removes conditional DOM immediately. Retain only a short-lived,
    // inert visual copy for the exit; actions and state never wait for animation.
    const retired = new WeakSet(), exitPositions = new Map();
    const retire = element => {
        if (reduced() || document.hidden || element.hasAttribute('data-exit-ghost') || retired.has(element)) return;
        retired.add(element);
        const modal = element.matches('.message-menu-dialog,.sheet-dialog');
        const copy = document.createElement('div');
        for (const attribute of element.attributes) copy.setAttribute(attribute.name, attribute.value);
        copy.removeAttribute('id'); copy.removeAttribute('role');
        copy.setAttribute('aria-hidden', 'true'); copy.inert = true;
        copy.dataset.exitGhost = ''; copy.setAttribute('open', '');
        copy.innerHTML = element.innerHTML;
        for (const node of copy.querySelectorAll('[id]')) node.removeAttribute('id');
        if (modal) {
            const backdrop = document.createElement('div'); backdrop.className = 'exit-backdrop'; copy.prepend(backdrop);
            document.body.append(copy);
        } else if (element.matches('.replyto,.conversation-menu')) {
            const box = exitPositions.get(element); if (!box) return;
            Object.assign(copy.style, { position: 'fixed', left: `${box.left}px`, top: `${box.top}px`, width: `${box.width}px`, height: `${box.height}px`, margin: '0' });
            document.body.append(copy); exitPositions.delete(element);
        } else {
            const host = document.querySelector('.toast-host'); if (!host) return;
            host.append(copy);
        }
        const content = copy.querySelector('.message-menu-stack,.sheet') || copy;
        const to = element.matches('.sheet-dialog') ? 'translateY(32px)' : 'scale(.96)';
        const animation = content.animate([{ opacity: 1, transform: 'none' }, { opacity: 0, transform: to }], { duration: 180, easing: 'ease-in', fill: 'forwards' });
        copy.querySelector('.exit-backdrop')?.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 180, fill: 'forwards' });
        animation.finished.catch(() => {}).finally(() => copy.remove());
    };
    const observe = () => {
        const app = document.getElementById('app'); if (!app) return;
        const composers = new Map();
        const resize = new ResizeObserver(entries => {
            for (const { target } of entries) {
                const before = composers.get(target), box = target.getBoundingClientRect();
                if (!box.height) continue;
                const controls = [...target.querySelectorAll(':scope > .inputbox,:scope > .attach-open,:scope > .composer-action')];
                const after = { box, controls: controls.map(element => ({ element, box: element.getBoundingClientRect() })) };
                composers.set(target, after);
                if (!before || reduced() || Math.abs(before.box.height - box.height) < 1) continue;
                // One layout commit, then FLIP only the backdrop and controls.
                // Text never scales and no grid/height/padding interpolates per frame.
                const optics = target.querySelector(':scope > .glass-optics');
                if (optics) {
                    optics.style.transformOrigin = '0 0';
                    optics.animate([{ transform: `translate(${before.box.left-box.left}px,${before.box.top-box.top}px) scale(${before.box.width/box.width},${before.box.height/box.height})` }, { transform: 'none' }], { duration: 220, easing: 'cubic-bezier(.22,1,.36,1)' });
                }
                for (const item of after.controls) {
                    const old = before.controls.find(x => x.element === item.element); if (!old) continue;
                    item.element.animate([{ transform: `translate(${old.box.left-item.box.left}px,${old.box.top-item.box.top}px)` }, { transform: 'none' }], { duration: 220, easing: 'cubic-bezier(.22,1,.36,1)' });
                }
            }
        });
        const syncComposers = () => {
            for (const element of exitPositions.keys()) if (!element.isConnected) exitPositions.delete(element);
            for (const element of app.querySelectorAll('.replyto,.conversation-menu')) exitPositions.set(element, element.getBoundingClientRect());
            for (const element of composers.keys()) if (!element.isConnected) { resize.unobserve(element); composers.delete(element); }
            for (const element of app.querySelectorAll('.inputrow')) if (!composers.has(element)) { composers.set(element, null); resize.observe(element); }
        };
        let syncFrame;
        new MutationObserver(records => {
            for (const record of records) for (const node of record.removedNodes)
                if (node.nodeType === 1 && node.matches('.message-menu-dialog,.sheet-dialog,.toast,.replyto,.conversation-menu')) retire(node);
            if (!syncFrame) syncFrame = requestAnimationFrame(() => { syncFrame = 0; syncComposers(); });
        }).observe(app, { childList: true, subtree: true });
        syncComposers();
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', observe, { once: true }); else observe();
})();
