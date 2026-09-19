import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/haptics.js', import.meta.url), 'utf8');

// vibrate:false is iOS Safari exactly: the property is absent, not a function that fails.
function fixture({ vibrate = true, reduced = false, hidden = false, saved = null } = {}) {
    const pulses = [], storage = new Map();
    if (saved !== null) storage.set('yap-haptics', saved);
    const clock = { value: 1000 };
    const window = { yap: {} };
    const sandbox = {
        window,
        document: { get hidden() { return hidden; } },
        navigator: vibrate ? { vibrate: pattern => { pulses.push(pattern); return true; } } : {},
        localStorage: { getItem: key => storage.has(key) ? storage.get(key) : null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
        matchMedia: () => ({ matches: reduced }),
        performance: { now: () => clock.value }
    };
    vm.runInNewContext(source, sandbox);
    return { haptics: window.yap.haptics, pulses, storage, advance: ms => { clock.value += ms; } };
}

test('a surface asks for a tap, not a buzz', () => {
    const { haptics, pulses, advance } = fixture();
    assert.equal(haptics.buzz('tap'), true);
    advance(100);
    haptics.buzz('press');
    advance(100);
    haptics.buzz('commit');
    assert.deepEqual(pulses, [8, 12, 14]);
    // Nothing here is long enough to read as a ring; the longest is under a frame and a half.
    assert.ok(Math.max(...pulses) <= 15);
});

test('an unknown kind still taps rather than throwing', () => {
    const { haptics, pulses } = fixture();
    haptics.buzz('nonsense');
    assert.deepEqual(pulses, [8]);
});

test('the setting is honoured and remembered as the absence of a choice', () => {
    const { haptics, pulses, storage, advance } = fixture();
    assert.equal(haptics.enabled(), true);
    assert.equal(haptics.setEnabled(false), false);
    assert.equal(storage.get('yap-haptics'), 'off');
    advance(500);
    assert.equal(haptics.buzz('tap'), false);
    // Turning it off also cancels anything already running.
    assert.deepEqual(pulses, [0]);
    haptics.setEnabled(true);
    assert.equal(storage.has('yap-haptics'), false);
    advance(500);
    assert.equal(haptics.buzz('tap'), true);
});

test('a stored off survives a reload', () => {
    const { haptics, pulses } = fixture({ saved: 'off' });
    assert.equal(haptics.enabled(), false);
    assert.equal(haptics.buzz('tap'), false);
    assert.deepEqual(pulses, []);
});

test('no vibration support is a silent no-op, not a failure', () => {
    const { haptics, pulses } = fixture({ vibrate: false });
    assert.equal(haptics.supported(), false);
    assert.equal(haptics.enabled(), true);
    assert.equal(haptics.buzz('press'), false);
    assert.deepEqual(pulses, []);
    // The setting still stores, so a device that gains support later honours the choice.
    assert.equal(haptics.setEnabled(false), false);
});

test('reduced motion and a hidden tab stay still', () => {
    assert.equal(fixture({ reduced: true }).haptics.buzz('tap'), false);
    assert.equal(fixture({ hidden: true }).haptics.buzz('tap'), false);
});

test('two surfaces from one gesture produce one pulse', () => {
    const { haptics, pulses, advance } = fixture();
    haptics.buzz('press');
    advance(20);
    assert.equal(haptics.buzz('tap'), false);
    advance(80);
    assert.equal(haptics.buzz('tap'), true);
    assert.deepEqual(pulses, [12, 8]);
});

test('a gesture that already spoke mutes the surface it raises', () => {
    const { haptics, pulses, advance } = fixture();
    haptics.buzz('press');
    haptics.mute(600);
    advance(300); // The menu opens a few renders later and must stay quiet.
    assert.equal(haptics.buzz('press'), false);
    advance(400);
    assert.equal(haptics.buzz('tap'), true);
    assert.deepEqual(pulses, [12, 8]);
});

test('a per-frame caller cannot turn taps into a buzz', () => {
    const { haptics, pulses, advance } = fixture();
    for (let frame = 0; frame < 30; frame++) { haptics.buzz('tap'); advance(16); }
    assert.ok(pulses.length <= 8, `30 frames produced ${pulses.length} pulses`);
});
