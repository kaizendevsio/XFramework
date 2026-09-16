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
        return { call: (...args) => rpc.call(...args), dispose: () => { rpc.close(); worker.terminate(); } };
    }
    window.yap.encryption = new Proxy({}, {
        get: (_target, method) => async (...args) => {
            if (method === 'passwordClear') {
                const previous = loaded; loaded = null;
                // Logout must clear worker memory even if its RPC loop is stalled.
                try { (await previous)?.dispose(); } catch { }
                return;
            }
            let rpc;
            try { rpc = await (loaded ??= connect()); }
            catch (error) { loaded = null; throw error; }
            return rpc.call(method, args);
        }
    });
})();
