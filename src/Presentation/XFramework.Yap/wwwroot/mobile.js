// Safari can ignore viewport zoom limits. Cancel its native pinch gestures too.
// Single-finger scrolling, taps, text selection, and keyboard input remain native.
(() => {
    const preventZoom = event => { if (event.cancelable) event.preventDefault(); };
    for (const type of ['gesturestart', 'gesturechange', 'gestureend']) {
        document.addEventListener(type, preventZoom, { passive: false });
    }
    for (const type of ['touchstart', 'touchmove']) {
        document.addEventListener(type, event => {
            if (event.touches.length > 1) preventZoom(event);
        }, { passive: false });
    }
})();
