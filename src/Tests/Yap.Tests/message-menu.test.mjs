import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/message-menu.js', import.meta.url), 'utf8');
function fixture({ top = 700, height = 600, viewport = 800, offset = 0, missing = false } = {}) {
    const listeners = new Map(), props = new Map();
    const style = { setProperty: (name, value) => props.set(name, value) };
    const events = prefix => ({ addEventListener: (name, fn) => listeners.set(prefix + name, fn), removeEventListener: name => listeners.delete(prefix + name) });
    const visualViewport = { height: viewport, offsetTop: offset, ...events('viewport-') };
    const window = { yap: {}, innerHeight: 800, visualViewport, ...events('window-') };
    const bubble = { getBoundingClientRect: () => ({ top }) };
    const message = { querySelector: selector => selector === '.bub,.photo-open' ? bubble : null };
    const dialog = { style, showModal() { this.open = true; }, close() { this.open = false; } };
    const stack = { style, offsetHeight: height, querySelector: () => null };
    let disconnected = false;
    vm.runInNewContext(source, { window, document: { querySelector: () => missing ? null : message }, CSS: { escape: x => x },
        ResizeObserver: class { observe() {} disconnect() { disconnected = true; } } });
    window.yap.messageMenu.open(dialog, stack, 'message');
    return { dialog, props, visualViewport, listeners, close: () => window.yap.messageMenu.close(dialog), disconnected: () => disconnected };
}

test('menu remains inside the viewport for messages at either edge', () => {
    assert.equal(fixture().props.get('--menu-top'), '184px');
    assert.equal(fixture({ top: -200 }).props.get('--menu-top'), '16px');
    assert.equal(fixture({ missing: true, height: 200 }).props.get('--menu-top'), '400px');
});
test('keyboard viewport and its scroll offset determine placement and update on resize', () => {
    const f = fixture({ top: 230, height: 200, viewport: 400, offset: 100 });
    assert.equal(f.props.get('--menu-top'), '130px');
    assert.equal(f.props.get('--menu-viewport-top'), '100px');
    f.visualViewport.height = 300;
    f.listeners.get('viewport-resize')();
    assert.equal(f.props.get('--menu-viewport-height'), '300px');
    assert.equal(f.props.get('--menu-top'), '84px');
});
test('closing removes observers and viewport handlers, including repeated cleanup', () => {
    const f = fixture();
    assert.equal(f.listeners.size, 3);
    f.close(); f.close();
    assert.equal(f.dialog.open, false);
    assert.equal(f.listeners.size, 0);
    assert.equal(f.disconnected(), true);
});
