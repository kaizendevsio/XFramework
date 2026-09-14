import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';
import { webcrypto } from 'node:crypto';

const source = await readFile(new URL('../wwwroot/device.js', import.meta.url), 'utf8');
const tenant = crypto.randomUUID(), user = crypto.randomUUID();
const scope = `${tenant.replaceAll('-', '')}:${user.replaceAll('-', '')}`;
const key = `${scope.replace(':', '-')}-${crypto.randomUUID().replaceAll('-', '')}.verified`;
const context = { tenantId: tenant, threadId: crypto.randomUUID(), messageId: crypto.randomUUID(), senderId: user, kind: 'attachment', parentId: null };
function setup() {
    const files = new Map(), state = { fetches: 0, decrypts: 0, failSignature: false, failMarker: false, committed: false };
    const missing = () => Object.assign(new Error('Missing'), { name: 'NotFoundError' });
    const directory = {
        async getFileHandle(name, options = {}) {
            if (!files.has(name)) { if (!options.create) throw missing(); files.set(name, new Blob()); }
            return { kind: 'file', name,
                async getFile() { return files.get(name); },
                async createWritable() {
                    const chunks = [];
                    const writable = new WritableStream({
                        write(chunk) { if (state.failMarker && name.endsWith('.ready')) throw new Error('Disk full'); chunks.push(chunk); },
                        close() { files.set(name, new Blob(chunks)); }, abort() { chunks.length = 0; }
                    });
                    // FileSystemWritableFileStream also exposes convenience methods.
                    writable.write = async chunk => { const writer = writable.getWriter(); try { await writer.write(chunk); } finally { writer.releaseLock(); } };
                    writable.close = async () => { const writer = writable.getWriter(); try { await writer.close(); } finally { writer.releaseLock(); } };
                    return writable;
                }
            };
        },
        async removeEntry(name) { if (!files.delete(name)) throw missing(); },
        async *entries() { for (const name of [...files.keys()]) yield [name, { kind: 'file' }]; }
    };
    const yap = { encryption: {
        async decryptStream(_scope, _context, _source, _directory, sink) {
            state.decrypts++; await sink.write(new TextEncoder().encode('private photo'));
            if (state.failSignature) { await sink.abort(); throw new Error('Invalid signature'); }
            state.committed = true; return sink.commit();
        },
        async encryptAttachment(_scope, _context, input) { return { stream: input, key: {} }; }
    } };
    const sandbox = { window: { yap }, yap, navigator: { storage: { getDirectory: async () => ({ getDirectoryHandle: async () => directory }) }, onLine: true },
        crypto: webcrypto, TextEncoder, Blob, URL, Set, Map, Uint8Array, SyntaxError, JSON, Promise, ReadableStream, WritableStream,
        document: { addEventListener() {} }, visualViewport: null, requestAnimationFrame() {}, cancelAnimationFrame() {}, addEventListener() {}, setTimeout, clearTimeout,
        async fetch() { state.fetches++; return { ok: true, body: new Blob(['cipher']).stream() }; } };
    vm.runInNewContext(source, sandbox);
    return { api: yap.device, files, state, sandbox };
}

test('only committed plaintext gets a ready marker; verified offline cache needs no fetch', async () => {
    const { api, files, state } = setup();
    await api.decryptFile(key, '/file', scope, true, context, {});
    assert.equal(state.committed, true); assert.equal(await files.get(key).text(), 'private photo');
    assert.equal(await api.hasVerifiedFile(key, scope, context), true);
    assert.ok(files.has(`${key}.ready`)); assert.equal([...files.keys()].some(k => k.endsWith('.unverified')), false);
    await api.decryptFile(key, '/file', scope, false, context, null);
    assert.equal(state.fetches, 1); assert.equal(state.decrypts, 1);
});

test('signature failure and marker write failure never produce a usable verified cache', async () => {
    for (const failure of ['failSignature', 'failMarker']) {
        const { api, files, state } = setup(); state[failure] = true;
        await assert.rejects(api.decryptFile(key, '/file', scope, true, context, {}));
        assert.equal(await api.hasVerifiedFile(key, scope, context), false);
        assert.equal([...files.keys()].some(k => k.endsWith('.unverified')), false);
    }
});

test('crashed partial final file without ready marker and resized final file are not trusted', async () => {
    const { api, files } = setup(); files.set(key, new Blob(['partial']));
    assert.equal(await api.hasVerifiedFile(key, scope, context), false);
    await assert.rejects(api.decryptFile(key, '/file', scope, false, context, {}), /Connect/);
    await api.decryptFile(key, '/file', scope, true, context, {});
    files.set(key, new Blob(['truncated']));
    assert.equal(await api.hasVerifiedFile(key, scope, context), false);
});

test('failed attachment logs identify the failing step without contents or keys', async () => {
    for (const [failure, phase] of [['failSignature', 'attachment-decrypt'], ['failMarker', 'attachment-commit'], ['fetch', 'attachment-fetch']]) {
        const { api, state, sandbox } = setup(); const events = [];
        sandbox.yap.diagnostics = { record: (event, details) => events.push({ event, ...details }) };
        if (failure === 'fetch') sandbox.fetch = async () => ({ ok: false, status: 403 });
        else state[failure] = true;
        await assert.rejects(api.decryptFile(key, '/private-file', scope, true, context, {}));
        assert.equal(events.length, 1); assert.equal(events[0].phase, phase);
        assert.equal(events[0].event, 'attachment.failed');
        assert.deepEqual(Object.keys(events[0]).sort(), ['event', 'phase', 'status', 'type']);
        if (failure === 'fetch') assert.equal(events[0].status, 403);
        assert.equal(JSON.stringify(events).includes(key), false);
    }
});

test('verified cache binds account and exact signed message context', async () => {
    const { api, state } = setup(); await api.decryptFile(key, '/file', scope, true, context, {});
    assert.equal(await api.hasVerifiedFile(key, scope, { ...context, messageId: crypto.randomUUID() }), false);
    const other = `${tenant.replaceAll('-', '')}:${crypto.randomUUID().replaceAll('-', '')}`;
    await assert.rejects(api.hasVerifiedFile(key, other, context), /account mismatch/);
    await assert.rejects(api.decryptFile(key, '/file', other, true, context, {}), /account mismatch/);
    assert.equal(state.fetches, 1);
});

test('startup removes abandoned quarantine/ciphertext but keeps verified caches', async () => {
    const { api, files } = setup(); files.set('crashed.unverified', new Blob(['secret'])); files.set('crashed.encrypted', new Blob(['cipher'])); files.set(key, new Blob(['saved']));
    await api.watch(null);
    // Housekeeping runs in the background and must not delay opening chats.
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(files.has('crashed.unverified'), false); assert.equal(files.has('crashed.encrypted'), false); assert.equal(files.has(key), true);
});

test('simultaneous readers share one decryption and failures return serializable Guid', async () => {
    const { api, state, sandbox, files } = setup();
    await Promise.all([api.decryptFile(key, '/file', scope, true, context, {}), api.decryptFile(key, '/file', scope, true, context, {})]);
    assert.equal(state.decrypts, 1);
    const missing = await api.uploadEncrypted('missing', 'thread', 'token', scope, context, []);
    assert.equal(missing.status, 410); assert.equal(missing.id, '00000000-0000-0000-0000-000000000000');
    files.set('upload', new Blob(['photo']));
    sandbox.fetch = async () => ({ ok: false, status: 413 });
    const denied = await api.uploadEncrypted('upload', 'thread', 'token', scope, context, []);
    assert.equal(denied.status, 413); assert.equal(denied.id, '00000000-0000-0000-0000-000000000000');
    assert.equal([...files.keys()].some(k => k.endsWith('.encrypted')), false);
});

test('reset removes only the selected account files, including queued previews and verification markers', async () => {
    const { api, files } = setup();
    const queued = crypto.randomUUID();
    for (const name of [key, `${key}.ready`, queued, `${queued}.preview-v2.jpg`, 'another-account-file']) files.set(name, new Blob(['data']));
    await assert.rejects(api.clearAccountFiles('', [queued]));
    assert.equal(files.size, 5);
    await api.clearAccountFiles(scope, [queued]);
    assert.deepEqual([...files.keys()], ['another-account-file']);
    await api.clearAccountFiles(scope, [queued]);
});
