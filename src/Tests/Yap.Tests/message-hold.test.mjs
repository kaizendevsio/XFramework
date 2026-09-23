import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/motion.js', import.meta.url), 'utf8');
function fixture() {
    const handlers = new Map(), timers = new Map(), actions = [], pulses = [];
    let now = 1000, next = 0, replies = 0;
    const element = { classList: { add() {}, remove() {}, toggle() {} }, style: { removeProperty() {}, setProperty() {} },
        querySelector: () => ({ click: () => replies++ }) };
    const bubble = { closest: selector => selector === '[data-swipe-reply]' ? element : null,
        dispatchEvent: event => actions.push(event.type) };
    const target = { closest: selector => selector === '.bub,.photo-open' ? bubble : null };
    const document = { hidden: false, readyState: 'loading', addEventListener: (type, fn) => handlers.set(type, fn) };
    const window = { yap: { haptics: { buzz: kind => pulses.push(kind), mute() {} } } };
    vm.runInNewContext(source, { window, yap: window.yap, document, performance: { now: () => now },
        matchMedia: () => ({ matches: false }),
        setTimeout: (fn, delay) => { const id = ++next; timers.set(id, { fn, at: now + delay }); return id; },
        clearTimeout: id => timers.delete(id), MouseEvent: class { constructor(type) { this.type = type; } } });
    const fire = (type, values = {}) => {
        const event = { target, pointerType: 'touch', isPrimary: true, button: 0, pointerId: 1, clientX: 0, clientY: 0,
            preventDefault() { this.prevented = true; }, stopImmediatePropagation() { this.stopped = true; }, ...values };
        handlers.get(type)?.(event); return event;
    };
    const advance = ms => { now += ms; for (const [id, timer] of timers) if (timer.at <= now) { timers.delete(id); timer.fn(); } };
    return { fire, advance, actions, pulses, document, replies: () => replies };
}

test('short touch or mouse press does not open message actions', () => {
    for (const pointerType of ['touch', 'mouse']) {
        const f = fixture(); f.fire('pointerdown', { pointerType }); f.advance(200); f.fire('pointerup'); f.advance(500);
        assert.deepEqual(f.actions, []); assert.deepEqual(f.pulses, []);
    }
});
test('hold opens actions once with one haptic and suppresses trailing pointer click', () => {
    for (const pointerType of ['touch', 'mouse']) {
        const f = fixture(); f.fire('pointerdown', { pointerType }); f.advance(450); f.advance(1000);
        assert.deepEqual(f.actions, ['contextmenu']); assert.deepEqual(f.pulses, ['press']);
        f.fire('pointerup'); assert.equal(f.fire('click', { detail: 1 }).stopped, true);
        assert.equal(f.fire('click', { detail: 0 }).stopped, undefined, 'keyboard/assistive activation stays usable');
    }
});
test('scrolling, cancellation and a hidden page cancel pending holds', () => {
    for (const cancel of [f => f.fire('pointermove', { clientY: 25 }), f => f.fire('pointercancel'), f => { f.document.hidden = true; f.fire('visibilitychange'); }]) {
        const f = fixture(); f.fire('pointerdown'); f.advance(100); cancel(f); f.advance(500);
        assert.deepEqual(f.actions, []); assert.deepEqual(f.pulses, []);
    }
});
test('swipe to reply does not open the action menu', () => {
    const f = fixture(); f.fire('pointerdown'); f.fire('pointermove', { clientX: 70 }); f.advance(500); f.fire('pointerup');
    assert.equal(f.replies(), 1); assert.deepEqual(f.actions, []);
});
test('native mobile context menu cannot race the hold, desktop right click remains available', () => {
    const f = fixture(); assert.equal(f.fire('contextmenu', { isTrusted: true }).stopped, undefined);
    f.fire('pointerdown'); assert.equal(f.fire('contextmenu', { isTrusted: true }).stopped, true);
    f.advance(450); assert.deepEqual(f.actions, ['contextmenu']);
});
