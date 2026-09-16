import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/worker-registration.js', import.meta.url), 'utf8');

// A browser without module service workers ignores the `type` member, loads the module file as a
// classic script and rejects on its first `import`. That rejection is the whole feature test, so
// the fixture models exactly that rather than a capability flag nothing actually exposes.
function fixture({ modules = true, existing = null, offline = false } = {}) {
    const attempts = [];
    const self = {};
    const navigator = { serviceWorker: {
        async getRegistration() { return existing; },
        async register(url, options) {
            attempts.push({ url, type: options.type, updateViaCache: options.updateViaCache });
            if (offline) throw new TypeError('Failed to fetch');
            if (url.endsWith('.module.js') && !modules) throw new SyntaxError("Cannot use import statement outside a module");
            return { script: url };
        }
    } };
    vm.runInContext(source, vm.createContext({ self, navigator }));
    return { api: self.yapWorker, attempts };
}

test('a browser with module service workers gets the worker that can decrypt', async () => {
    const f = fixture();
    assert.deepEqual(await f.api.register(), { script: 'service-worker.module.js' });
    assert.deepEqual(f.attempts, [{ url: 'service-worker.module.js', type: 'module', updateViaCache: 'none' }]);
});

// iOS 15.0-16.3, Firefox 146 and older. They keep today's worker - offline shell, update prompt,
// generic banner - rather than being left with no service worker at all. No iOS release that can
// receive a web push is below this line, so nothing there loses a notification it would have had.
test('a browser without them falls back to the classic worker rather than to nothing', async () => {
    const f = fixture({ modules: false });
    assert.deepEqual(await f.api.register(), { script: 'service-worker.js' });
    assert.deepEqual(f.attempts.map(x => x.url), ['service-worker.module.js', 'service-worker.js']);
    assert.equal(f.attempts[1].type, undefined);
    assert.equal(f.attempts[1].updateViaCache, 'none');
});

// Registering a different script URL at the same scope replaces the registration, so the three
// callers have to share one result or they would keep swapping the installed worker.
test('one registration is shared by every caller', async () => {
    const f = fixture();
    const [a, b, c] = await Promise.all([f.api.register(), f.api.register(), f.api.register()]);
    assert.ok(a === b && b === c);
    assert.equal(f.attempts.length, 1);
});

test('push reuses whatever is already installed instead of racing the app', async () => {
    const f = fixture({ existing: { script: 'installed-already' } });
    assert.deepEqual(await f.api.existing(), { script: 'installed-already' });
    assert.deepEqual(f.attempts, []);
});

test('an offline attempt is not remembered, so the module worker is tried again next time', async () => {
    const f = fixture({ offline: true });
    await assert.rejects(f.api.register());
    await assert.rejects(f.api.register());
    assert.deepEqual(f.attempts.map(x => x.url),
        ['service-worker.module.js', 'service-worker.js', 'service-worker.module.js', 'service-worker.js']);
});
