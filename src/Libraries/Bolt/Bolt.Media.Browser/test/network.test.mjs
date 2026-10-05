import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../wwwroot/bolt-media.js', import.meta.url), 'utf8')
    .replaceAll('export ', '').replaceAll('import.meta.url', "'https://example.test/bolt-media.js'");

function fixture(connection) {
    const storage = new Map();
    const sandbox = {
        console, URL, Uint8Array, Float32Array, isSecureContext: true,
        navigator: { connection, mediaDevices: {} },
        localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, String(value)) },
    };
    vm.createContext(sandbox);
    vm.runInContext(source + '\nthis.api = { networkHint, rememberNetworkRate, networkCapKbps, networkKind };', sandbox);
    return { api: sandbox.api, sandbox, storage };
}

test('a connection the browser rates slow caps the start; a fast or unknown one does not', () => {
    assert.equal(fixture({ effectiveType: '3g' }).api.networkCapKbps(), 700);
    assert.equal(fixture({ effectiveType: '2g' }).api.networkCapKbps(), 150);
    assert.equal(fixture({ effectiveType: '4g', saveData: true }).api.networkCapKbps(), 1000);
    assert.equal(fixture({ effectiveType: '4g' }).api.networkCapKbps(), 0);
    assert.equal(fixture(undefined).api.networkCapKbps(), 0, 'Safari has no Network Information API');
});

test('the settled rate of a call is remembered per kind of network, and forgotten after two weeks', () => {
    const f = fixture({ type: 'wifi', effectiveType: '4g' });
    const now = 1_000_000_000_000;
    assert.equal(f.api.rememberNetworkRate(4200, f.sandbox.localStorage, now), true);
    assert.equal(f.api.networkHint(f.sandbox.localStorage, now + 1000).cachedKbps, 4200);
    // Another kind of network knows nothing of it.
    f.sandbox.navigator.connection = { type: 'cellular', effectiveType: '4g' };
    assert.equal(f.api.networkHint(f.sandbox.localStorage, now + 1000).cachedKbps, 0);
    f.sandbox.navigator.connection = { type: 'wifi', effectiveType: '4g' };
    assert.equal(f.api.networkHint(f.sandbox.localStorage, now + 15 * 24 * 3600 * 1000).cachedKbps, 0, 'too old to trust');
});

test('unavailable or corrupt storage gives no hint and never throws', () => {
    const f = fixture({ type: 'wifi' });
    f.storage.set('bolt-network-rates', '{broken');
    assert.equal(f.api.networkHint().cachedKbps, 0);
    const failing = { getItem() { throw new Error('denied'); }, setItem() { throw new Error('denied'); } };
    assert.equal(f.api.networkHint(failing).cachedKbps, 0);
    assert.equal(f.api.rememberNetworkRate(1000, failing), false);
    assert.equal(f.api.rememberNetworkRate(0), false, 'nothing settled, nothing remembered');
});
