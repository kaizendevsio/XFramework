// Keep OpenPGP, key derivation and signed attachment processing off the UI thread.
(() => {
    let loaded;
    async function connect() {
        const { cryptoRpc } = await import('./crypto-rpc.mjs');
        const worker = new Worker(new URL('./encryption-worker.mjs', document.baseURI), { type: 'module', name: 'Yap encryption' });
        const rpc = cryptoRpc(worker);
        worker.addEventListener('message', ({ data }) => {
            if (data.ready) window.yap.diagnostics?.record('encryption.worker-ready');
        });
        worker.addEventListener('error', () => { rpc.close(); worker.terminate(); loaded = null; });
        worker.addEventListener('messageerror', () => { rpc.close(); worker.terminate(); loaded = null; });
        return rpc;
    }
    window.yap.encryption = new Proxy({}, {
        get: (_target, method) => async (...args) => {
            let rpc;
            try { rpc = await (loaded ??= connect()); }
            catch (error) { loaded = null; throw error; }
            return rpc.call(method, args);
        }
    });
})();
