import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Worker } from 'node:worker_threads';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';
import { cryptoRpc } from '../wwwroot/crypto-rpc.mjs';

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
function pair(methods) {
    const channel = new MessageChannel();
    const server = cryptoRpc(channel.port1, methods), client = cryptoRpc(channel.port2);
    return { client, close() { client.close(); server.close(); } };
}
test('a CPU-bound operation runs on another thread while the caller remains responsive', async () => {
    const module = new URL('../wwwroot/crypto-rpc.mjs', import.meta.url).href;
    const worker = new Worker(`const { parentPort } = require('node:worker_threads');
        import(${JSON.stringify(module)}).then(({cryptoRpc}) => cryptoRpc(parentPort, {
          work() { const end = Date.now()+250; while(Date.now()<end) {} return 42; }
        }));`, { eval: true });
    const channel = new MessageChannel();
    // Adapt the Node Worker event interface to the browser Worker shape.
    worker.on('message', data => channel.port1.postMessage(data));
    channel.port1.onmessage = ({data}) => worker.postMessage(data);
    const rpc = cryptoRpc(channel.port2);
    let ticks = 0; const timer = setInterval(() => ticks++, 10);
    try { assert.equal(await rpc.call('work', []), 42); assert.ok(ticks >= 10); }
    finally { clearInterval(timer); rpc.close(); channel.port1.close(); await worker.terminate(); }
});
test('streams cross ports with backpressure and exact bytes, including nested results', async () => {
    let pulls = 0;
    const source = new ReadableStream({ pull(c) { if (pulls === 32) c.close(); else c.enqueue(new Uint8Array(65536).fill(pulls++)); } }, { highWaterMark: 0 });
    const p = pair({ echo: stream => ({ stream, key: 'metadata' }) });
    try {
        const result = await p.client.call('echo', [source]);
        await sleep(20); assert.equal(pulls, 0, 'No whole-file eager read');
        const reader = result.stream.getReader();
        for (let i = 0; i < 32; i++) {
            const {value, done} = await reader.read(); assert.equal(done, false);
            assert.equal(value.length, 65536); assert.ok(value.every(x => x === i));
            assert.equal(pulls, i + 1);
        }
        assert.equal((await reader.read()).done, true);
        assert.equal(result.key, 'metadata');
    } finally { p.close(); }
});
test('quarantine sink commits only after worker verification, failure aborts', async () => {
    for (const valid of [true, false]) {
        const calls = [];
        const p = pair({ async decrypt(sink, valid) {
            try { await sink.write(new Uint8Array([1, 2, 3])); if (!valid) throw new Error('Bad signature'); await sink.commit(); }
            catch (error) { await sink.abort(); throw error; }
        } });
        const sink = { write: data => { calls.push([...data]); }, commit: () => { calls.push('commit'); }, abort: () => { calls.push('abort'); } };
        try {
            if (valid) await p.client.call('decrypt', [sink, valid]);
            else await assert.rejects(p.client.call('decrypt', [sink, valid]), /Bad signature/);
            await sleep(20);
            assert.deepEqual(calls, [[1, 2, 3], valid ? 'commit' : 'abort']);
        } finally { p.close(); }
    }
});
test('rejected operations cancel their input streams and do not dispatch inherited methods', async () => {
    let cancelled = false;
    const p = pair({ reject() { throw new Error('Invalid roster'); } });
    try {
        await assert.rejects(p.client.call('reject', [new ReadableStream({ cancel() { cancelled = true; } })]), /Invalid roster/);
        await sleep(20); assert.equal(cancelled, true);
        await assert.rejects(p.client.call('toString', []), /Unsupported/);
    } finally { p.close(); }
});
test('worker failure rejects pending calls instead of leaving the UI waiting forever', async () => {
    const p = pair({ wait: () => new Promise(() => {}) });
    const call = p.client.call('wait', []);
    p.client.close(new Error('Worker stopped'));
    await assert.rejects(call, /Worker stopped/);
    p.close();
});

test('interop recreates a crashed worker on the next call without falling back to UI-thread crypto', async () => {
    const workers = [];
    class BrowserWorker {
        listeners = new Map();
        constructor(url, options) { this.url = url; this.options = options; workers.push(this); }
        addEventListener(name, handler) { this.listeners.set(name, handler); }
        postMessage(request) { this.request = request; }
        terminate() { this.terminated = true; }
    }
    const window = { yap: {} };
    const source = (await readFile(new URL('../wwwroot/encryption-loader.js', import.meta.url), 'utf8'))
        .replace("import('./crypto-rpc.mjs')", 'Promise.resolve({ cryptoRpc: globalThis.cryptoRpc })');
    vm.runInNewContext(source, { window, document: { baseURI: 'https://yap.test/' }, Worker: BrowserWorker, cryptoRpc, URL });
    const first = window.yap.encryption.status('scope');
    await sleep(0);
    assert.equal(workers[0].options.type, 'module');
    workers[0].listeners.get('error')();
    await assert.rejects(first, /worker stopped/i);
    assert.equal(workers[0].terminated, true);
    const second = window.yap.encryption.status('scope');
    await sleep(0);
    assert.equal(workers.length, 2);
    workers[1].onmessage({ data: { id: workers[1].request.id, result: { approved: true } } });
    assert.deepEqual(await second, { approved: true });
});
