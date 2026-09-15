import { test, mock } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// Values produced inside the vm carry the vm's prototypes; compare them by value.
const plain = value => JSON.parse(JSON.stringify(value));
// start() cancels any previous vibration first, so only the real patterns are interesting.
const patterns = vibrations => plain(vibrations.filter(Array.isArray));
const source = readFileSync(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/call-audio.js', import.meta.url), 'utf8');

function fixture({ blocked = false, vibrate = true, saved = {} } = {}) {
    const storage = new Map(Object.entries(saved)), vibrations = [], listeners = new Map();
    const context = {
        state: 'suspended', currentTime: 0, destination: { id: 'out' }, oscillators: [], gains: [], suspended: 0,
        createGain() { const node = gain(); this.gains.push(node); return node; },
        createOscillator() {
            const node = {
                type: '', frequency: { value: 0 }, started: null, stopped: null, disconnected: false, onended: null,
                connect() {}, disconnect() { node.disconnected = true; },
                start(at) { node.started = at; }, stop(at) { if (node.started === null) throw new Error('not started'); node.stopped = at; }
            };
            this.oscillators.push(node); return node;
        },
        async resume() { if (!blocked) this.state = 'running'; this.onstatechange?.(); },
        async suspend() { this.suspended++; this.state = 'suspended'; }
    };
    function gain() {
        const ramps = [];
        return { ramps, gain: {
            value: 1,
            setValueAtTime: (value, at) => ramps.push(['set', value, at]),
            linearRampToValueAtTime: (value, at) => ramps.push(['linear', value, at]),
            exponentialRampToValueAtTime: (value, at) => ramps.push(['exp', value, at])
        }, connect() {}, disconnect() {} };
    }
    const window = { yap: {} };
    const sandbox = {
        window, AudioContext: function () { return context; },
        document: { addEventListener: (name, handler) => listeners.set(name, handler) },
        addEventListener: (name, handler) => listeners.set(name, handler),
        navigator: vibrate ? { vibrate: pattern => { vibrations.push(pattern); return true; } } : {},
        localStorage: { getItem: key => storage.has(key) ? storage.get(key) : null, setItem: (key, value) => storage.set(key, value) },
        setInterval: (...args) => setInterval(...args), clearInterval: (...args) => clearInterval(...args),
        setTimeout: (...args) => setTimeout(...args), clearTimeout: (...args) => clearTimeout(...args),
        Date, Math, Number, Promise
    };
    sandbox.globalThis = sandbox;
    vm.runInContext(source, vm.createContext(sandbox));
    return { ring: window.yap.ring, context, vibrations, storage, listeners };
}

test('a ringtone schedules tones and vibration, and stopping silences every voice', async () => {
    const f = fixture();
    const status = await f.ring.start('ringtone');
    assert.deepEqual(plain(status), { audible: true, vibrating: true });
    assert.equal(f.context.state, 'running');
    assert.ok(f.context.oscillators.length > 0);
    assert.ok(f.context.oscillators.every(osc => osc.started !== null && osc.stopped !== null));
    assert.deepEqual(patterns(f.vibrations), [[900, 3100]]);
    f.ring.stop();
    assert.equal(f.vibrations.at(-1), 0, 'stopping must cancel the vibration too');
    assert.ok(f.context.oscillators.every(osc => osc.disconnected));
    assert.equal(f.context.suspended, 1, 'the ring context is parked, not left holding the audio route');
    assert.equal(f.ring.state().mode, '');
});

test('blocked autoplay reports silence, still vibrates, and keeps the dialog useful', async () => {
    const f = fixture({ blocked: true });
    const status = await f.ring.start('ringtone');
    assert.deepEqual(plain(status), { audible: false, vibrating: true });
    assert.equal(f.context.oscillators.length, 0);
    assert.deepEqual(patterns(f.vibrations), [[900, 3100]]);
    f.ring.stop();
});

test('with neither audio nor vibration the ring is honestly silent', async () => {
    const f = fixture({ blocked: true, vibrate: false });
    assert.deepEqual(plain(await f.ring.start('ringtone')), { audible: false, vibrating: false });
    f.ring.stop();
});

test('a second call replaces the first ring instead of stacking on top of it', async () => {
    const f = fixture();
    await f.ring.start('ringtone');
    const first = f.context.oscillators.length;
    await f.ring.start('ringtone');
    assert.equal(f.context.oscillators.length, first * 2, 'one cadence per start, never two rings at once');
    assert.ok(f.context.oscillators.slice(0, first).every(osc => osc.disconnected), 'the earlier ring is torn down');
    assert.ok(f.context.oscillators.slice(first).every(osc => !osc.disconnected));
    f.ring.stop();
});

test('ringback is quieter than the ringtone at the same volume', async () => {
    const peak = async mode => {
        const f = fixture();
        await f.ring.start(mode);
        const value = Math.max(...f.context.gains.flatMap(node => node.ramps.map(([, value]) => value)));
        f.ring.stop();
        return value;
    };
    assert.ok(await peak('ringback') < await peak('ringtone'));
});

test('ringback never vibrates: the caller already knows they are calling', async () => {
    const f = fixture();
    assert.deepEqual(plain(await f.ring.start('ringback')), { audible: true, vibrating: false });
    assert.deepEqual(patterns(f.vibrations), []);
    f.ring.stop();
});

test('the ring repeats on its cadence and stops for good when told to', async () => {
    mock.timers.enable({ apis: ['setInterval', 'setTimeout'] });
    try {
        const f = fixture();
        await f.ring.start('ringtone');
        const first = f.context.oscillators.length;
        mock.timers.tick(4000);
        assert.equal(f.context.oscillators.length, first * 2, 'the cadence repeats');
        f.ring.stop();
        mock.timers.tick(20000);
        assert.equal(f.context.oscillators.length, first * 2, 'no cadence survives stop');
    } finally { mock.timers.reset(); }
});

test('a preview plays one cadence and stops itself', async () => {
    mock.timers.enable({ apis: ['setInterval', 'setTimeout'] });
    try {
        const f = fixture();
        await f.ring.preview('classic');
        const played = f.context.oscillators.length;
        assert.ok(played > 0);
        assert.deepEqual(patterns(f.vibrations), [], 'a preview in Settings must not buzz the device');
        mock.timers.tick(10000);
        assert.equal(f.context.oscillators.length, played);
        assert.equal(f.ring.state().mode, '');
        assert.equal(f.storage.get('yap-ringtone'), 'classic');
    } finally { mock.timers.reset(); }
});

test('a preview cannot cut off a call that is actually ringing', async () => {
    const f = fixture();
    await f.ring.start('ringtone');
    assert.deepEqual(plain(await f.ring.preview('chime')), { audible: false, vibrating: false });
    assert.equal(f.ring.state().mode, 'ringtone');
    f.ring.stop();
});

test('leaving the page stops the ring', async () => {
    const f = fixture();
    await f.ring.start('ringtone');
    f.listeners.get('pagehide')();
    assert.equal(f.ring.state().mode, '');
    assert.ok(f.context.oscillators.every(osc => osc.disconnected));
});

test('stored preferences are validated, and a missing volume is not a silent one', async () => {
    assert.deepEqual(plain(fixture().ring.preference()), { tone: 'classic', volume: .7 });
    assert.deepEqual(plain(fixture({ saved: { 'yap-ringtone': 'evil', 'yap-ring-volume': '11' } }).ring.preference()), { tone: 'classic', volume: .7 });
    const f = fixture();
    assert.equal(f.ring.setTone('nonsense'), 'classic');
    assert.equal(f.ring.setTone('chime'), 'chime');
    assert.equal(f.ring.setVolume(5), 1);
    assert.equal(f.ring.setVolume(-1), 0);
    assert.equal(f.ring.setVolume('0.25'), .25);
    assert.deepEqual(plain(f.ring.preference()), { tone: 'chime', volume: .25 });
});

test('a muted ring volume plays nothing, and the silent tone only vibrates', async () => {
    const muted = fixture({ saved: { 'yap-ring-volume': '0' } });
    assert.deepEqual(plain(await muted.ring.start('ringtone')), { audible: false, vibrating: true });
    assert.equal(muted.context.oscillators.length, 0);
    muted.ring.stop();
    const silent = fixture({ saved: { 'yap-ringtone': 'silent' } });
    assert.deepEqual(plain(await silent.ring.start('ringtone')), { audible: false, vibrating: true });
    assert.equal(silent.context.oscillators.length, 0);
    assert.deepEqual(patterns(silent.vibrations), [[500, 400, 500, 1600]]);
    silent.ring.stop();
});

test('every tone offered in Settings can actually be rung', async () => {
    const f = fixture();
    for (const tone of f.ring.tones()) {
        assert.equal(f.ring.setTone(tone.id), tone.id, `${tone.id} is a real tone`);
        assert.ok(tone.label.length > 0);
        const before = f.context.oscillators.length;
        await f.ring.start('ringtone');
        assert.ok(tone.id === 'silent' ? f.context.oscillators.length === before : f.context.oscillators.length > before, tone.id);
        f.ring.stop();
    }
});

test('an earlier tap in the session primes audio and parks it again until something rings', async () => {
    const f = fixture();
    assert.equal(f.ring.state().unlocked, false);
    await f.listeners.get('pointerdown')();
    await new Promise(resolve => setTimeout(resolve, 0));
    assert.equal(f.ring.state().unlocked, true);
    assert.equal(f.context.state, 'suspended', 'priming must not leave the audio route open');
    assert.deepEqual(plain(await f.ring.start('ringtone')), { audible: true, vibrating: true });
    f.ring.stop();
});
