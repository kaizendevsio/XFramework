import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createChatSocket } from '../../Presentation/XFramework.Yap.Client/wwwroot/chat-socket.mjs';
import { fnv1aHash } from '../../Presentation/XFramework.Yap.Client/wwwroot/vendor/bolt/protocol.js';

const settle = async () => { for (let i = 0; i < 15; i++) await Promise.resolve(); };
function fixture() {
    const clients = [], requests = [], calls = [], timers = new Map();
    const navigator = { onLine: true }, document = { hidden: false };
    let id = 0, apply = async () => {}, failAdmission = false, admissionUrl = '/api/chat/socket?ticket=opaque';
    class Client {
        constructor(url, clientId, name) { Object.assign(this, { url, clientId, name, isConnected: false, invocations: [] }); clients.push(this); }
        async connect() { this.isConnected = true; }
        disconnect() { this.isConnected = false; this.onDisconnected?.(); }
        async invoke(recipient, command, payload) {
            const value = JSON.parse(new TextDecoder().decode(payload));
            this.invocations.push(value);
            if (value.operation === 'ack' && this.holdAck) await this.holdAck;
            if (this.invokeFailure) throw new Error('Connection lost after send');
            return { statusCode: 200, payload: new TextEncoder().encode(JSON.stringify({ status: 200, body: { ok: true } })) };
        }
        push(event) { this.onPush(fnv1aHash('yap.chat.event'), new TextEncoder().encode(JSON.stringify(event))); }
    }
    const api = createChatSocket(() => ({ async invokeMethodAsync(...args) { calls.push(args); return apply(...args); } }), {
        Client, navigator, document, location: { href: 'https://example.test/', host: 'example.test', protocol: 'https:' },
        timeout: () => undefined,
        async fetch(path, options) { requests.push({ path, options }); if (failAdmission) throw new Error('Unavailable'); return { ok: true, status: 200, json: async () => ({ url: admissionUrl, clientId: 'issued-id' }) }; },
        setTimeout(fn, delay) { timers.set(++id, { fn, delay }); return id; },
        clearTimeout(id) { timers.delete(id); }
    });
    return { api, clients, requests, calls, timers, navigator, document,
        setApply(fn) { apply = fn; }, setFailure(value) { failAdmission = value; },
        setAdmissionUrl(value) { admissionUrl = value; },
        async tick() { const [key, timer] = timers.entries().next().value; timers.delete(key); timer.fn(); await settle(); return timer.delay; } };
}
const event = (sequence, eventId = `event-${sequence}`) => ({ sequence, eventId, kind: 'MessageCreated', messages: [] });

test('admission is same-origin and token bound; watching another thread reuses the account socket', async () => {
    const f = fixture(); f.api.watch('account', 'first', 'csrf'); await settle();
    assert.equal(f.clients.length, 1); assert.equal(f.clients[0].name, 'yap-browser');
    assert.equal(f.clients[0].url, 'wss://example.test/api/chat/socket?ticket=opaque');
    assert.equal(f.requests[0].options.headers['X-Yap-Account'], 'account');
    assert.equal(f.requests[0].options.headers.RequestVerificationToken, 'csrf');
    f.api.watch('account', 'second', 'csrf'); await settle();
    assert.equal(f.clients.length, 1);
    assert.deepEqual(f.clients[0].invocations.map(x => x.body.threadId), ['first', 'second']);
});

test('delivery events ACK only after C# application completes, and replay IDs are deduplicated', async () => {
    const f = fixture(); f.api.watch('account', null, 'csrf'); await settle();
    let release; f.setApply(() => new Promise(resolve => release = resolve));
    f.clients[0].push(event(1)); await settle();
    assert.equal(f.clients[0].invocations.filter(x => x.operation === 'ack').length, 0);
    release(); await settle();
    assert.deepEqual(f.clients[0].invocations.at(-1), { operation: 'ack', body: { sequence: 1 } });
    f.clients[0].disconnect(); await f.tick();
    f.clients[1].push(event(1)); await settle();
    assert.equal(f.calls.length, 1);
    assert.deepEqual(f.clients[1].invocations.at(-1), { operation: 'ack', body: { sequence: 1 } });
});

test('failed apply and sequence gaps reconnect without acknowledging lost events', async () => {
    const f = fixture(); f.api.watch('account', null, 'csrf'); await settle();
    f.setApply(async () => { throw new Error('SQLite unavailable'); });
    f.clients[0].push(event(1)); await settle();
    assert.equal(f.clients[0].isConnected, false);
    assert.equal(f.clients[0].invocations.filter(x => x.operation === 'ack').length, 0);
    await f.tick(); f.clients[1].push(event(2)); await settle();
    assert.equal(f.clients[1].isConnected, false);
    assert.equal(f.calls.length, 1);
});

test('slow readers have a bounded queue and account switches discard stale callbacks and IDs', async () => {
    const f = fixture(); f.api.watch('first', null, 'csrf'); await settle();
    let release; f.setApply(() => new Promise(resolve => release = resolve));
    const old = f.clients[0]; old.push(event(1)); await settle();
    for (let i = 2; i <= 131; i++) old.push(event(i));
    assert.equal(old.isConnected, false);
    f.api.watch('second', null, 'new-csrf'); await settle();
    release(); await settle(); old.push(event(132)); await settle();
    assert.equal(f.clients.length, 2); assert.equal(f.calls.length, 1);
    f.setApply(async () => {}); f.clients[1].push(event(1)); await settle();
    assert.equal(f.calls[1][1], 'second');
    f.api.watch('', null, ''); old.onDisconnected();
    assert.equal(f.timers.size, 0);
});

test('offline/background retry stays quiet and resume probes a retained socket', async () => {
    const f = fixture(); f.setFailure(true); f.api.watch('account', null, 'csrf'); await settle();
    assert.equal(f.requests.length, 1); f.navigator.onLine = false;
    assert.equal(await f.tick(), 1000); assert.equal(f.requests.length, 1);
    f.navigator.onLine = true; f.document.hidden = true; f.api.resume(); await settle();
    assert.equal(f.requests.length, 1);
    f.document.hidden = false; f.api.resume(); await settle();
    assert.equal(f.requests.length, 2); assert.equal(await f.tick(), 2000);
    f.setFailure(false); f.api.resume(); await settle();
    assert.equal(f.clients.length, 1);
    const before = f.clients[0].invocations.length; f.api.resume(); await settle();
    assert.equal(f.clients[0].invocations.length, before + 1);
});

test('writes fall back only before sending; uncertain failures are never replayed in JS', async () => {
    const f = fixture(); assert.equal(await f.api.request('account', 'send', {}), null);
    f.api.watch('account', null, 'csrf'); await settle();
    assert.equal(await f.api.request('other-account', 'send', {}), null);
    const client = f.clients[0]; client.invokeFailure = true;
    await assert.rejects(f.api.request('account', 'send', { id: 'stable-id' }), /Connection lost/);
    assert.equal(client.invocations.filter(x => x.operation === 'send').length, 1);
});

test('slow acknowledgment round trips do not block application of newer messages', async () => {
    const f = fixture(); f.api.watch('account', null, 'csrf'); await settle();
    let release;
    const client = f.clients[0]; client.holdAck = new Promise(resolve => release = resolve);
    client.push(event(1)); await settle();
    client.push(event(2)); client.push(event(3)); await settle();
    assert.equal(f.calls.length, 3, 'Messages apply while the previous ACK is in flight');
    assert.equal(client.invocations.filter(x => x.operation === 'ack').length, 1);
    release(); await settle();
    assert.deepEqual(client.invocations.at(-1), { operation: 'ack', body: { sequence: 3 } });
});

test('admission cannot redirect the account socket to another origin or downgrade TLS', async () => {
    for (const url of ['wss://evil.test/api/chat/socket', 'ws://example.test/api/chat/socket']) {
        const f = fixture(); f.setAdmissionUrl(url); f.api.watch('account', null, 'csrf'); await settle();
        assert.equal(f.clients.length, 0); assert.equal(f.timers.size, 1);
    }
});
