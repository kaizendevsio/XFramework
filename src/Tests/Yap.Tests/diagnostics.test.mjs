import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const root = new URL('../../Presentation/XFramework.Yap.Client/wwwroot/', import.meta.url);
function fixture(storage = new Map(), yap = {}) {
    const handlers = new Map(), window = { yap, fetch: async () => ({ status: 403 }) };
    const document = { querySelectorAll: () => [], getElementsByTagName: () => ({ length: 42 }), visibilityState: 'visible', addEventListener: (name, handler) => handlers.set('document.' + name, handler) };
    vm.runInNewContext(readFileSync(new URL('diagnostics.js', root), 'utf8'), { window, document, URL, performance, Date, JSON, Math,
        setInterval: () => 1, clearInterval() {},
        location: { href: 'https://yap.test/chat/secret-person-id?token=private-token', origin: 'https://yap.test' },
        navigator: { onLine: true, userAgent: 'Test browser', clipboard: { writeText: async () => { throw Error('Denied'); } } },
        innerWidth: 390, innerHeight: 844, addEventListener: (name, handler) => { const prior = handlers.get(name); handlers.set(name, prior ? e => { prior(e); handler(e); } : handler); },
        localStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) } });
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
    // Only the navigation fetch matters here, so push arrives pre-loaded rather than via importScripts.
    const self = { importScripts() {}, yapNotifications: { install() {} }, assetsManifest: { version: 'test', assets: [] }, location: new URL('https://yap.test/service-worker.js'), addEventListener: (name, handler) => handlers[name] = handler };
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
test('session health survives an abrupt end and reports only counts and sizes', () => {
    const storage = new Map();
    const motion = { transitions: { navigations: () => 37, started: () => 0, enabled: () => false } };
    const first = fixture(storage, { motion });
    first.api.health.navigated();
    assert.equal(storage.has('yap-session-health-v1'), false, 'Nothing is written until logging is enabled.');
    first.api.setEnabled(true);
    const written = JSON.parse(storage.get('yap-session-health-v1'));
    assert.equal(written.clean, false); assert.equal(written.navigations, 37); assert.equal(written.domNodes, 42);
    // No pagehide: the process was killed, as iOS does to a WebContent process over its memory limit.
    const next = fixture(storage, { motion });
    assert.equal(next.api.health.previous().abrupt, true);
    assert.equal(next.api.health.previous().visible, true);
    const report = JSON.parse(next.api.report());
    const abrupt = report.entries.find(e => e.event === 'session.abrupt');
    assert.equal(abrupt.count, 37); assert.equal(abrupt.kind, 'foreground');
    assert.equal(report.previousSession.navigations, 37);
    for (const secret of ['secret-person-id', 'private-token']) assert.equal(JSON.stringify(report).includes(secret), false);
    // A normal close marks the record clean, so the next start does not cry wolf.
    next.handlers.get('pagehide')({ persisted: false });
    assert.equal(fixture(storage, { motion }).api.health.previous().abrupt, false);
    next.api.clear();
    assert.equal(storage.has('yap-session-previous-v1'), false);
});
