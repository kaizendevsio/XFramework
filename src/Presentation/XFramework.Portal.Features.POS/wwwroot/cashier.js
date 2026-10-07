export function readSearchValue() {
    return document.getElementById('pos-catalog-search')?.value ?? '';
}

export function focusSearch() {
    if (!document.querySelector('[role="dialog"][data-state="open"]')) {
        document.getElementById('pos-catalog-search')?.focus({ preventScroll: true });
    }
}

export function restoreDialogFocus(name) {
    const trigger = document.querySelector(`[data-pos-dialog-trigger="${name}"]`);
    if (!trigger) return;

    // Portaled dialog removal finishes after the page's own render.
    const restore = () => {
        if (!trigger.isConnected) return true;
        if (document.getElementById(trigger.getAttribute('aria-controls'))) return false;
        requestAnimationFrame(() => {
            if (trigger.isConnected) trigger.focus({ preventScroll: true });
        });
        return true;
    };
    if (restore()) return;
    const observer = new MutationObserver(() => {
        if (restore()) observer.disconnect();
    });
    observer.observe(document.body, { childList: true, subtree: true });
}

let fullscreenOwner = null;

export function initializeFocus(root) {
    const button = root.querySelector('[data-pos-focus-toggle]');
    const owner = {};
    let focused = false;
    let disposed = false;
    let pendingRequest = null;
    let unavailable = false;

    const render = () => {
        root.dataset.focus = String(focused);
        root.dataset.fullscreen = String(document.fullscreenElement === document.documentElement);
        const label = focused ? 'Exit cashier focus' : 'Enter cashier focus';
        button.setAttribute('aria-label', label);
        button.setAttribute('aria-pressed', String(focused));
        button.title = focused && unavailable ? `${label} (fullscreen unavailable)` : label;
        button.querySelector('[data-pos-focus-enter-icon]').hidden = focused;
        button.querySelector('[data-pos-focus-exit-icon]').hidden = !focused;
    };

    const exitFullscreen = async () => {
        if (fullscreenOwner !== owner || document.fullscreenElement !== document.documentElement)
            return;
        try {
            await document.exitFullscreen();
        } catch {
            // A browser may reject exit; normal layout and the native Escape exit remain available.
        }
        if (!pendingRequest && document.fullscreenElement !== document.documentElement && fullscreenOwner === owner)
            fullscreenOwner = null;
        if (!disposed)
            render();
    };

    const exit = () => {
        focused = false;
        render();
        return exitFullscreen();
    };

    const toggle = () => {
        if (focused) {
            void exit();
            return;
        }
        focused = true;
        unavailable = false;
        render();
        if (pendingRequest)
            return;
        if (typeof document.documentElement.requestFullscreen !== 'function' || !document.fullscreenEnabled) {
            unavailable = true;
            render();
            return;
        }

        fullscreenOwner = owner;
        try {
            // Invoke before any await or Blazor Server roundtrip consumes the trusted click gesture.
            pendingRequest = document.documentElement.requestFullscreen();
        } catch {
            fullscreenOwner = null;
            unavailable = true;
            render();
            return;
        }
        pendingRequest.then(() => {
            pendingRequest = null;
            if (disposed || !focused)
                void exitFullscreen();
            else
                render();
        }, () => {
            pendingRequest = null;
            if (fullscreenOwner === owner)
                fullscreenOwner = null;
            unavailable = true;
            if (!disposed)
                render();
        });
    };

    const onClick = event => {
        if (event.target.closest('[data-pos-focus-toggle]') === button && !button.disabled)
            toggle();
    };
    const onEscape = event => {
        if (event.key === 'Escape' && focused)
            void exit();
    };
    const onFullscreenChange = () => {
        if (fullscreenOwner !== owner)
            return;
        if (document.fullscreenElement !== document.documentElement) {
            focused = false;
            fullscreenOwner = null;
        }
        render();
    };

    root.addEventListener('click', onClick, true);
    document.addEventListener('keydown', onEscape, true);
    document.addEventListener('fullscreenchange', onFullscreenChange);
    render();

    return {
        async dispose() {
            disposed = true;
            root.removeEventListener('click', onClick, true);
            document.removeEventListener('keydown', onEscape, true);
            document.removeEventListener('fullscreenchange', onFullscreenChange);
            await exit();
        }
    };
}
