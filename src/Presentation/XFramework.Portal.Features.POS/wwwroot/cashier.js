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
