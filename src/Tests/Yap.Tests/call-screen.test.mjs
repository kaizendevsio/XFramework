// The call screen's gestures: when the controls may hide by themselves, how a picture is fitted,
// and where a dragged self-view comes to rest. The pure rules are tested directly; the controller
// runs against a small fake of the dialog, with fake timers, so "after four quiet seconds" is exact.
import { test, mock } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';

const source = (await fs.readFile(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/call-screen.js', import.meta.url), 'utf8'))
    .replaceAll('export function ', 'function ').replaceAll('export const ', 'var ');

/** A dialog whose contents are described by which selectors currently match. */
class FakeElement {
    constructor(name = 'div') { this.name = name; this.attributes = new Map(); this.listeners = new Map(); this.dataset = {}; this.style = {}; }
    setAttribute(key, value) { this.attributes.set(key, String(value)); }
    getAttribute(key) { return this.attributes.has(key) ? this.attributes.get(key) : null; }
    removeAttribute(key) { this.attributes.delete(key); }
    hasAttribute(key) { return this.attributes.has(key); }
    toggleAttribute(key) { if (this.hasAttribute(key)) this.removeAttribute(key); else this.setAttribute(key, ''); }
    addEventListener(name, handler) { (this.listeners.get(name) ?? this.listeners.set(name, []).get(name)).push(handler); }
    removeEventListener(name, handler) { this.listeners.set(name, (this.listeners.get(name) ?? []).filter(x => x !== handler)); }
    dispatch(name, event) { for (const handler of this.listeners.get(name) ?? []) handler(event); }
    getBoundingClientRect() { return this.rect ?? { left: 0, top: 0, width: 0, height: 0 }; }
    contains(other) { return other === this || other?.parent === this || other?.parent?.parent === this; }
    closest(selector) { return this.closestMap?.[selector] ?? null; }
    get offsetWidth() { return 0; }
    matches() { return false; }
    querySelector() { return null; }
}

function fixture({ video = true, remote = true, hold = false, keyboard = false, group = false } = {}) {
    const root = new FakeElement('dialog');
    root.isConnected = true;
    root.rect = { left: 0, top: 0, width: 375, height: 812 };
    const screen = new FakeElement(); screen.parent = root;
    const header = new FakeElement('header'), tray = new FakeElement(); header.parent = tray.parent = screen;
    const pip = new FakeElement(); pip.parent = screen;
    const stage = new FakeElement('canvas'); stage.parent = screen; stage.closestMap = {};
    const state = { video, remote, hold, group };
    root.querySelector = selector => {
        if (selector === '.vidscreen') return state.video ? screen : null;
        if (selector === '.vidscreen.is-group') return state.video && state.group ? screen : null;
        if (selector === '.vidscreen.self-stage') return null;
        if (selector === '.vidscreen .pip') return state.video ? pip : null;
        if (selector.includes('canvas')) return state.video && state.remote ? stage : null;
        if (selector.includes('data-call-hold')) return state.hold ? new FakeElement() : null;
        return null;
    };
    root.querySelectorAll = selector => selector.includes('data-chrome-part') && state.video ? [header, tray] : [];
    const context = vm.createContext({
        document: { documentElement: { dataset: keyboard ? { input: 'keyboard' } : {} } },
        MutationObserver: class { observe() {} disconnect() {} },
        requestAnimationFrame: callback => callback(),
        matchMedia: () => ({ matches: true }),
        setTimeout: (...args) => setTimeout(...args), clearTimeout: id => clearTimeout(id),
        Date: { now: () => Date.now() }, Math,
    });
    vm.runInContext(source, context);
    context.mount(root);
    return { root, screen, header, tray, pip, stage, state, context };
}

const hidden = root => root.getAttribute('data-chrome') === 'hidden';

test('the controls hide themselves after four quiet seconds over someone\'s video', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root, header, tray } = fixture();
    mock.timers.tick(3999);
    assert.equal(hidden(root), false, 'not yet');
    mock.timers.tick(1);
    assert.equal(hidden(root), true);
    assert.ok(header.hasAttribute('inert') && tray.hasAttribute('inert'), 'hidden controls are out of reach, not just invisible');
});

test('the controls never hide on their own during a voice call', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root } = fixture({ video: false });
    mock.timers.tick(20000);
    assert.equal(hidden(root), false);
});

test('the controls stay while there is only your own camera to look at', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root } = fixture({ remote: false });
    mock.timers.tick(20000);
    assert.equal(hidden(root), false, 'ringing, or the other side has no camera on');
});

test('the controls stay while a menu or a notice is open', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root, state } = fixture({ hold: true });
    mock.timers.tick(20000);
    assert.equal(hidden(root), false);
    state.hold = false;
    mock.timers.tick(4000);
    assert.equal(hidden(root), true, 'and go once it closes and things are quiet again');
});

test('keyboard users keep their controls', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root } = fixture({ keyboard: true });
    mock.timers.tick(20000);
    assert.equal(hidden(root), false);
});

test('a tap on the video toggles the controls, and any key brings them back', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root, stage, context } = fixture();
    const tap = at => root.dispatch('click', { target: stage, timeStamp: at });
    tap(0);
    assert.equal(hidden(root), false, 'a single tap waits to be sure it is not a double tap');
    mock.timers.tick(context.DOUBLE_TAP_MS);
    assert.equal(hidden(root), true);
    tap(1000);
    mock.timers.tick(context.DOUBLE_TAP_MS);
    assert.equal(hidden(root), false);
    tap(2000); mock.timers.tick(context.DOUBLE_TAP_MS);
    assert.equal(hidden(root), true);
    root.dispatch('keydown', { key: 'Tab' });
    assert.equal(hidden(root), false);
});

test('a mouse that moves the controls back in and then clicks does not hide them again', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root, stage, context } = fixture();
    mock.timers.tick(4000);
    assert.equal(hidden(root), true);
    root.dispatch('pointermove', { target: stage, pointerType: 'mouse', pointerId: 9 });
    assert.equal(hidden(root), false, 'moving the mouse shows them');
    root.dispatch('click', { target: stage, timeStamp: 5000 });
    mock.timers.tick(context.DOUBLE_TAP_MS);
    assert.equal(hidden(root), false);
});

test('a double tap is not two single taps', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root, stage, context } = fixture();
    root.dispatch('click', { target: stage, timeStamp: 0 });
    root.dispatch('click', { target: stage, timeStamp: 120 });
    mock.timers.tick(context.DOUBLE_TAP_MS * 2);
    assert.equal(hidden(root), false, 'the controls did not flash off and back on');
});

test('a dragged self-view comes to rest in the corner it was thrown towards', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root, pip } = fixture();
    assert.equal(root.getAttribute('data-corner'), 'tr', 'top right to begin with');
    pip.rect = { left: 259, top: 114, width: 104, height: 156 };
    root.dispatch('pointerdown', { target: pip, pointerId: 1, clientX: 300, clientY: 180, timeStamp: 0, button: 0 });
    root.dispatch('pointermove', { target: pip, pointerId: 1, clientX: 250, clientY: 300, timeStamp: 40 });
    // Released left of centre in the top half, but moving fast down and left.
    pip.rect = { left: 150, top: 240, width: 104, height: 156 };
    root.dispatch('pointermove', { target: pip, pointerId: 1, clientX: 190, clientY: 360, timeStamp: 60 });
    root.dispatch('pointerup', { target: pip, pointerId: 1, clientX: 190, clientY: 360, timeStamp: 60 });
    assert.equal(root.getAttribute('data-corner'), 'bl');
});

test('a tap on the self-view swaps it with the person you are talking to', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root, pip } = fixture();
    const tap = () => {
        root.dispatch('pointerdown', { target: pip, pointerId: 2, clientX: 300, clientY: 180, timeStamp: 0, button: 0 });
        root.dispatch('pointerup', { target: pip, pointerId: 2, clientX: 302, clientY: 181, timeStamp: 90 });
    };
    tap();
    assert.equal(root.hasAttribute('data-swapped'), true);
});

test('a group starts with the self-view bottom right, away from the row of faces', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root } = fixture({ group: true });
    assert.equal(root.getAttribute('data-corner'), 'br');
});

test('a dialog that has gone away lets go of its listeners, even with the controls hidden', t => {
    mock.timers.enable({ apis: ['setTimeout', 'Date'] });
    t.after(() => mock.timers.reset());
    const { root } = fixture();
    mock.timers.tick(4000);
    assert.equal(hidden(root), true);
    root.isConnected = false; // the call ended while nobody was touching the screen
    mock.timers.tick(4000);
    assert.equal([...root.listeners.values()].flat().length, 0);
});

// ---- The rules on their own -------------------------------------------------------------------

const rules = (() => { const context = vm.createContext({ Math }); vm.runInContext(source, context); return context; })();

test('the full stage fills only when the picture is the same way round as the screen', () => {
    assert.equal(rules.fitFor(720, 1280, 375, 812), 'cover', 'portrait phone on portrait phone: trim an edge');
    assert.equal(rules.fitFor(480, 640, 375, 812), 'cover', 'a 3:4 phone camera too');
    assert.equal(rules.fitFor(1280, 720, 375, 812), 'contain', 'landscape sender on a portrait screen stays whole');
    assert.equal(rules.fitFor(1280, 720, 1440, 900), 'cover', 'landscape on a desktop window');
    assert.equal(rules.fitFor(1920, 480, 1440, 900), 'contain', 'an extreme crop is never taken silently');
    assert.equal(rules.fitFor(0, 0, 375, 812), 'contain', 'no picture yet: crop nothing');
});

test('a group tile fills unless the crop would be extreme', () => {
    assert.equal(rules.fitFor(720, 1280, 359, 290, true), 'cover', 'a portrait sender in a wide tile');
    assert.equal(rules.fitFor(640, 480, 176, 290, true), 'cover', 'a 4:3 landscape sender in a column');
    assert.equal(rules.fitFor(1280, 720, 176, 290, true), 'contain', 'but a 16:9 one would lose two thirds of the frame');
});

test('corners and flicks', () => {
    assert.equal(rules.nearestCorner(10, 10, 375, 812), 'tl');
    assert.equal(rules.nearestCorner(370, 10, 375, 812), 'tr');
    assert.equal(rules.nearestCorner(10, 800, 375, 812), 'bl');
    assert.equal(rules.nearestCorner(370, 800, 375, 812), 'br');
    const flung = rules.projectRelease(300, 200, 0, 2);
    assert.equal(rules.nearestCorner(flung.x, flung.y, 375, 812), 'br', 'a flick downwards carries it past the middle');
});

test('the auto-hide rule', () => {
    const quiet = { video: true, remoteVideo: true, holding: false, keyboard: false, idleMs: 4000 };
    assert.equal(rules.shouldAutoHide(quiet), true);
    assert.equal(rules.shouldAutoHide({ ...quiet, idleMs: 3999 }), false);
    assert.equal(rules.shouldAutoHide({ ...quiet, video: false }), false);
    assert.equal(rules.shouldAutoHide({ ...quiet, remoteVideo: false }), false);
    assert.equal(rules.shouldAutoHide({ ...quiet, holding: true }), false);
    assert.equal(rules.shouldAutoHide({ ...quiet, keyboard: true }), false);
});
