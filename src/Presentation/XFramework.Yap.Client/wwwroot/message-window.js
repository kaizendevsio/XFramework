// One owner of scroll position. Anchor corrections use the current scrollTop,
// never a position saved by an earlier scroll event or asynchronous render.
(() => {
    const states = new WeakMap();
    const reduced = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
    const prefix = state => {
        state.offsets = [0];
        for (const id of state.ids) state.offsets.push(state.offsets.at(-1) + (state.heights.get(id) || 100));
    };
    const rowAt = (state, y) => {
        let low = 0, high = state.ids.length;
        while (low < high) { const mid = (low + high) >>> 1; if (state.offsets[mid + 1] < y) low = mid + 1; else high = mid; }
        return Math.min(low, Math.max(0, state.ids.length - 1));
    };
    const origin = state => (parseFloat(getComputedStyle(state.element).paddingTop) || 0) + state.element.querySelector('[data-window-lead]').getBoundingClientRect().height;
    const sample = state => {
        const position = state.element.scrollTop;
        if (!state.hasNewer && position > state.lastTop + .5 && state.element.scrollHeight - position - state.element.clientHeight < 8) state.pinned = true;
        state.lastTop = position;
    };
    const move = (state, position) => {
        if (Math.abs(state.element.scrollTop - position) > .5) state.element.scrollTop = position;
        state.lastTop = state.element.scrollTop;
    };
    const spacers = state => {
        // The scroll extent survives a Blazor window replacement. In-flow rows and
        // old spacers briefly collapsed it, causing the browser to clamp scrollTop.
        state.element.querySelector('[data-window-canvas]').style.height = `${state.offsets.at(-1)}px`;
        for (const row of state.element.querySelectorAll('[data-window-row]')) {
            const index = state.ids.indexOf(row.dataset.windowRow);
            row.style.top = `${state.offsets[index] || 0}px`;
            row.dataset.positioned = '';
        }
    };
    const request = state => {
        if (!state.ids.length || state.requesting || !state.element.isConnected) return;
        const y = Math.max(0, state.element.scrollTop - state.origin);
        const index = rowAt(state, y), last = rowAt(state, y + state.element.clientHeight);
        // Keep a buffer and only replace the window near its edges. Moving it on
        // every row constantly remounts photos and interrupts momentum scrolling.
        if (index < state.start + 4 && state.start > 0 || last >= state.start + state.count - 4 && state.start + state.count < state.ids.length) {
            const start = Math.max(0, index - 12), count = Math.min(80, state.ids.length - start, Math.max(40, last - start + 18));
            state.requesting = true;
            state.ref.invokeMethodAsync('WindowChanged', start, count).catch(() => {}).finally(() => { state.requesting = false; schedule(state); });
        }
        if (state.hasMore && index < 8 && !state.pinned && state.loadedBefore !== state.ids[0]) {
            state.loadedBefore = state.ids[0];
            state.ref.invokeMethodAsync('LoadEarlier').catch(() => {});
        }
        if (state.hasNewer && last >= state.ids.length - 8 && state.loadedAfter !== state.ids.at(-1)) {
            state.loadedAfter = state.ids.at(-1);
            state.ref.invokeMethodAsync('LoadNewer').catch(() => {});
        }
    };
    const schedule = state => {
        if (state.frame || !state.element.isConnected) return;
        state.frame = requestAnimationFrame(() => {
            state.frame = 0;
            if (!state.element.isConnected) return;
            request(state);
            // Scroll stays entirely in JS. Coalesce read checks rather than
            // crossing WASM/JS (and scanning rectangles) on every animation frame.
            if (!state.readTimer) state.readTimer = setTimeout(() => {
                state.readTimer = 0;
                if (state.element.isConnected && state.element.getClientRects().length && !document.hidden && !state.reading) {
                    state.reading = true;
                    state.ref.invokeMethodAsync('ReadVisible').catch(() => {}).finally(() => { state.reading = false; });
                }
            }, 250);
        });
    };
    // A recycled row carries an id the previous list already held, so only ids appended
    // past the old tail were just sent or received. Prepended history and window moves
    // never replay entry, which is the regression #496 removed the CSS animation for.
    const entering = (state, previous) => {
        if (!previous.length || reduced()) return;
        const from = state.ids.indexOf(previous.at(-1));
        if (from < 0) return;
        const known = new Set(previous), fresh = new Set(state.ids.slice(from + 1).filter(id => !known.has(id)));
        if (!fresh.size || fresh.size > 3) return; // a page of newer history is not an arrival
        for (const row of state.element.querySelectorAll('[data-window-row]')) {
            const bubble = fresh.has(row.dataset.windowRow) ? row.querySelector?.('.msg') : null;
            if (!bubble) continue;
            // Transform and opacity only: heights are already measured and the pinned
            // scroll anchor above would shift if entry touched layout. Outgoing springs
            // up out of the composer corner; incoming settles in from the sender side.
            const out = bubble.classList.contains('out'), origin = out ? '100% 100%' : '0 100%';
            bubble.animate([
                { opacity: 0, transform: out ? 'translateY(18px) scale(.82)' : 'translateY(10px) scale(.94)', transformOrigin: origin },
                { opacity: 1, transform: 'none', transformOrigin: origin }],
                { duration: out ? 300 : 240, easing: out ? 'cubic-bezier(.2,.9,.3,1.18)' : 'cubic-bezier(.22,1,.36,1)' });
        }
    };
    // Overscroll band. Translating the two in-flow children never re-measures a height,
    // so neither the ResizeObserver nor the anchor correction sees the gesture.
    const edge = (state, dy) => dy > 0 && state.element.scrollTop <= 0 ? 1
        : dy < 0 && state.element.scrollHeight - state.element.scrollTop - state.element.clientHeight <= 1 ? -1 : 0;
    const pull = (state, raw) => {
        const offset = Math.sign(raw) * Math.min(92, Math.abs(raw) ** .82 * .9);
        if (offset === state.band) return;
        state.band = offset;
        state.element.setAttribute('data-banding', '');
        state.element.style.setProperty('--band', `${offset}px`);
    };
    const release = state => {
        state.bandFrom = state.bandEdge = state.bandRaw = 0;
        if (!state.band) return;
        state.band = 0;
        state.element.removeAttribute('data-banding'); // the spring back is the CSS transition
        state.element.style.setProperty('--band', '0px');
    };
    const layout = (state, next) => {
        if (!state.element.getClientRects().length) return;
        sample(state); // Native scrolling may already have moved before its event arrives.
        const previous = state.ids;
        const position = state.element.scrollTop;
        const index = rowAt(state, Math.max(0, position - state.origin));
        const anchor = state.ids[index], oldOffset = state.offsets[index] || 0, oldOrigin = state.origin;
        if (next) {
            if (state.ids[0] !== next.ids[0]) state.loadedBefore = undefined;
            if (state.ids.at(-1) !== next.ids.at(-1)) state.loadedAfter = undefined;
            Object.assign(state, next, { ids: [...next.ids] });
            const retained = new Set(state.ids);
            for (const id of state.heights.keys()) if (!retained.has(id)) state.heights.delete(id);
            if (state.hasNewer) state.pinned = false;
        }
        for (const row of state.element.querySelectorAll('[data-window-row]')) {
            const height = row.getBoundingClientRect().height;
            if (height) state.heights.set(row.dataset.windowRow, height);
        }
        prefix(state); state.origin = origin(state); spacers(state);
        if (state.pinned) move(state, state.element.scrollHeight - state.element.clientHeight);
        else {
            const current = state.ids.indexOf(anchor);
            // Only changed content above the reader merits a correction. No-op
            // renders, images below them and footer resizing must not reset scrollTop.
            if (current >= 0) move(state, position + state.offsets[current] - oldOffset + state.origin - oldOrigin);
        }
        if (next) entering(state, previous);
        schedule(state);
    };
    const receipts = state => {
        const previous = state.receipts || new Map(), current = new Map();
        const scroll = state.element.scrollTop, viewport = state.element.getBoundingClientRect();
        const animate = !matchMedia('(prefers-reduced-motion: reduce)').matches;
        // Batch geometry reads before any animation writes. Coordinates include
        // scroll offset so ordinary scrolling never animates a receipt.
        for (const element of state.element.querySelectorAll('[data-reader-id]')) {
            const id = element.dataset.readerId, message = element.closest('[data-message-id]').dataset.messageId;
            const old = previous.get(id);
            if (old?.element === element && element.getAnimations().length) { current.set(id, old); continue; }
            const box = element.getBoundingClientRect();
            current.set(id, { element, message, x: box.left, y: box.top + scroll, top: box.top });
        }
        for (const [id, to] of current) {
            const from = previous.get(id);
            if (!animate || !from || from.message === to.message || to.top < viewport.top || to.top > viewport.bottom || from.y - scroll < viewport.top) continue;
            to.element.animate([{ transform: `translate(${from.x - to.x}px,${from.y - to.y}px)` }, { transform: 'none' }],
                { duration: 320, easing: 'cubic-bezier(.22,1,.36,1)' });
        }
        state.receipts = current;
    };
    window.yap.messageWindow = {
        sync(element, ref, ids, start, count, hasMore, hasNewer = false) {
            let state = states.get(element);
            if (!state) {
                state = { element, ref, ids: [], heights: new Map(), offsets: [0], origin: origin({ element }), lastTop: element.scrollTop, pinned: true, observed: new Set() };
                state.scroll = () => { sample(state); schedule(state); };
                const releaseBottom = () => { state.pinned = false; };
                state.wheel = event => {
                    if (event.deltaY < 0) releaseBottom();
                    if (reduced() || !edge(state, -event.deltaY)) { release(state); return; }
                    event.preventDefault(); // only past an end, so ordinary wheeling stays native
                    pull(state, state.bandRaw = Math.max(-260, Math.min(260, (state.bandRaw || 0) - event.deltaY)));
                    clearTimeout(state.bandTimer);
                    state.bandTimer = setTimeout(() => release(state), 140);
                };
                state.touchStart = event => { state.touchY = state.startY = event.touches[0]?.clientY; state.startX = event.touches[0]?.clientX; release(state); };
                state.touchMove = event => {
                    const y = event.touches[0]?.clientY, dy = y - state.touchY;
                    if (y > state.touchY + 1) releaseBottom();
                    state.touchY = y;
                    if (reduced() || event.touches.length > 1) return;
                    // A horizontal drag belongs to the swipe-to-reply gesture in motion.js.
                    if (Math.abs(event.touches[0].clientX - state.startX) > Math.abs(y - state.startY)) { release(state); return; }
                    if (!state.bandEdge) { state.bandEdge = edge(state, dy); state.bandFrom = y - dy; }
                    if (!state.bandEdge) return;
                    const raw = y - state.bandFrom;
                    if (raw * state.bandEdge <= 0) { release(state); return; } // dragged back into the content
                    event.preventDefault();
                    pull(state, raw);
                };
                state.touchEnd = () => release(state);
                state.key = event => { if (['ArrowUp', 'PageUp', 'Home'].includes(event.key)) releaseBottom(); };
                state.pointer = event => { if (event.pointerType === 'mouse' && event.target === element) releaseBottom(); };
                state.visibility = () => schedule(state);
                state.resize = new ResizeObserver(() => layout(state));
                element.addEventListener('scroll', state.scroll, { passive: true });
                element.addEventListener('wheel', state.wheel, { passive: false });
                element.addEventListener('touchstart', state.touchStart, { passive: true });
                element.addEventListener('touchmove', state.touchMove, { passive: false });
                element.addEventListener('touchend', state.touchEnd, { passive: true });
                element.addEventListener('touchcancel', state.touchEnd, { passive: true });
                element.addEventListener('keydown', state.key);
                element.addEventListener('pointerdown', state.pointer);
                document.addEventListener('visibilitychange', state.visibility);
                state.resize.observe(element); states.set(element, state);
            }
            for (const row of state.observed) if (!row.isConnected) { state.resize.unobserve(row); state.observed.delete(row); }
            for (const row of element.querySelectorAll('[data-window-row]')) if (!state.observed.has(row)) { state.observed.add(row); state.resize.observe(row); }
            layout(state, { ids, start, count, hasMore, hasNewer });
            if (element.getClientRects().length) receipts(state);
        },
        bottom(element) { const state = states.get(element); if (state) { state.pinned = true; move(state, element.scrollHeight - element.clientHeight); schedule(state); } },
        show(element, id) {
            const state = states.get(element), index = state?.ids.indexOf(id);
            if (index === undefined || index < 0) return;
            state.pinned = false; move(state, state.origin + state.offsets[index] - 80); schedule(state);
        },
        resize(element) { const state = states.get(element); if (state) layout(state); },
        dispose(element) {
            const state = states.get(element); if (!state) return;
            state.resize.disconnect(); cancelAnimationFrame(state.frame); clearTimeout(state.readTimer); clearTimeout(state.bandTimer); states.delete(element);
            element.removeEventListener('scroll', state.scroll); element.removeEventListener('wheel', state.wheel);
            element.removeEventListener('touchstart', state.touchStart); element.removeEventListener('touchmove', state.touchMove);
            element.removeEventListener('touchend', state.touchEnd); element.removeEventListener('touchcancel', state.touchEnd);
            element.removeEventListener('keydown', state.key); document.removeEventListener('visibilitychange', state.visibility);
            element.removeEventListener('pointerdown', state.pointer);
        }
    };
})();
