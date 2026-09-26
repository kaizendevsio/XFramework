import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/motion.js', import.meta.url), 'utf8');

function load(vendor, stored = new Map(), app = null) {
    const transitions = [];
    const classes = new Set();
    const documentElement = { dataset: {}, classList: { toggle: (name, on) => on ? classes.add(name) : classes.delete(name), remove: name => classes.delete(name) } };
    const document = {
        documentElement, readyState: 'complete', hidden: false, addEventListener() {}, getElementById: () => app,
        startViewTransition(update) {
            const done = Promise.resolve().then(update);
            const transition = { ready: done, finished: done, skipTransition() {} };
            transitions.push(transition); return transition;
        }
    };
    let navigated = 0;
    const window = { yap: { diagnostics: { health: { navigated: () => navigated++ } } } };
    const observers = [];
    vm.runInNewContext(source, { window, document, URL, Promise, setTimeout, clearTimeout, performance,
        requestAnimationFrame: () => 0, ResizeObserver: class { observe() {} unobserve() {} },
        MutationObserver: class { constructor(callback) { observers.push(callback); } observe() {} },
        location: { href: 'https://yap.test/' }, navigator: { vendor },
        matchMedia: () => ({ matches: false }),
        localStorage: { getItem: key => stored.get(key) ?? null, setItem: (key, value) => stored.set(key, value), removeItem: key => stored.delete(key) } });
    return { motion: window.yap.motion, transitions, classes, stored, observers, navigated: () => navigated };
}

test('WebKit navigations skip view transitions, whose snapshots it never frees', async () => {
    const apple = load('Apple Computer, Inc.');
    for (const [from, to] of [['/', '/calls'], ['/calls', '/saved'], ['/saved', '/settings'], ['/settings', '/']])
        assert.equal(apple.motion.begin(`https://yap.test${from}`, `https://yap.test${to}`), undefined, 'Navigation is not held for a snapshot.');
    assert.equal(apple.transitions.length, 0);
    assert.equal(apple.motion.transitions.started(), 0);
    assert.equal(apple.motion.transitions.navigations(), 4);
    assert.equal(apple.navigated(), 4, 'Each route change refreshes the session-health record.');
    assert.equal(apple.classes.has('motion-fallback'), false, 'No transform lands on the screen that owns fixed chrome.');
});

test('Chromium keeps its view transitions, and a WebKit device can opt back in to compare', async () => {
    const chrome = load('Google Inc.');
    await chrome.motion.begin('https://yap.test/', 'https://yap.test/calls');
    assert.equal(chrome.transitions.length, 1);
    chrome.motion.complete();

    const apple = load('Apple Computer, Inc.');
    assert.equal(apple.motion.transitions.setPreference('on'), 'on');
    await apple.motion.begin('https://yap.test/', 'https://yap.test/calls');
    assert.equal(apple.transitions.length, 1);
    assert.equal(apple.motion.transitions.setPreference('auto'), 'auto');
    assert.equal(apple.stored.has('yap-view-transitions'), false);
    assert.equal(apple.motion.begin('https://yap.test/calls', 'https://yap.test/saved'), undefined);
    assert.equal(apple.transitions.length, 1);
});

test('same-route navigations are not counted', () => {
    const apple = load('Apple Computer, Inc.');
    apple.motion.begin('https://yap.test/settings', 'https://yap.test/settings?x=1');
    assert.equal(apple.motion.transitions.navigations(), 0);
});

test('without snapshots the tab bar still fades out and back in', () => {
    const app = { querySelectorAll: () => [] };
    const apple = load('Apple Computer, Inc.', new Map(), app);
    const animated = [];
    const tabs = () => ({ nodeType: 1, isConnected: true, matches: selector => selector.startsWith('.shell-tabs'),
        querySelectorAll: () => [], removeAttribute() {}, setAttribute() {}, dataset: {}, remove() {},
        cloneNode() { return tabs(); }, animate(frames) { animated.push(frames); return { finished: Promise.resolve() }; } });
    const parent = { isConnected: true, append() {} };
    apple.motion.begin('https://yap.test/', 'https://yap.test/chat/1');
    assert.equal(apple.classes.has('motion-fallback'), false, 'The screen itself still takes no transform.');
    apple.observers[0]([{ target: parent, removedNodes: [tabs()], addedNodes: [] }]);
    assert.equal(animated.length, 1, 'The leaving bar fades instead of vanishing in one frame.');
    apple.motion.begin('https://yap.test/chat/1', 'https://yap.test/');
    apple.observers[0]([{ target: parent, removedNodes: [], addedNodes: [tabs()] }]);
    assert.equal(animated.length, 2, 'The returning bar fades back in.');
});
