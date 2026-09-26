// Call-screen gestures: tap to show or hide the controls over video, idle auto-hide, the draggable
// self-view that snaps to a corner, tap-to-swap, and fill/fit for remote pictures.
//
// Everything here is written to attributes Blazor never renders (data-chrome, data-corner,
// data-swapped, data-fit, data-orient, inert), so a re-render of the call surface cannot undo it.
// The root is the call <dialog>, which survives the voice/video switch; only its contents change,
// so the handlers are delegated and every lookup is made at the time it is needed.

/** How long the controls stay up over video with nothing happening. */
export const IDLE_MS = 4000;
/** Movement below this is a tap, not a drag. */
export const TAP_SLOP = 8;
/** Two taps closer together than this are one double tap. */
export const DOUBLE_TAP_MS = 280;
/** How far a flick carries the self-view, in ms of its release velocity. */
export const FLICK_MS = 180;

/**
 * Fill or fit. Cover when the picture and the box share an orientation, so the crop only trims an
 * edge - a portrait phone camera on a portrait phone. A landscape camera on a portrait screen would
 * lose more than half the frame, so it is shown whole instead: a call must not silently crop
 * someone out of their own picture. A double tap flips it either way.
 */
export function fitFor(frameWidth, frameHeight, boxWidth, boxHeight, tile = false) {
    if (!(frameWidth > 0 && frameHeight > 0 && boxWidth > 0 && boxHeight > 0)) return 'contain';
    const frame = frameWidth / frameHeight, box = boxWidth / boxHeight, stretch = Math.max(frame / box, box / frame);
    // A group tile is a glimpse, not the stage: black bars around three of four people read as
    // broken, so a tile fills unless the crop would be extreme (a 16:9 sender in a narrow column).
    if (tile) return stretch <= 2.4 ? 'cover' : 'contain';
    if ((frame >= 1) !== (box >= 1)) return 'contain';
    return stretch <= 2 ? 'cover' : 'contain';
}

/** The corner a point is closest to, within a bounds of the given size. */
export function nearestCorner(x, y, width, height) {
    return (y < height / 2 ? 't' : 'b') + (x < width / 2 ? 'l' : 'r');
}

/** Where a released self-view is heading: its centre, carried on by its velocity (px/ms). */
export function projectRelease(x, y, velocityX, velocityY, carry = FLICK_MS) {
    return { x: x + velocityX * carry, y: y + velocityY * carry };
}

/**
 * Whether the controls may hide by themselves. Only over someone's live video, never while a menu
 * or a notice is up, never for keyboard users (hidden controls are unreachable to them), and only
 * after a quiet spell.
 */
export function shouldAutoHide({ video, remoteVideo, holding, keyboard, idleMs }) {
    return !!video && !!remoteVideo && !holding && !keyboard && idleMs >= IDLE_MS;
}

const states = new WeakMap();
const HOLD = '[data-call-hold],.video-debug,[aria-expanded="true"]';
const INTERACTIVE = 'button,a,input,select,textarea,label,[data-call-hold],.video-debug';

function reducedMotion() {
    try { return matchMedia('(prefers-reduced-motion: reduce)').matches; } catch { return false; }
}

export function mount(root) {
    if (!root || states.has(root)) return;
    const state = {
        last: Date.now(), timer: 0, tapTimer: 0, lastTap: null, drag: null,
        observer: null, resize: null,
    };
    states.set(root, state);
    const screen = () => root.querySelector('.vidscreen');
    // The surface that floats in a corner. Nothing floats while your own camera is the whole stage.
    const small = () => root.querySelector('.vidscreen.self-stage') ? null
        : root.hasAttribute('data-swapped') ? root.querySelector('.vidgrid.one > .vidtile:has(canvas)') : root.querySelector('.vidscreen .pip');
    const canSwap = () => !!root.querySelector('.vidscreen .pip') && !!root.querySelector('.vidgrid.one > .vidtile canvas');
    const holding = () => !!root.querySelector(HOLD);
    const keyboard = () => document.documentElement.dataset.input === 'keyboard';

    const setChrome = hidden => {
        const video = screen();
        hidden = hidden && !!video;
        if (hidden) root.setAttribute('data-chrome', 'hidden'); else root.removeAttribute('data-chrome');
        // Hidden controls must not keep focus or be tabbed to; visibility does most of it, inert
        // also takes them out of the accessibility tree while they are gone.
        for (const element of root.querySelectorAll('.vidscreen [data-chrome-part]'))
            if (hidden) element.setAttribute('inert', ''); else element.removeAttribute('inert');
    };
    const schedule = () => {
        clearTimeout(state.timer);
        state.timer = setTimeout(tick, IDLE_MS);
    };
    const tick = () => {
        if (!root.isConnected) { unmount(root); return; }
        const decision = shouldAutoHide({
            video: !!screen(), remoteVideo: !!root.querySelector('.vidtile canvas'), holding: holding(),
            keyboard: keyboard(), idleMs: Date.now() - state.last,
        });
        if (decision) setChrome(true);
        // Keep ticking even while hidden: it is also how a dialog that went away (the call ended,
        // or was minimized) gets its observers released.
        schedule();
    };
    const touch = () => { state.last = Date.now(); if (root.hasAttribute('data-chrome') && holding()) setChrome(false); schedule(); };
    state.touch = touch;

    // --- Fill / fit --------------------------------------------------------------------------
    const refit = () => {
        for (const tile of root.querySelectorAll('.vidtile')) {
            // Tiles change size without the dialog doing so: the grid re-lays out as people join
            // and as the controls come and go. Observing twice is a no-op.
            state.resize?.observe(tile);
            const canvas = tile.querySelector('canvas');
            if (!canvas) { delete tile.dataset.fit; continue; }
            tile.dataset.fit = tile.dataset.fitUser
                || fitFor(canvas.width, canvas.height, tile.clientWidth, tile.clientHeight, !tile.parentElement?.matches('.vidgrid.one'));
        }
        const pip = root.querySelector('.vidscreen .pip');
        const video = pip?.querySelector('video');
        if (pip && video) {
            if (video.videoWidth && video.videoHeight) pip.dataset.orient = video.videoWidth > video.videoHeight ? 'landscape' : 'portrait';
            // Your own picture fills unless it is known to be the wrong way round for the stage.
            pip.dataset.fit = root.hasAttribute('data-swapped') && video.videoWidth
                ? fitFor(video.videoWidth, video.videoHeight, pip.clientWidth, pip.clientHeight) : 'cover';
        }
        // Until someone drags it: top right over one person, bottom right in a group, where the top
        // row is faces and the bottom-right corner is the least of anyone's picture.
        if (!state.cornerChosen) {
            const corner = root.querySelector('.vidscreen.is-group') ? 'br' : 'tr';
            if (root.getAttribute('data-corner') !== corner) root.setAttribute('data-corner', corner);
        }
        if (root.hasAttribute('data-swapped') && !canSwap()) root.removeAttribute('data-swapped');
    };
    // Deferred a frame: refit observes tiles, and observing from inside the callback loops.
    state.resize = typeof ResizeObserver === 'function' ? new ResizeObserver(() => requestAnimationFrame(refit)) : null;
    state.resize?.observe(root);
    const watchVideo = () => {
        for (const video of root.querySelectorAll('.vidscreen .pip video'))
            if (!video.yapCallWatched) { video.yapCallWatched = true; video.addEventListener('resize', refit); video.addEventListener('loadedmetadata', refit); }
    };
    state.observer = new MutationObserver(records => {
        // A canvas reports its picture size through its width/height attributes; anything else
        // is Blazor swapping screens, opening a menu or adding a tile.
        refit(); watchVideo();
        if (records.some(record => record.type === 'childList')) {
            if (!screen()) { root.removeAttribute('data-swapped'); setChrome(false); }
            else if (holding() && root.hasAttribute('data-chrome')) setChrome(false);
            touch();
        }
    });
    state.observer.observe(root, { subtree: true, childList: true, attributes: true, attributeFilter: ['width', 'height'] });

    // --- Self-view drag, snap and swap --------------------------------------------------------
    const flip = (element, change) => {
        const before = element.getBoundingClientRect();
        change();
        if (reducedMotion()) { element.style.translate = ''; return; }
        const after = element.getBoundingClientRect();
        element.style.transition = 'none';
        element.style.translate = `${before.left - after.left}px ${before.top - after.top}px`;
        void element.offsetWidth;
        element.style.transition = '';
        element.style.translate = '';
    };
    const pointerDown = event => {
        const surface = small();
        if (!surface || !surface.contains(event.target) || event.button > 0) return;
        state.drag = { surface, id: event.pointerId, x: event.clientX, y: event.clientY, t: event.timeStamp,
            vx: 0, vy: 0, moved: false };
        surface.setPointerCapture?.(event.pointerId);
    };
    const pointerMove = event => {
        const drag = state.drag;
        if (!drag || event.pointerId !== drag.id) return;
        const dx = event.clientX - drag.x, dy = event.clientY - drag.y;
        if (!drag.moved && Math.hypot(dx, dy) < TAP_SLOP) return;
        if (!drag.moved) { drag.moved = true; drag.surface.setAttribute('data-dragging', ''); }
        const elapsed = Math.max(1, event.timeStamp - (drag.lt ?? drag.t));
        drag.vx = (event.clientX - (drag.lx ?? drag.x)) / elapsed;
        drag.vy = (event.clientY - (drag.ly ?? drag.y)) / elapsed;
        drag.lx = event.clientX; drag.ly = event.clientY; drag.lt = event.timeStamp;
        drag.surface.style.translate = `${dx}px ${dy}px`;
    };
    const pointerUp = event => {
        const drag = state.drag;
        if (!drag || event.pointerId !== drag.id) return;
        state.drag = null;
        drag.surface.removeAttribute('data-dragging');
        if (!drag.moved) {
            // A tap on the self-view swaps it with the person you are talking to.
            // The size change animates in CSS; nothing to FLIP.
            if (canSwap()) { root.toggleAttribute('data-swapped'); refit(); }
            return;
        }
        const bounds = root.getBoundingClientRect(), rect = drag.surface.getBoundingClientRect();
        const aim = projectRelease(rect.left + rect.width / 2 - bounds.left, rect.top + rect.height / 2 - bounds.top, drag.vx, drag.vy);
        const corner = nearestCorner(aim.x, aim.y, bounds.width, bounds.height);
        state.cornerChosen = true;
        flip(drag.surface, () => { root.setAttribute('data-corner', corner); drag.surface.style.translate = ''; });
    };
    const pointerCancel = event => {
        const drag = state.drag;
        if (!drag || event.pointerId !== drag.id) return;
        state.drag = null;
        drag.surface.removeAttribute('data-dragging');
        flip(drag.surface, () => { drag.surface.style.translate = ''; });
    };

    // --- Tap to toggle, double tap to fill/fit --------------------------------------------------
    const click = event => {
        const video = screen();
        if (!video || !video.contains(event.target)) return;
        const surface = small();
        if (surface?.contains(event.target)) return; // Handled as a tap or a drag above.
        if (event.target.closest(INTERACTIVE)) return;
        const tile = event.target.closest('.vidtile');
        const now = event.timeStamp, previous = state.lastTap;
        if (previous && now - previous.t < DOUBLE_TAP_MS && previous.tile === tile) {
            clearTimeout(state.tapTimer); state.lastTap = null;
            if (tile?.querySelector('canvas')) {
                tile.dataset.fitUser = (tile.dataset.fit === 'cover') ? 'contain' : 'cover';
                refit();
            }
            return;
        }
        state.lastTap = { t: now, tile };
        clearTimeout(state.tapTimer);
        state.tapTimer = setTimeout(() => {
            state.lastTap = null;
            const hidden = root.hasAttribute('data-chrome');
            // A mouse that just brought the controls back by moving was on its way to click them
            // back: that click must not immediately hide them again.
            const justRevealed = Date.now() - (state.revealed ?? -Infinity) < 800;
            setChrome(!hidden && !holding() && !justRevealed);
            touch();
        }, DOUBLE_TAP_MS);
    };
    const key = event => {
        touch();
        if (root.hasAttribute('data-chrome')) setChrome(false);
        if (event.key === 'Escape' && root.hasAttribute('data-swapped')) root.removeAttribute('data-swapped');
    };
    const focus = () => { if (root.hasAttribute('data-chrome')) setChrome(false); touch(); };
    // A mouse has no tap to bring the controls back; moving it does, as in any video player.
    const move = event => {
        if (event.pointerType !== 'mouse') return;
        if (root.hasAttribute('data-chrome')) { setChrome(false); state.revealed = Date.now(); }
        touch();
    };

    state.listeners = [
        ['pointerdown', pointerDown], ['pointermove', pointerMove], ['pointerup', pointerUp],
        ['pointercancel', pointerCancel], ['click', click], ['keydown', key], ['focusin', focus],
        ['pointermove', move],
    ];
    for (const [name, handler] of state.listeners) root.addEventListener(name, handler);
    state.down = () => touch();
    root.addEventListener('pointerdown', state.down, true);
    refit(); watchVideo(); schedule();
}

export function unmount(root) {
    const state = root && states.get(root);
    if (!state) return;
    clearTimeout(state.timer); clearTimeout(state.tapTimer);
    state.observer?.disconnect(); state.resize?.disconnect();
    for (const [name, handler] of state.listeners) root.removeEventListener(name, handler);
    root.removeEventListener('pointerdown', state.down, true);
    states.delete(root);
}

/** Bring the controls back, e.g. after something outside the gestures changed the call. */
export function reveal(root) {
    const state = root && states.get(root);
    if (!state) return;
    root.removeAttribute('data-chrome');
    for (const element of root.querySelectorAll('[data-chrome-part][inert]')) element.removeAttribute('inert');
    state.touch();
}
