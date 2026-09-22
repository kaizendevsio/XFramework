import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/install.js', import.meta.url), 'utf8');
function fixture(userAgent, standalone = false) {
    const handlers = {}, saved = new Map(), events = [];
    const context = { navigator: { userAgent, standalone }, matchMedia: () => ({ matches: false }), Date, Number, String, queueMicrotask,
        localStorage: { getItem: k => saved.get(k), setItem: (k,v) => saved.set(k,v) },
        addEventListener: (name,fn) => handlers[name] = fn, window: { yap: { device: { canInstall: () => false } } } };
    vm.runInNewContext(source, context);
    const api = context.window.yap.installSupport;
    const watch = () => api.watch({ invokeMethodAsync: async (...args) => events.push(args) });
    return { api, context, handlers, events, watch };
}
test('iOS offers instructions and respects a seven-day dismissal and standalone installation', () => {
    const f = fixture('iPhone');
    assert.equal(f.watch().platform, 'ios'); assert.equal(f.watch().native, false);
    f.api.dismiss(); assert.equal(f.watch().dismissed, true);
    assert.equal(fixture('iPhone', true).watch().installed, true);
});
test('Android notifies the component when a native install prompt becomes available', async () => {
    const f = fixture('Android'); f.watch();
    f.context.window.yap.device.canInstall = () => true;
    f.handlers.beforeinstallprompt(); await Promise.resolve();
    assert.equal(f.events[0][0], 'InstallChanged'); assert.equal(f.events[0][1].native, true);
    await f.api.open(); assert.equal(f.events[1][0], 'OpenInstall');
    f.api.unwatch(); f.handlers.appinstalled(); assert.equal(f.events.length, 2);
});
