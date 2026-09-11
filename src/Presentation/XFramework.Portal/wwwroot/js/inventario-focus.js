// Programmatically opened Inventario overlays retain their actual keyboard opener.
// Blueprint 3.16 popovers and dialogs use separate document Escape listeners.
// Route Escape through the popover's outside-dismiss path, inside its parent dialog.
document.addEventListener('keydown', event => {
    if (event.key !== 'Escape' || !document.querySelector('.inventario-shell')) return;
    const trigger = document.querySelector('.inv-dialog button[aria-haspopup][aria-expanded="true"]');
    if (!trigger || !trigger.getClientRects().length) return;
    const dialog = trigger.closest('.inv-dialog');
    if (!dialog) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    dialog.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    dialog.dispatchEvent(new PointerEvent('pointerup', { bubbles: true }));
    trigger.focus({ preventScroll: true });
}, true);

window.inventarioFocus = (() => {
    const openers = new Map();
    return {
        capture(key) {
            openers.set(key, document.activeElement);
        },
        restore(key) {
            const opener = openers.get(key);
            openers.delete(key);
            requestAnimationFrame(() => {
                if (opener?.isConnected && !opener.disabled) opener.focus({ preventScroll: true });
            });
        }
    };
})();
