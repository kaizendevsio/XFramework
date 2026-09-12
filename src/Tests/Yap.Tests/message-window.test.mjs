import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function fixture() {
    const events = new Map(), frames = new Map(); let nextFrame = 0, resize;
    const ids = Array.from({ length: 30 }, (_, i) => `m${i}`);
    const rows = ids.map(id => ({ dataset: { windowRow: id }, isConnected: true, height: 100, getBoundingClientRect() { return { height: this.height }; } }));
    let scrollTop = 0;
    const element = { clientHeight: 500, scrollHeight: 3000, isConnected: true,
        get scrollTop() { return scrollTop; }, set scrollTop(value) { scrollTop = Math.max(0, Math.min(value, this.scrollHeight - this.clientHeight)); },
        querySelector(selector) { return selector === '[data-window-lead]' ? { getBoundingClientRect: () => ({ height: 0 }) } : { style: {} }; },
        querySelectorAll: () => rows,
        addEventListener: (name, handler) => events.set(name, handler), removeEventListener: name => events.delete(name)
    };
    const context = { window: { yap: {} }, getComputedStyle: () => ({ paddingTop: '0' }),
        requestAnimationFrame: callback => { frames.set(++nextFrame, callback); return nextFrame; }, cancelAnimationFrame: id => frames.delete(id),
        ResizeObserver: class { constructor(callback) { resize = callback; } observe() {} unobserve() {} disconnect() {} }
    };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/message-window.js', import.meta.url), 'utf8'), context);
    const api = context.window.yap.messageWindow, ref = { invokeMethodAsync: async () => {} };
    const sync = () => api.sync(element, ref, ids, 0, ids.length, true);
    const flush = () => { const pending = [...frames.values()]; frames.clear(); pending.forEach(callback => callback()); };
    sync(); flush();
    return { api, element, ids, rows, events, sync, flush, resize: () => resize() };
}

test('new messages and late image layout follow when still at the latest message', () => {
    const f = fixture(); assert.equal(f.element.scrollTop, 2500);
    f.ids.push('new'); f.element.scrollHeight += 100; f.sync();
    assert.equal(f.element.scrollTop, 2600);
    f.rows[29].height += 300; f.element.scrollHeight += 300; f.resize();
    assert.equal(f.element.scrollTop, 2900);
});

test('upward input during a programmatic scroll immediately releases following, even within 64px', () => {
    const f = fixture(); f.sync(); // adjusting remains true until the next frame
    f.events.get('wheel')({ deltaY: -20 }); f.element.scrollTop -= 20; f.events.get('scroll')();
    f.ids.push('new'); f.element.scrollHeight += 100; f.sync();
    assert.equal(f.element.scrollTop, 2480);
    f.rows[29].height += 300; f.element.scrollHeight += 300; f.resize();
    assert.equal(f.element.scrollTop, 2480, 'an image below the reader must not pull them down');
});

test('touch history keeps its anchor when older messages are prepended and repins only at bottom', () => {
    const f = fixture(); f.events.get('touchstart')({ touches: [{ clientY: 200 }] });
    f.events.get('touchmove')({ touches: [{ clientY: 230 }] });
    f.element.scrollTop = 1900; f.events.get('scroll')();
    f.ids.unshift('older'); f.element.scrollHeight += 100; f.sync();
    assert.equal(f.element.scrollTop, 2000);
    f.element.scrollTop = f.element.scrollHeight; f.events.get('scroll')(); f.flush();
    f.ids.push('new'); f.element.scrollHeight += 100; f.sync();
    assert.equal(f.element.scrollTop, 2700);
    f.api.dispose(f.element); assert.equal(f.events.size, 0);
});
