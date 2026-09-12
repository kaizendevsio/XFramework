import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const root = new URL('../../Presentation/XFramework.Yap.Client/wwwroot/', import.meta.url);
function fixture(storage = new Map()) {
    const handlers = new Map(), window = { yap: {}, fetch: async () => ({ status: 403 }) };
    const document = { querySelectorAll: () => [], addEventListener() {} };
    vm.runInNewContext(readFileSync(new URL('diagnostics.js', root), 'utf8'), { window, document, URL, performance, Date,
        location: { href: 'https://yap.test/chat/secret-person-id?token=private-token', origin: 'https://yap.test' },
        navigator: { onLine: true, userAgent: 'Test browser', clipboard: { writeText: async () => { throw Error('Denied'); } } },
        innerWidth: 390, innerHeight: 844, addEventListener: (name, handler) => handlers.set(name, handler),
        localStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value) } });
    return { api: window.yap.diagnostics, window, storage, handlers };
}
test('logging is opt-in, survives reload, and never captures private request data', async () => {
    const f = fixture();
    await f.window.fetch('/api/chat/messages/private-message?token=private-token', { method: 'POST', body: 'secret message', headers: { Authorization: 'secret password' } });
    assert.equal(JSON.parse(f.api.report()).entries.length, 0);
    f.api.setEnabled(true);
    await f.window.fetch('/api/chat/messages/private-message?token=private-token', { method: 'POST', body: 'secret message', headers: { Authorization: 'secret password' } });
    f.api.record('error', { status: 403, message: 'secret message', token: 'private-token', fileName: 'private-photo.jpg' });
    f.handlers.get('unhandledrejection')({ reason: new TypeError('secret message') });
    const restored = fixture(f.storage), report = restored.api.report();
    for (const secret of ['private-token', 'private-message', 'secret message', 'secret password', 'secret-person-id', 'private-photo']) assert.equal(report.includes(secret), false);
    assert.ok(report.includes('TypeError'));
    assert.ok(report.includes('"status": 403'));
    assert.ok(report.includes('/api/chat/messages/:id'));
    assert.equal(JSON.parse(report).entries.filter(e => e.event === 'page.start').length, 1);
    assert.equal(await restored.api.copy(), false, 'Clipboard denial returns a selectable-text fallback.');
});
test('log history is bounded, disable stops recording, and clear removes persisted history', () => {
    const f = fixture(); f.api.setEnabled(true);
    for (let i = 0; i < 600; i++) f.api.record('image.loaded', { width: 1440, height: 1920 });
    assert.ok(JSON.parse(f.api.report()).entries.length <= 200);
    f.api.setEnabled(false); const before = f.api.report(); f.api.record('error'); assert.equal(f.api.report(), before);
    f.api.clear(); assert.equal(JSON.parse(fixture(f.storage).api.report()).entries.length, 0);
});
test('recovery log navigation uses its cached page instead of booting Blazor', async () => {
    const handlers = {}, matches = [];
    const self = { importScripts() {}, assetsManifest: { version: 'test', assets: [] }, location: new URL('https://yap.test/service-worker.js'), addEventListener: (name, handler) => handlers[name] = handler };
    vm.runInNewContext(readFileSync(new URL('service-worker.published.js', root), 'utf8'), { self, URL, Set,
        caches: { open: async () => ({ match: async path => { matches.push(path); return 'cached'; } }) } });
    let response;
    handlers.fetch({ request: { url: 'https://yap.test/diagnostics.html', method: 'GET', mode: 'navigate' }, respondWith: value => response = value });
    await response; assert.deepEqual(matches, ['diagnostics.html']);
});
test('native previews are bounded and release their bitmap and canvas', async () => {
    let closed = false, encoded;
    const canvas = { width: 0, height: 0, getContext: () => ({ drawImage() {} }), toBlob: callback => { encoded = [canvas.width, canvas.height]; callback(new Blob(['jpeg'])); } };
    const window = { yap: {} };
    vm.runInNewContext(readFileSync(new URL('image-previews.js', root), 'utf8'), { window, Blob,
        createImageBitmap: async (_file, options) => { assert.equal(options.resizeWidth, 1440); return { width: 1440, height: 2880, close() { closed = true; } }; },
        document: { createElement: () => canvas } });
    await window.yap.imagePreviews.jpeg(new Blob(['photo']), false);
    assert.deepEqual(encoded, [1024, 2048]); assert.equal(closed, true); assert.equal(canvas.width, 1); assert.equal(canvas.height, 1);
});
