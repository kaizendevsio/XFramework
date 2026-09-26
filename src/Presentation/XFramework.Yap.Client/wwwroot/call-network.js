// Network hints for a live call. None of these proves anything about the call's socket: "online" can
// fire on a network that has no route yet, and a switch from Wi-Fi to mobile data can leave a socket
// that looks open but will never deliver again. They only tell VoiceState to check now (while
// connected) or to retry now (while reconnecting) instead of waiting out a timer.

/**
 * Report network hints to `target.invokeMethodAsync('OnCallNetworkChanged', kind)` until disposed.
 * Kinds: "online" (the browser regained a network), "network" (the connection type changed, where
 * the Network Information API exists), "visible" (the page came back from the background; iOS
 * suspends a backgrounded PWA's sockets and timers, so this is when a resume can begin).
 */
export function watch(target, scope = globalThis) {
    const doc = scope.document;
    const connection = scope.navigator?.connection;
    let disposed = false;
    const report = kind => { if (!disposed) Promise.resolve(target.invokeMethodAsync('OnCallNetworkChanged', kind)).catch(() => {}); };
    const online = () => report('online');
    const network = () => report('network');
    const visible = () => { if (!doc?.hidden) report('visible'); };
    const shown = event => { if (event?.persisted) report('visible'); };
    scope.addEventListener?.('online', online);
    scope.addEventListener?.('pageshow', shown);
    doc?.addEventListener?.('visibilitychange', visible);
    connection?.addEventListener?.('change', network);
    return {
        dispose() {
            if (disposed) return;
            disposed = true;
            scope.removeEventListener?.('online', online);
            scope.removeEventListener?.('pageshow', shown);
            doc?.removeEventListener?.('visibilitychange', visible);
            connection?.removeEventListener?.('change', network);
        }
    };
}
