// Variable-height message virtualization. Stable message IDs anchor history; measured
// rows and estimated spacers bound the DOM without assuming all bubbles have one height.
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
    const top = state => (parseFloat(getComputedStyle(state.element).paddingTop) || 0) + state.element.querySelector('[data-window-lead]').getBoundingClientRect().height;
    const remember = state => {
        const element = state.element;
        state.pinned = element.scrollHeight - element.scrollTop - element.clientHeight < 64;
        const index = rowAt(state, Math.max(0, element.scrollTop - top(state)));
        state.anchor = state.ids[index]; state.anchorOffset = element.scrollTop - top(state) - state.offsets[index];
    };
    const restore = state => {
        state.adjusting = true;
        if (state.pinned) state.element.scrollTop = state.element.scrollHeight;
        else {
            const index = state.ids.indexOf(state.anchor);
            if (index >= 0) state.element.scrollTop = top(state) + state.offsets[index] + state.anchorOffset;
        }
        cancelAnimationFrame(state.restoreFrame);
        state.restoreFrame = requestAnimationFrame(() => { state.adjusting = false; });
    };
    const spacers = state => {
        state.element.querySelector('[data-window-top]').style.height = `${state.offsets[state.start] || 0}px`;
        state.element.querySelector('[data-window-bottom]').style.height = `${Math.max(0, state.offsets.at(-1) - state.offsets[Math.min(state.ids.length, state.start + state.count)])}px`;
    };
    const request = state => {
        if (!state.ids.length || state.requesting || !state.element.isConnected) return;
        const y = Math.max(0, state.element.scrollTop - top(state));
        const index = rowAt(state, y);
        const start = Math.max(0, index - 8);
        const end = Math.min(state.ids.length, rowAt(state, y + state.element.clientHeight) + 12);
        if (start !== state.start || end > state.start + state.count || end < state.start + state.count - 12) {
            state.requesting = true;
            state.ref.invokeMethodAsync('WindowChanged', start, Math.min(80, end - start)).catch(() => {}).finally(() => { state.requesting = false; });
        }
        if (state.hasMore && index < 8 && !state.pinned && state.loadedBefore !== state.ids[0]) {
            state.loadedBefore = state.ids[0];
            state.ref.invokeMethodAsync('LoadEarlier').catch(() => {});
        }
    };
    const measure = state => {
        let changed = false;
        for (const row of state.element.querySelectorAll('[data-window-row]')) {
            const height = row.getBoundingClientRect().height;
            if (height && Math.abs(height - (state.heights.get(row.dataset.windowRow) || 0)) > .5) { state.heights.set(row.dataset.windowRow, height); changed = true; }
        }
        if (changed) { prefix(state); spacers(state); restore(state); }
        request(state);
    };
    window.yap.messageWindow = {
        sync(element, ref, ids, start, count, hasMore) {
            let state = states.get(element);
            if (!state) {
                state = { element, ref, ids: [], heights: new Map(), offsets: [0], pinned: true, observed: new Set() };
                state.scroll = () => { if (!state.adjusting) { remember(state); request(state); } };
                state.resize = new ResizeObserver(() => measure(state));
                element.addEventListener('scroll', state.scroll, { passive: true });
                state.resize.observe(element); states.set(element, state);
            }
            Object.assign(state, { ids, start, count, hasMore }); prefix(state);
            for (const row of state.observed) if (!row.isConnected) { state.resize.unobserve(row); state.observed.delete(row); }
            for (const row of element.querySelectorAll('[data-window-row]')) if (!state.observed.has(row)) { state.observed.add(row); state.resize.observe(row); }
            spacers(state); measure(state); restore(state);
            requestAnimationFrame(() => request(state));
        },
        bottom(element) { const state = states.get(element); if (state) { state.pinned = true; restore(state); request(state); } },
        show(element, id) {
            const state = states.get(element), index = state?.ids.indexOf(id);
            if (index === undefined || index < 0) return;
            state.pinned = false; state.anchor = id; state.anchorOffset = -80; restore(state); request(state);
        },
        resize(element) { const state = states.get(element); if (state) { restore(state); request(state); } },
        dispose(element) {
            const state = states.get(element); if (!state) return;
            state.resize.disconnect(); cancelAnimationFrame(state.restoreFrame); element.removeEventListener('scroll', state.scroll); states.delete(element);
        }
    };
})();
