import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const read = name => readFileSync(new URL(`../../Presentation/XFramework.Yap.Client/wwwroot/${name}`, import.meta.url), 'utf8');
const source = read('startup-watchdog.js');

function fixture() {
    const notice = { hidden: true }, text = { textContent: '' };
    const splash = { isConnected: true };
    const app = { observers: [] };
    const handlers = {}, records = [];
    let timer = null, cleared = false;
    const context = vm.createContext({
        window: { yap: { diagnostics: { record: (event, details) => records.push([event, details]) } } },
        document: { querySelector: selector => selector === '#app .startup-splash' ? splash : null,
            getElementById: id => id === 'startup-notice' ? notice : id === 'startup-notice-text' ? text : app },
        addEventListener: (name, fn) => (handlers[name] ??= []).push(fn),
        setTimeout: (fn, ms) => { assert.equal(ms, 20000); timer = fn; return 1; },
        clearTimeout: () => cleared = true,
        MutationObserver: class { constructor(fn) { this.fn = fn; } observe(target) { target.observers.push(this); } disconnect() { this.fn = null; } }
    });
    vm.runInContext(source, context);
    return { notice, text, splash, records: () => records.map(([event, details]) => [event, details.reason]), cleared: () => cleared,
        expire: () => timer(), render: () => { splash.isConnected = false; for (const o of app.observers) o.fn?.(); },
        emit: (name, event = {}) => { for (const fn of handlers[name] ?? []) fn(event); } };
}

test('a shell that never renders offers recovery instead of spinning forever', () => {
    const f = fixture();
    assert.equal(f.notice.hidden, true);
    f.expire();
    assert.equal(f.notice.hidden, false);
    assert.match(f.text.textContent, /longer than usual/);
    assert.deepEqual(f.records(), [['startup.stalled', 'slow']]);
});

test('a boot that has already failed says so immediately, without waiting out the patience', () => {
    for (const [name, event] of [['error', { message: 'x' }], ['error', { target: { tagName: 'SCRIPT' } }], ['unhandledrejection', {}]]) {
        const f = fixture();
        f.emit(name, event);
        assert.equal(f.notice.hidden, false);
        assert.match(f.text.textContent, /could not finish starting/);
        assert.deepEqual(f.records(), [['startup.stalled', 'error']]);
    }
});

test('a cosmetic resource failure is not a failed boot', () => {
    const f = fixture();
    f.emit('error', { target: { tagName: 'IMG' } });
    assert.equal(f.notice.hidden, true);
    assert.deepEqual(f.records(), []);
});

test('once .NET renders, App.razor owns the screen and the watchdog stands down', () => {
    const f = fixture();
    f.render();
    assert.equal(f.cleared(), true);
    f.expire();
    f.emit('unhandledrejection', {});
    assert.equal(f.notice.hidden, true);
});

test('the shell actually carries the watchdog and the recovery affordance it reveals', () => {
    const html = read('index.html');
    assert.match(html, /<script defer src="startup-watchdog\.js"><\/script>/);
    assert.match(html, /id="startup-notice"[^>]*hidden/);
    assert.match(html, /id="startup-notice-text"/);
    assert.match(html, /href="\/api\/app-recovery"/);
    // Order matters: app.js assigns window.yap outright, so anything hanging state off it has to run after.
    assert.ok(html.indexOf('app.js') < html.indexOf('startup-watchdog.js'));
});
