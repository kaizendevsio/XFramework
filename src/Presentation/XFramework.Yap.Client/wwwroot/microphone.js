// Consent is owned by the browser. Never persist a pretend "always allowed" flag.
window.yap.microphone = {
    async state() {
        if (!navigator.mediaDevices?.getUserMedia) return 'unsupported';
        try { return (await navigator.permissions.query({ name: 'microphone' })).state; }
        catch { return 'unknown'; }
    },
    async request() {
        if (!navigator.mediaDevices?.getUserMedia) return 'unsupported';
        let stream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            return 'ready';
        } catch (error) {
            return error?.name === 'NotAllowedError' ? 'denied'
                : error?.name === 'NotFoundError' ? 'missing' : 'unavailable';
        } finally { stream?.getTracks().forEach(track => track.stop()); }
    }
};
