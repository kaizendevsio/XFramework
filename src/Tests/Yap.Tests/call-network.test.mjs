// Network hints during a call: which browser events reach VoiceState, under which names, and that
// disposing the watcher really stops them (a finished call must not keep poking the next one).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';

const source = (await fs.readFile(new URL('../../Presentation/XFramework.Yap.Client/wwwroot/call-network.js', import.meta.url), 'utf8'))
    .replaceAll('export function ', 'function ');
const { watch } = vm.runInNewContext(`${source}; ({ watch })`, { Promise });

class Target {
    constructor() { this.listeners = new Map(); }
    addEventListener(name, handler) { (this.listeners.get(name) ?? this.listeners.set(name, new Set()).get(name)).add(handler); }
    removeEventListener(name, handler) { this.listeners.get(name)?.delete(handler); }
    fire(name, event = {}) { for (const handler of this.listeners.get(name) ?? []) handler(event); }
    count() { return [...this.listeners.values()].reduce((sum, set) => sum + set.size, 0); }
}

function fixture({ connection = true } = {}) {
    const scope = new Target();
    scope.document = new Target();
    scope.document.hidden = false;
    scope.navigator = connection ? { connection: Object.assign(new Target(), { type: 'wifi', effectiveType: '4g', rtt: 50, downlink: 10 }) } : {};
    const calls = [];
    const target = { invokeMethodAsync: (method, kind) => { calls.push(`${method}:${kind}`); return Promise.resolve(); } };
    return { scope, calls, handle: watch(target, scope) };
}

test('online, a network change and the page coming back are each reported', () => {
    const { scope, calls } = fixture();
    scope.fire('online');
    scope.navigator.connection.type = 'cellular';
    scope.navigator.connection.fire('change');
    scope.document.fire('visibilitychange');
    scope.fire('pageshow', { persisted: true });
    assert.deepEqual(calls, ['OnCallNetworkChanged:online', 'OnCallNetworkChanged:network',
        'OnCallNetworkChanged:visible', 'OnCallNetworkChanged:visible']);
});

test('a new estimate of the same network is not a network change', () => {
    // Chromium also fires "change" when only its RTT or bandwidth estimate moves.
    // Each "network" hint restarts ICE on the call's UDP path, so only a different connection type may count.
    const { scope, calls } = fixture();
    const connection = scope.navigator.connection;
    for (const [rtt, downlink, effectiveType] of [[100, 8.5, '4g'], [350, 1.2, '3g'], [50, 10, '4g']]) {
        Object.assign(connection, { rtt, downlink, effectiveType });
        connection.fire('change');
    }
    assert.deepEqual(calls, []);
    connection.type = 'cellular';
    connection.fire('change');
    connection.fire('change');
    connection.type = 'wifi';
    connection.fire('change');
    assert.deepEqual(calls, ['OnCallNetworkChanged:network', 'OnCallNetworkChanged:network']);
});

test('without a connection type, change events are not network changes', () => {
    const { scope, calls } = fixture();
    delete scope.navigator.connection.type;
    scope.navigator.connection.fire('change');
    assert.deepEqual(calls, []);
});

test('going to the background is not a hint; only coming back is', () => {
    const { scope, calls } = fixture();
    scope.document.hidden = true;
    scope.document.fire('visibilitychange');
    scope.fire('pageshow', { persisted: false });
    assert.deepEqual(calls, []);
});

test('a browser without the Network Information API still reports the rest', () => {
    const { scope, calls } = fixture({ connection: false });
    scope.fire('online');
    assert.deepEqual(calls, ['OnCallNetworkChanged:online']);
});

test('dispose removes every listener and silences late events', () => {
    const { scope, calls, handle } = fixture();
    const before = scope.count() + scope.document.count() + scope.navigator.connection.count();
    assert.equal(before, 4);
    handle.dispose();
    handle.dispose();
    assert.equal(scope.count() + scope.document.count() + scope.navigator.connection.count(), 0);
    scope.fire('online');
    assert.deepEqual(calls, []);
});

test('a rejected interop call cannot escape as an unhandled rejection', async () => {
    const scope = new Target();
    scope.document = new Target();
    const handle = watch({ invokeMethodAsync: () => Promise.reject(new Error('circuit gone')) }, scope);
    scope.fire('online');
    await new Promise(resolve => setTimeout(resolve, 10));
    handle.dispose();
});
