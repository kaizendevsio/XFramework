// One owner of scroll position. Anchor corrections use the current scrollTop,
// never a position saved by an earlier scroll event or asynchronous render.
(() => {
    const states = new WeakMap();
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
            if (!state.reading) {
                state.reading = true;
                state.readAgain = false;
                state.ref.invokeMethodAsync('ReadVisible').catch(() => {}).finally(() => { state.reading = false; if (state.readAgain) schedule(state); });
            }
            else state.readAgain = true;
        });
    };
    const layout = (state, next) => {
        sample(state); // Native scrolling may already have moved before its event arrives.
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
        schedule(state);
    };
    window.yap.messageWindow = {
        sync(element, ref, ids, start, count, hasMore, hasNewer = false) {
            let state = states.get(element);
            if (!state) {
                state = { element, ref, ids: [], heights: new Map(), offsets: [0], origin: origin({ element }), lastTop: element.scrollTop, pinned: true, observed: new Set() };
                state.scroll = () => { sample(state); schedule(state); };
                const releaseBottom = () => { state.pinned = false; };
                state.wheel = event => { if (event.deltaY < 0) releaseBottom(); };
                state.touchStart = event => { state.touchY = event.touches[0]?.clientY; };
                state.touchMove = event => { const y = event.touches[0]?.clientY; if (y > state.touchY + 1) releaseBottom(); state.touchY = y; };
                state.key = event => { if (['ArrowUp', 'PageUp', 'Home'].includes(event.key)) releaseBottom(); };
                state.pointer = event => { if (event.pointerType === 'mouse' && event.target === element) releaseBottom(); };
                state.visibility = () => schedule(state);
                state.resize = new ResizeObserver(() => layout(state));
                element.addEventListener('scroll', state.scroll, { passive: true });
                element.addEventListener('wheel', state.wheel, { passive: true });
                element.addEventListener('touchstart', state.touchStart, { passive: true });
                element.addEventListener('touchmove', state.touchMove, { passive: true });
                element.addEventListener('keydown', state.key);
                element.addEventListener('pointerdown', state.pointer);
                document.addEventListener('visibilitychange', state.visibility);
                state.resize.observe(element); states.set(element, state);
            }
            for (const row of state.observed) if (!row.isConnected) { state.resize.unobserve(row); state.observed.delete(row); }
            for (const row of element.querySelectorAll('[data-window-row]')) if (!state.observed.has(row)) { state.observed.add(row); state.resize.observe(row); }
            layout(state, { ids, start, count, hasMore, hasNewer });
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
            state.resize.disconnect(); cancelAnimationFrame(state.frame); states.delete(element);
            element.removeEventListener('scroll', state.scroll); element.removeEventListener('wheel', state.wheel);
            element.removeEventListener('touchstart', state.touchStart); element.removeEventListener('touchmove', state.touchMove);
            element.removeEventListener('keydown', state.key); document.removeEventListener('visibilitychange', state.visibility);
            element.removeEventListener('pointerdown', state.pointer);
        }
    };
})();
