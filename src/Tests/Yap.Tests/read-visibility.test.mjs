import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

test('read receipts exclude background, other routes, and rows hidden under the header or composer', () => {
    const row = (id, top, bottom) => ({ dataset: { windowRow: id }, getBoundingClientRect: () => ({ top, bottom }) });
    const rows = [row('header-covered', 0, 60), row('visible', 100, 250), row('composer-covered', 740, 790)];
    const list = { getBoundingClientRect: () => ({ top: 0, bottom: 800 }), querySelectorAll: () => rows };
    const document = { hidden: false, addEventListener() {}, querySelector: () => list };
    const location = { pathname: '/chat/chat-id' }, window = { yap: {} };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/device.js', import.meta.url), 'utf8'), {
        document, location, window, yap: window.yap, navigator: {}, addEventListener() {}, visualViewport: null,
        requestAnimationFrame() {}, cancelAnimationFrame() {}, getComputedStyle: () => ({ paddingTop: '90px', paddingBottom: '80px' })
    });
    const read = (parent = null) => Array.from(window.yap.device.visibleMessages('chat-id', parent));
    assert.deepEqual(read(), ['visible']);
    document.hidden = true; assert.deepEqual(read(), []);
    document.hidden = false;
    for (const route of ['/', '/settings', '/chat/other', '/thread/chat-id/reply-id']) { location.pathname = route; assert.deepEqual(read(), []); }
    assert.deepEqual(read('reply-id'), ['visible']);
});
