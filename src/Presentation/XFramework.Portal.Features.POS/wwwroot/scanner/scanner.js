import { decoder, decode } from '/_content/XFramework.Portal.Shared/barcodes/barcodes.js';
export { decoder, decode, qrData } from '/_content/XFramework.Portal.Shared/barcodes/barcodes.js';
const cameras = new WeakMap();

export function cameraError(error) {
    switch (error?.name) {
        case "NotAllowedError": return "Camera permission was denied. Allow camera access in browser settings.";
        case "NotFoundError": return "No camera was found on this device.";
        case "NotReadableError": return "Camera is busy. Close other camera apps and try again.";
        case "OverconstrainedError": return "This camera cannot provide a supported video stream.";
        default: return "Camera could not start. Use a supported browser and allow camera access.";
    }
}

export function takeChallenge() {
    const challenge = location.hash.slice(1);
    history.replaceState(history.state, "", location.pathname + location.search);
    return /^[A-F0-9]{64}$/.test(challenge) ? challenge : "";
}

export function pairingChallenge(value) {
    try {
        const url = new URL(value);
        return url.origin === location.origin && url.pathname === "/pos/mobile-scanner" &&
            /^[A-F0-9]{64}$/.test(url.hash.slice(1)) ? url.hash.slice(1) : "";
    } catch { return /^[A-F0-9]{64}$/.test(value) ? value : ""; }
}

export function pairingTenant(value = location.href) {
    try {
        const url = new URL(value);
        const tenant = url.searchParams.get("tenant") ?? "";
        return url.origin === location.origin && url.pathname === "/pos/mobile-scanner" &&
            /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(tenant) ? tenant : "";
    } catch { return ""; }
}

export async function start(video, receiver) {
    stop(video);
    if (!globalThis.isSecureContext)
        throw new Error("Camera requires HTTPS. Open this Portal on its authorized HTTPS address.");
    if (!navigator.mediaDevices?.getUserMedia)
        throw new Error("Camera scanning is not supported by this browser.");
    const state = { stopped: false, stream: null, timer: null, last: "", emptySince: null };
    cameras.set(video, state);
    const active = () => !state.stopped && cameras.get(video) === state;
    state.onHidden = () => {
        if (document.hidden && active()) {
            stop(video);
            receiver.invokeMethodAsync("CameraFailed", "Camera paused while this page was hidden. Restart the camera.");
        }
    };
    state.onExit = () => { if (active()) stop(video); };
    document.addEventListener("visibilitychange", state.onHidden);
    window.addEventListener("pagehide", state.onExit);
    let decoderLoaded = false;
    try {
        await decoder();
        decoderLoaded = true;
        if (!active()) return false;
        state.stream = await navigator.mediaDevices.getUserMedia({
            audio: false, video: { facingMode: { ideal: "environment" }, width: { ideal: 1280 }, height: { ideal: 720 } }
        });
        if (!active()) { state.stream.getTracks().forEach(t => t.stop()); return false; }
        video.srcObject = state.stream;
        await video.play();
        if (!active()) return false;
        const canvas = document.createElement("canvas");
        const context = canvas.getContext("2d", { willReadFrequently: true });
        const frame = async () => {
            if (state.stopped) return;
            try {
                if (video.readyState >= 2 && video.videoWidth > 0) {
                    const scale = Math.min(1, 1280 / video.videoWidth);
                    canvas.width = Math.round(video.videoWidth * scale);
                    canvas.height = Math.round(video.videoHeight * scale);
                    context.drawImage(video, 0, 0, canvas.width, canvas.height);
                    const results = await decode(context.getImageData(0, 0, canvas.width, canvas.height));
                    if (state.stopped) return;
                    const code = results.find(r => r.isValid)?.text ?? "";
                    if (!code) {
                        state.emptySince ??= performance.now();
                        if (performance.now() - state.emptySince >= 700) state.last = "";
                    } else {
                        state.emptySince = null;
                        if (code !== state.last) {
                            state.last = code;
                            if (!await receiver.invokeMethodAsync("Decoded", code)) { if (active()) stop(video); return; }
                        }
                    }
                }
                if (!state.stopped) state.timer = setTimeout(frame, 180);
            } catch {
                if (!active()) return;
                stop(video);
                await receiver.invokeMethodAsync("CameraFailed", "Scanner connection interrupted. Restart the camera.");
            }
        };
        state.timer = setTimeout(frame, 180);
        return true;
    } catch (error) {
        if (!active()) return false;
        stop(video);
        if (!decoderLoaded) throw error;
        throw new Error(cameraError(error));
    }
}

export function stop(video) {
    const state = cameras.get(video);
    if (!state) return;
    state.stopped = true;
    clearTimeout(state.timer);
    state.stream?.getTracks().forEach(track => track.stop());
    video.pause();
    video.srcObject = null;
    document.removeEventListener("visibilitychange", state.onHidden);
    window.removeEventListener("pagehide", state.onExit);
    cameras.delete(video);
}
