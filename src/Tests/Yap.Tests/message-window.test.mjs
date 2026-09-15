import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

function fixture(ref = { invokeMethodAsync: async () => {} }) {
    const readers = [], animations = [], attributes = new Map(), styles = new Map();
    const events = new Map(), frames = new Map(), timers = new Map(); let nextFrame = 0, resize;
    const ids = Array.from({ length: 30 }, (_, i) => `m${i}`);
    const bubble = (id, out) => ({ classList: { contains: name => name === (out ? 'out' : 'in') },
        animate: (keyframes, options) => animations.push({ id, keyframes, options }) });
    const row = (id, out = false) => ({ dataset: { windowRow: id }, style: {}, isConnected: true, height: 100,
        getBoundingClientRect() { return { height: this.height }; }, querySelector: () => bubble(id, out) });
    const rows = ids.map(id => row(id));
    let scrollTop = 0;
    const element = { clientHeight: 500, scrollHeight: 3000, isConnected: true,
        get scrollTop() { return scrollTop; }, set scrollTop(value) { scrollTop = Math.max(0, Math.min(value, this.scrollHeight - this.clientHeight)); },
        querySelector(selector) { return selector === '[data-window-lead]' ? { getBoundingClientRect: () => ({ height: 0 }) } : { style: {} }; },
        querySelectorAll: selector => selector === '[data-reader-id]' ? readers : rows,
        getClientRects: () => [1], getBoundingClientRect: () => ({ top: 0, bottom: 500 }),
        style: { setProperty: (name, value) => styles.set(name, value) },
        setAttribute: (name, value) => attributes.set(name, value), removeAttribute: name => attributes.delete(name),
        addEventListener: (name, handler) => events.set(name, handler), removeEventListener: name => events.delete(name)
    };
    const context = { window: { yap: {} }, document: { addEventListener() {}, removeEventListener() {} }, getComputedStyle: () => ({ paddingTop: '0' }),
        matchMedia: () => ({ matches: false }), setTimeout: callback => { timers.set(++nextFrame, callback); return nextFrame; }, clearTimeout: id => timers.delete(id),
        requestAnimationFrame: callback => { frames.set(++nextFrame, callback); return nextFrame; }, cancelAnimationFrame: id => frames.delete(id),
        ResizeObserver: class { constructor(callback) { resize = callback; } observe() {} unobserve() {} disconnect() {} }
    };
    vm.runInNewContext(readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/message-window.js', import.meta.url), 'utf8'), context);
    const api = context.window.yap.messageWindow;
    const sync = () => api.sync(element, ref, ids, 0, ids.length, true);
    const flush = () => { const pending = [...frames.values()]; frames.clear(); pending.forEach(callback => callback()); };
    const append = (id, out) => { ids.push(id); rows.push(row(id, out)); element.scrollHeight += 100; };
    sync(); flush();
    return { api, element, ids, rows, readers, events, sync, flush, timers, resize: () => resize(),
        animations, attributes, styles, row, append };
}

test('a burst of scroll frames schedules one delayed read check instead of interop each frame', async () => {
    const calls = []; const f = fixture({ invokeMethodAsync: async name => calls.push(name) });
    for (let i = 0; i < 60; i++) { f.events.get('scroll')(); f.flush(); }
    assert.equal(calls.filter(x => x === 'ReadVisible').length, 0);
    assert.equal(f.timers.size, 1);
    [...f.timers.values()][0]();
    assert.equal(calls.filter(x => x === 'ReadVisible').length, 1);
    f.api.dispose(f.element);
});

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

test('rapid upward scrolling before a coalesced scroll event survives render and composer resize', () => {
    const f = fixture();
    f.events.get('wheel')({ deltaY: -400 });
    // Browsers update scrollTop before delivering the next scroll event. Blazor or
    // ResizeObserver can run in between, particularly during touch momentum.
    for (let i = 0; i < 12; i++) {
        const target = f.element.scrollTop - 60;
        f.element.scrollTop = target;
        f.sync(); f.api.resize(f.element);
        assert.equal(f.element.scrollTop, target, `render ${i} must not restore a stale anchor`);
    }
});

test('late image above the viewport adjusts once without undoing subsequent upward input', () => {
    const f = fixture();
    f.events.get('wheel')({ deltaY: -500 }); f.element.scrollTop = 1800; f.events.get('scroll')();
    f.rows[2].height += 300; f.element.scrollHeight += 300; f.resize();
    assert.equal(f.element.scrollTop, 2100);
    f.element.scrollTop -= 80;
    f.resize(); f.sync();
    assert.equal(f.element.scrollTop, 2020);
});

test('bounded history windows preserve the same visible message in both directions', () => {
    const f = fixture();
    f.events.get('wheel')({ deltaY: -800 }); f.element.scrollTop = 1800; f.events.get('scroll')();
    const original = [...f.ids];
    const older = ['old0', 'old1', 'old2', 'old3', 'old4', ...original.slice(0, -5)];
    f.api.sync(f.element, { invokeMethodAsync: async () => {} }, older, 0, 30, true, true);
    assert.equal(f.element.scrollTop, 2300);
    f.api.sync(f.element, { invokeMethodAsync: async () => {} }, original, 0, 30, true, false);
    assert.equal(f.element.scrollTop, 1800);
});

test('returning to a previous history window can load its earlier edge again', () => {
    const calls = [];
    const ref = { invokeMethodAsync: async name => { calls.push(name); } };
    const f = fixture(ref);
    f.events.get('wheel')({ deltaY: -3000 }); f.element.scrollTop = 0;
    f.api.sync(f.element, ref, f.ids, 0, 30, true, true); f.flush();
    assert.equal(calls.filter(x => x === 'LoadEarlier').length, 1);
    const other = ['older', ...f.ids.slice(0, -1)];
    f.api.sync(f.element, ref, other, 0, 30, true, true);
    f.api.sync(f.element, ref, f.ids, 0, 30, true, true); f.flush();
    assert.equal(calls.filter(x => x === 'LoadEarlier').length, 2);
});


test('read receipt moves from its previous message and does not animate ordinary scrolling', () => {
    const f = fixture(), animations = [];
    const reader = (message, top) => ({ dataset: { readerId: 'person' },
        closest: () => ({ dataset: { messageId: message } }), getAnimations: () => [],
        getBoundingClientRect: () => ({ left: 100, top }), animate: frames => animations.push(frames) });
    f.readers.push(reader('m28', 200)); f.sync();
    f.readers[0] = reader('m29', 350); f.sync();
    assert.equal(animations.length, 1);
    assert.equal(animations[0][0].transform, 'translate(0px,-150px)');
    f.readers[0] = reader('m29', 250); f.sync();
    assert.equal(animations.length, 1);
});

test('a sent bubble springs out of the composer and a row recycled back never replays it', () => {
    const f = fixture();
    f.append('sent', true); f.sync();
    assert.equal(f.animations.length, 1);
    assert.equal(f.animations[0].id, 'sent');
    assert.equal(f.animations[0].keyframes[0].transform, 'translateY(18px) scale(.82)');
    assert.equal(f.animations[0].keyframes[0].transformOrigin, '100% 100%');
    assert.equal(f.animations[0].options.easing, 'cubic-bezier(.2,.9,.3,1.18)');
    f.sync();
    assert.equal(f.animations.length, 1, 'a repeated render must not replay entry');
    f.rows.splice(0, 5); f.sync();
    f.rows.unshift(...f.ids.slice(0, 5).map(id => f.row(id)));
    f.sync();
    assert.equal(f.animations.length, 1, 'a recycled row must not replay entry');
});

test('an incoming bubble settles in from the sender side rather than the composer', () => {
    const f = fixture();
    f.append('received', false); f.sync();
    assert.equal(f.animations.length, 1);
    assert.equal(f.animations[0].keyframes[0].transform, 'translateY(10px) scale(.94)');
    assert.equal(f.animations[0].keyframes[0].transformOrigin, '0 100%');
    assert.equal(f.animations[0].options.easing, 'cubic-bezier(.22,1,.36,1)');
    assert.ok(f.animations[0].keyframes.every(frame => !('height' in frame) && !('top' in frame)));
});

test('prepended history and a page of newer messages never animate', () => {
    const f = fixture();
    f.ids.unshift('older'); f.rows.unshift(f.row('older')); f.element.scrollHeight += 100; f.sync();
    assert.equal(f.animations.length, 0, 'earlier history is not an arrival');
    for (let i = 0; i < 5; i++) f.append(`page${i}`, false);
    f.sync();
    assert.equal(f.animations.length, 0, 'a page of newer history is not an arrival');
});

test('overscrolling the top bands the content and springs back when the finger lifts', () => {
    const f = fixture();
    f.element.scrollTop = 0; f.events.get('scroll')(); f.flush();
    let prevented = 0;
    const touch = y => ({ touches: [{ clientY: y, clientX: 40 }], preventDefault: () => prevented++ });
    f.events.get('touchstart')(touch(100));
    f.events.get('touchmove')(touch(140));
    f.events.get('touchmove')(touch(200));
    assert.equal(prevented, 2);
    assert.ok(f.attributes.has('data-banding'));
    assert.ok(parseFloat(f.styles.get('--band')) > 0);
    assert.equal(f.element.scrollTop, 0, 'the band must never move the scroller');
    f.events.get('touchend')();
    assert.equal(f.styles.get('--band'), '0px');
    assert.equal(f.attributes.has('data-banding'), false);
});

test('a horizontal drag at an end belongs to swipe-to-reply, not to the band', () => {
    const f = fixture();
    f.element.scrollTop = 0; f.events.get('scroll')(); f.flush();
    let prevented = 0;
    const touch = (y, x) => ({ touches: [{ clientY: y, clientX: x }], preventDefault: () => prevented++ });
    f.events.get('touchstart')(touch(100, 40));
    f.events.get('touchmove')(touch(106, 120));
    assert.equal(prevented, 0);
    assert.equal(f.styles.has('--band'), false);
});

test('wheeling past the bottom bands, and wheeling inside the history stays native', () => {
    const f = fixture(); // the fixture opens pinned to the latest message
    let prevented = 0;
    const wheel = deltaY => ({ deltaY, preventDefault: () => prevented++ });
    f.events.get('wheel')(wheel(120));
    assert.equal(prevented, 1);
    assert.ok(parseFloat(f.styles.get('--band')) < 0);
    f.element.scrollTop = 1200;
    f.events.get('wheel')(wheel(120));
    assert.equal(prevented, 1, 'ordinary wheeling must not be swallowed');
    assert.equal(f.styles.get('--band'), '0px');
    f.api.dispose(f.element); assert.equal(f.events.size, 0);
});
