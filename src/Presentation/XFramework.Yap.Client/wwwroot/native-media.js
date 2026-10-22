let reference, interval;
function ready() { reference?.invokeMethodAsync('Available', typeof window.yapNative?.send === 'function'); }
function message(event) { reference?.invokeMethodAsync('Message', event.detail); }
export function attach(dotnet) {
    reference = dotnet;
    window.addEventListener('yap-native-message', message);
    window.addEventListener('yap-native-ready', ready);
    // Android installs the bridge after navigation completes; Blazor can render first.
    interval = setInterval(ready, 1000); ready();
}
export function send(command) {
    if (!window.yapNative?.send) throw new Error('The native benchmark app is required.');
    if (command.action === 'export' || command.action === 'stop') command = { action: command.action };
    window.yapNative.send(command);
}
export function detach() {
    window.yapNative?.send({ action: 'stop' });
    clearInterval(interval);
    window.removeEventListener('yap-native-message', message);
    window.removeEventListener('yap-native-ready', ready);
    reference = null;
}
