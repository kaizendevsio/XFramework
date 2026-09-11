// Programmatically opened Inventario overlays retain their actual keyboard opener.
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
