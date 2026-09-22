// Native installation on supporting Android browsers; explicit instructions on iOS.
(() => {
    let receiver;
    const installed = () => matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
    const platform = () => /iPhone|iPad|iPod/.test(navigator.userAgent) || navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1 ? 'ios'
        : /Android/.test(navigator.userAgent) ? 'android' : 'other';
    const status = () => {
        let until = 0; try { until = Number(localStorage.getItem('yap-install-dismissed') || 0); } catch {}
        return { installed: installed(), platform: platform(), native: window.yap.device.canInstall(), dismissed: Date.now() < until };
    };
    const notify = () => { void receiver?.invokeMethodAsync('InstallChanged', status()).catch(() => {}); };
    addEventListener('beforeinstallprompt', () => queueMicrotask(notify));
    addEventListener('appinstalled', notify);
    window.yap.installSupport = {
        watch(ref) { receiver = ref; return status(); },
        unwatch() { receiver = null; },
        dismiss() { try { localStorage.setItem('yap-install-dismissed', String(Date.now() + 7 * 86400000)); } catch {} },
        open() { return receiver?.invokeMethodAsync('OpenInstall', status()); }
    };
})();
