// This page runs without .NET, SQLite or encryption initialization. Only the app
// shell is updated: account data, OPFS and encryption-key storage are untouched.
(() => {
    const button = document.getElementById('recover'), notice = document.getElementById('notice');
    const timeout = (work, milliseconds) => new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('timeout')), milliseconds);
        Promise.resolve(work).then(resolve, reject).finally(() => clearTimeout(timer));
    });
    function installed(worker) {
        return new Promise((resolve, reject) => {
            const finish = error => {
                clearTimeout(timer); worker.removeEventListener('statechange', changed);
                error ? reject(error) : resolve();
            };
            const changed = () => {
                if (['installed', 'activated'].includes(worker.state)) finish();
                else if (worker.state === 'redundant') finish(new Error('installation-failed'));
            };
            const timer = setTimeout(() => finish(new Error('timeout')), 90000);
            worker.addEventListener('statechange', changed); changed();
        });
    }
    button.onclick = async () => {
        button.disabled = true;
        notice.textContent = 'Checking for an update…';
        try {
            if (!navigator.onLine) throw new Error('offline');
            if ('serviceWorker' in navigator) {
                const registration = await timeout(navigator.serviceWorker.register('/service-worker.js', { updateViaCache: 'none' }), 20000);
                await timeout(registration.update(), 20000);
                if (registration.installing) {
                    notice.textContent = 'Downloading the update. Keep this page open…';
                    await installed(registration.installing);
                }
                if (registration.waiting) {
                    notice.textContent = 'Applying the update…';
                    const worker = registration.waiting;
                    const activated = new Promise((resolve, reject) => {
                        const changed = () => {
                            if (worker.state === 'activated' || worker.state === 'redundant') {
                                clearTimeout(timer); worker.removeEventListener('statechange', changed);
                                worker.state === 'activated' ? resolve() : reject(new Error('activation-failed'));
                            }
                        };
                        const timer = setTimeout(() => { worker.removeEventListener('statechange', changed); reject(new Error('timeout')); }, 20000);
                        worker.addEventListener('statechange', changed);
                    });
                    worker.postMessage('activate');
                    await activated;
                }
            }
            window.yap?.diagnostics?.record('page.reload-requested', { reason: 'startup-recovery' });
            location.replace('/');
        } catch (error) {
            window.yap?.diagnostics?.record('startup.recovery-failed', { reason: error.message });
            notice.textContent = navigator.onLine
                ? 'The update could not finish. Try again on a stable connection, or open diagnostic logs below. Your saved data has not been cleared.'
                : 'Connect to the internet, then try again. Your saved data has not been cleared.';
            button.disabled = false;
        }
    };
})();
