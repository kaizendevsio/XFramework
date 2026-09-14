// Resolve interop calls even when the crypto module has not finished downloading.
(() => {
    let loaded;
    window.yap.encryption = new Proxy({}, {
        get: (_target, method) => async (...args) => {
            const module = await (loaded ??= import('./encryption.mjs'));
            if (typeof module.encryption[method] !== 'function') throw new Error('Unsupported encryption operation.');
            return module.encryption[method](...args);
        }
    });
})();
