import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createEncryption } from '../wwwroot/encryption.mjs';
import * as pgp from '../wwwroot/vendor/openpgp/openpgp.min.mjs';

function client() {
    const data = new Map();
    return { api: createEncryption({ get: async key => structuredClone(data.get(key)), put: async (key, value) => data.set(key, structuredClone(value)) }), data };
}
const tenantId = crypto.randomUUID();
// The server's checked answer for an account that has published nothing: the only input that
// lets a root be created. Every other answer, including an unreachable one, must refuse.
const emptyAccount = { kind: 'account-identity', checked: true, directory: null, recoveryArchive: null };
async function account(credentialId = crypto.randomUUID()) {
    const c = client(), scope = { tenantId, credentialId };
    const initial = await c.api.initialize(scope, emptyAccount);
    await c.api.acceptDirectory(scope, initial.directory);
    return { ...c, scope, ...initial };
}
function context(a, overrides = {}) { return { tenantId, threadId: crypto.randomUUID(), messageId: crypto.randomUUID(), senderId: a.scope.credentialId, parentId: null, isThreadReply: false, kind: 'message', ...overrides }; }
const stored = a => a.data.get(`${a.scope.tenantId}:${a.scope.credentialId}`);

test('enrollment persists before publishing and retry retains device and root', async () => {
    const a = client(), scope = { tenantId, credentialId: crypto.randomUUID() };
    const first = await a.api.initialize(scope, emptyAccount), retry = await a.api.initialize(scope, emptyAccount);
    assert.deepEqual(first.directory, retry.directory);
    assert.equal((await a.api.status(scope)).approved, false);
    await a.api.acceptDirectory(scope, first.directory);
    assert.equal((await a.api.status(scope)).approved, true);
    assert.equal((await a.api.status(`${tenantId.replaceAll('-', '')}:${scope.credentialId.replaceAll('-', '')}`)).deviceId, first.directory.devices[0].deviceId);
});

test('signed multi-recipient message decrypts for both peers and sender', async () => {
    const a = await account(), b = await account(), c = await account();
    const ctx = context(a), payload = { text: 'Private hello 👋', attachments: [] };
    const encrypted = await a.api.encrypt(a.scope, ctx, payload, [a.directory, b.directory, c.directory]);
    assert.ok(!encrypted.includes(payload.text));
    for (const receiver of [a, b, c]) assert.deepEqual(await receiver.api.decrypt(receiver.scope, ctx, encrypted, a.directory), payload);
    const stranger = await account();
    await assert.rejects(stranger.api.decrypt(stranger.scope, ctx, encrypted, a.directory));
});

test('signed context prevents cross-message, thread, parent, sender and tenant replay', async () => {
    const a = await account(), b = await account(), ctx = context(a);
    const encrypted = await a.api.encrypt(a.scope, ctx, { text: 'hello' }, [a.directory, b.directory]);
    for (const changes of [{ messageId: crypto.randomUUID() }, { threadId: crypto.randomUUID() }, { parentId: crypto.randomUUID() }, { isThreadReply: true }, { senderId: b.scope.credentialId }, { tenantId: crypto.randomUUID() }, { kind: 'attachment' }]) {
        await assert.rejects(b.api.decrypt(b.scope, { ...ctx, ...changes }, encrypted, a.directory));
    }
});

test('tampered, plaintext and unsigned envelopes fail closed', async () => {
    const a = await account(), b = await account(), ctx = context(a);
    const encrypted = await a.api.encrypt(a.scope, ctx, { text: 'secret' }, [a.directory, b.directory]);
    const lines = encrypted.split('\n'), index = lines.length - 5;
    lines[index] = (lines[index][0] === 'A' ? 'B' : 'A') + lines[index].slice(1);
    await assert.rejects(b.api.decrypt(b.scope, ctx, lines.join('\n'), a.directory));
    await assert.rejects(b.api.decrypt(b.scope, ctx, '{"text":"plaintext"}', a.directory));
    const unsigned = await pgp.encrypt({ message: await pgp.createMessage({ text: 'unsigned' }), encryptionKeys: await pgp.readKey({ armoredKey: b.directory.devices[0].encryptionPublicKey }) });
    await assert.rejects(b.api.decrypt(b.scope, ctx, unsigned, a.directory));
});

test('attacker signature and forged sender device are rejected', async () => {
    const a = await account(), b = await account(), attacker = await account(), ctx = context(a);
    const bytes = new TextEncoder().encode('forged');
    const envelope = await pgp.encrypt({ message: await pgp.createMessage({ binary: bytes }), encryptionKeys: await pgp.readKey({ armoredKey: b.directory.devices[0].encryptionPublicKey }), signingKeys: await pgp.readPrivateKey({ armoredKey: stored(attacker).device.signingPrivateKey }) });
    await assert.rejects(b.api.decrypt(b.scope, ctx, envelope, a.directory));
});

test('signed directory, immutable root pin and rollback protection', async () => {
    const a = await account(), b = await account();
    await b.api.acceptDirectory(b.scope, a.directory);
    const forged = structuredClone(a.directory); forged.devices[0].encryptionPublicKey = b.directory.devices[0].encryptionPublicKey;
    await assert.rejects(b.api.acceptDirectory(b.scope, forged));
    const replacement = await account(a.scope.credentialId);
    await assert.rejects(b.api.acceptDirectory(b.scope, replacement.directory), /key changed/);
    const newClient = client(), proposal = await newClient.api.proposeDevice(a.scope);
    const update = await a.api.approveDevice(a.scope, proposal, a.directory);
    await b.api.acceptDirectory(b.scope, update.directory);
    await assert.rejects(b.api.acceptDirectory(b.scope, a.directory), /older device roster/);
    await assert.rejects(b.api.verifyFingerprint(b.scope, a.scope.credentialId, '0'.repeat(40)));
    const fp = (await b.api.acceptDirectory(b.scope, update.directory)).fingerprint;
    await b.api.verifyFingerprint(b.scope, a.scope.credentialId, fp);
    assert.ok((await b.api.status(b.scope)).verifiedContacts.includes(a.scope.credentialId));
});

test('device approval activates a fresh identity and revoked recipient stops receiving new data', async () => {
    const a = await account(), b = await account(), second = client();
    const proposal = await second.api.proposeDevice(b.scope);
    const update = await b.api.approveDevice(b.scope, proposal, b.directory);
    assert.equal((await second.api.status(b.scope)).approved, false);
    await second.api.importApproval(b.scope, update.approval, update.directory);
    await b.api.acceptDirectory(b.scope, update.directory);
    const ctx = context(a), encrypted = await a.api.encrypt(a.scope, ctx, { text: 'before revoke' }, [a.directory, update.directory]);
    assert.equal((await second.api.decrypt(b.scope, ctx, encrypted, a.directory)).text, 'before revoke');
    const revoked = await b.api.revokeDevice(b.scope, proposal.deviceId, update.directory);
    assert.equal(stored(b).directory.devices.find(d => d.deviceId === proposal.deviceId).revocation, null, 'pending CAS must not mutate confirmed roster');
    await b.api.acceptDirectory(b.scope, revoked.directory);
    const next = context(a), nextEncrypted = await a.api.encrypt(a.scope, next, { text: 'after revoke' }, [a.directory, revoked.directory]);
    await assert.rejects(second.api.decrypt(b.scope, next, nextEncrypted, a.directory));
    assert.equal((await b.api.decrypt(b.scope, next, nextEncrypted, a.directory)).text, 'after revoke');
    await assert.rejects(a.api.encrypt(a.scope, context(a), {}, [a.directory, update.directory]), /older device roster/);
});

test('recovery rejects wrong key/account, uses fresh device and preserves old history decryption', async () => {
    const a = await account(), b = await account(), fresh = client();
    const ctx = context(a), envelope = await a.api.encrypt(a.scope, ctx, { text: 'history' }, [a.directory, b.directory]);
    const backup = await b.api.exportRecovery(b.scope);
    assert.ok(!backup.recoveryArchive.includes(stored(b).rootPrivateKey));
    await assert.rejects(fresh.api.recovery(b.scope, '0'.repeat(64), backup.recoveryArchive, b.directory));
    await assert.rejects(fresh.api.recovery(a.scope, backup.recoveryKey, backup.recoveryArchive, b.directory));
    assert.equal((await fresh.api.status(b.scope)).enrolled, false);
    const recovered = await fresh.api.recovery(b.scope, backup.recoveryKey, backup.recoveryArchive, b.directory);
    assert.notEqual((await fresh.api.status(b.scope)).deviceId, (await b.api.status(b.scope)).deviceId);
    assert.equal((await fresh.api.status(b.scope)).approved, false);
    await fresh.api.acceptDirectory(b.scope, recovered.directory);
    assert.equal((await fresh.api.decrypt(b.scope, ctx, envelope, a.directory)).text, 'history');
    assert.equal((await fresh.api.status({ tenantId: crypto.randomUUID(), credentialId: b.scope.credentialId })).enrolled, false);
});

test('binary attachments authenticate bytes and never return partial plaintext', async () => {
    const a = await account(), b = await account(), ctx = context(a, { kind: 'attachment', attachmentId: crypto.randomUUID() });
    const data = crypto.getRandomValues(new Uint8Array(64000));
    const encrypted = await a.api.encryptBytes(a.scope, ctx, data, [a.directory, b.directory]);
    assert.ok(encrypted instanceof Uint8Array);
    assert.deepEqual(await b.api.decryptBytes(b.scope, ctx, encrypted, a.directory), data);
    encrypted[encrypted.length - 20] ^= 1;
    await assert.rejects(b.api.decryptBytes(b.scope, ctx, encrypted, a.directory));
});

test('call keys encrypt only to exact active devices and bind the actual signer', async () => {
    const a = await account(), b = await account(), extra = client();
    const proposal = await extra.api.proposeDevice(b.scope);
    const added = await b.api.approveDevice(b.scope, proposal, b.directory);
    await extra.api.importApproval(b.scope, added.approval, added.directory);
    const deviceId = a.directory.devices[0].deviceId;
    const ctx = context(a, { kind: 'call-control', senderDeviceId: deviceId, recipientDeviceId: proposal.deviceId, callId: crypto.randomUUID(), epoch: 1, rosterBinding: 'roster-digest' });
    const envelope = await a.api.encryptToDevices(a.scope, ctx, { key: 'test-key' }, [a.directory, added.directory], [proposal.deviceId]);
    assert.equal((await extra.api.decrypt(b.scope, ctx, envelope, a.directory)).key, 'test-key');
    await assert.rejects(b.api.decrypt(b.scope, ctx, envelope, a.directory));
    await assert.rejects(a.api.encryptToDevices(a.scope, { ...ctx, senderDeviceId: crypto.randomUUID() }, {}, [a.directory, added.directory], [proposal.deviceId]));
    await assert.rejects(a.api.encryptToDevices(a.scope, ctx, {}, [a.directory, added.directory], [crypto.randomUUID()]));
    await assert.rejects(a.api.encrypt(a.scope, { ...context(a), expectedSenderDirectoryRevision: 100 }, {}, [a.directory, b.directory]));
});

test('recovery key stays stable and recovery revokes previous active identities', async () => {
    const a = await account(), extra = client(), fresh = client();
    const proposal = await extra.api.proposeDevice(a.scope);
    const added = await a.api.approveDevice(a.scope, proposal, a.directory);
    await a.api.acceptDirectory(a.scope, added.directory);
    const first = await a.api.exportRecovery(a.scope), second = await a.api.exportRecovery(a.scope);
    assert.equal(first.recoveryKey, second.recoveryKey);
    const restored = await fresh.api.recovery(a.scope, first.recoveryKey, first.recoveryArchive, added.directory);
    assert.equal(restored.recoveryKey, first.recoveryKey);
    assert.equal(restored.directory.devices.filter(d => !d.revocation).length, 1);
    assert.equal(restored.directory.devices.filter(d => !!d.revocation).length, 3);
    assert.notEqual(restored.directory.devices.find(d => !d.revocation).deviceId, proposal.deviceId);
});

test('approval rotates the owner before transferring history and retries the exact staged result', async () => {
    const owner = await account(), sender = await account(), target = client();
    const oldOwner = structuredClone(stored(owner).device);
    const before = context(sender), history = await sender.api.encrypt(sender.scope, before, { text: 'Earlier photo message' }, [sender.directory, owner.directory]);
    const proposal = await target.api.proposeDevice(owner.scope);
    const first = await owner.api.approveDevice(owner.scope, proposal, owner.directory);
    const retry = await owner.api.approveDevice(owner.scope, proposal, owner.directory);
    assert.deepEqual(retry, first);
    assert.equal((await owner.api.status(owner.scope)).deviceId, oldOwner.deviceId, 'pending CAS must not change the active device');
    assert.ok(first.directory.devices.find(d => d.deviceId === oldOwner.deviceId).revocation);
    assert.equal(first.directory.devices.filter(d => !d.revocation).length, 2);
    await target.api.importApproval(owner.scope, first.approval, first.directory);
    assert.equal((await target.api.decrypt(owner.scope, before, history, sender.directory)).text, 'Earlier photo message');
    // Simulate lost POST response: the next call sees the confirmed server roster.
    const confirmedRetry = await owner.api.approveDevice(owner.scope, proposal, first.directory);
    assert.equal(confirmedRetry.alreadyPublished, true); assert.deepEqual(confirmedRetry.approval, first.approval);
    assert.notEqual((await owner.api.status(owner.scope)).deviceId, oldOwner.deviceId);
    const after = context(sender), future = await sender.api.encrypt(sender.scope, after, { text: 'New encrypted message' }, [sender.directory, first.directory]);
    for (const recipient of [owner, target]) assert.equal((await recipient.api.decrypt(owner.scope, after, future, sender.directory)).text, 'New encrypted message');
    await assert.rejects(pgp.decrypt({ message: await pgp.readMessage({ armoredMessage: future }), decryptionKeys: await pgp.readPrivateKey({ armoredKey: oldOwner.encryptionPrivateKey }) }));
    const outgoing = context(owner), reply = await owner.api.encrypt(owner.scope, outgoing, { text: 'Rotated owner can send' }, [first.directory, sender.directory]);
    assert.equal((await sender.api.decrypt(sender.scope, outgoing, reply, first.directory)).text, 'Rotated owner can send');
    assert.equal((await target.api.status(owner.scope)).canApproveDevices, false);
    await assert.rejects(target.api.exportRecovery(owner.scope), /owner device/);
});

test('recipient count accepts existing 101-account conversation bound', async () => {
    const a = await account();
    await assert.rejects(a.api.encrypt(a.scope, context(a), {}, new Array(101).fill(a.directory)), /Duplicate recipient account/);
    await assert.rejects(a.api.encrypt(a.scope, context(a), {}, new Array(102).fill(a.directory)), /Missing recipient directories/);
});

const streamOf = (bytes, chunkSize = 8192) => new ReadableStream({
    start(controller) { for (let n = 0; n < bytes.length; n += chunkSize) controller.enqueue(bytes.slice(n, n + chunkSize)); controller.close(); }
});
async function collect(stream) { const chunks = []; for await (const chunk of stream) chunks.push(chunk); return new Uint8Array(Buffer.concat(chunks)); }
function quarantine() {
    const chunks = [], calls = { write: 0, commit: 0, abort: 0 };
    return { calls, sink: { async write(chunk) { calls.write++; chunks.push(chunk.slice()); }, async commit() { calls.commit++; return new Uint8Array(Buffer.concat(chunks)); }, async abort() { calls.abort++; chunks.length = 0; } } };
}
test('streamed attachments commit only after final integrity, signature and context verification', async () => {
    const a = await account(), b = await account(), ctx = context(a, { kind: 'attachment' });
    const bytes = new Uint8Array(2 * 1024 * 1024 + 123); for (let n = 0; n < bytes.length; n++) bytes[n] = n % 251;
    const encrypted = await collect(await a.api.encryptStream(a.scope, ctx, streamOf(bytes), [a.directory, b.directory]));
    const success = quarantine();
    const plain = await b.api.decryptStream(b.scope, ctx, streamOf(encrypted, 4093), a.directory, success.sink);
    assert.deepEqual(plain, bytes); assert.equal(success.calls.commit, 1); assert.equal(success.calls.abort, 0); assert.ok(success.calls.write > 1);
    const wrongContext = quarantine();
    await assert.rejects(b.api.decryptStream(b.scope, { ...ctx, messageId: crypto.randomUUID() }, streamOf(encrypted), a.directory, wrongContext.sink));
    assert.equal(wrongContext.calls.commit, 0); assert.equal(wrongContext.calls.abort, 1);
    const corrupt = encrypted.slice(); corrupt[corrupt.length - 12] ^= 1;
    const failed = quarantine();
    await assert.rejects(b.api.decryptStream(b.scope, ctx, streamOf(corrupt), a.directory, failed.sink));
    assert.equal(failed.calls.commit, 0); assert.equal(failed.calls.abort, 1);
    const truncated = quarantine();
    await assert.rejects(b.api.decryptStream(b.scope, ctx, streamOf(encrypted.subarray(0, encrypted.length - 40)), a.directory, truncated.sink));
    assert.equal(truncated.calls.commit, 0); assert.equal(truncated.calls.abort, 1);
});

test('attachments larger than buffered limit stream with bounded chunks and no accumulation', async () => {
    const a = await account(), b = await account(), ctx = context(a, { kind: 'attachment' });
    const expected = createHash('sha256'), actual = createHash('sha256');
    let chunks = 0, bytes = 0, maximumChunk = 0, committed = false;
    const source = new ReadableStream({ pull(controller) {
        if (chunks === 1280) { controller.close(); return; }
        const chunk = new Uint8Array(65536).fill(chunks++ % 251); expected.update(chunk); controller.enqueue(chunk);
    } });
    const encrypted = await a.api.encryptStream(a.scope, ctx, source, [a.directory, b.directory]);
    await b.api.decryptStream(b.scope, ctx, encrypted, a.directory, {
        async write(chunk) { bytes += chunk.length; maximumChunk = Math.max(maximumChunk, chunk.length); actual.update(chunk); },
        async commit() { committed = true; },
        async abort() { assert.fail('Valid large stream must not abort.'); }
    });
    assert.equal(bytes, 80 * 1024 * 1024); assert.ok(maximumChunk <= 1024 * 1024);
    assert.equal(actual.digest('hex'), expected.digest('hex')); assert.equal(committed, true);
});


test('late enrollment opens the original 7 MB attachment through an encrypted message key without reupload', async () => {
    const sender = await account(), ready = await account();
    const ctx = context(sender, { kind: 'attachment' });
    const bytes = new Uint8Array(7 * 1024 * 1024); for (let n = 0; n < bytes.length; n++) bytes[n] = n % 251;
    const upload = await sender.api.encryptAttachment(sender.scope, ctx, streamOf(bytes), [sender.directory, ready.directory]);
    const ciphertext = await collect(upload.stream), originalHash = createHash('sha256').update(ciphertext).digest('hex');
    const late = await account();
    await assert.rejects(late.api.decryptStream(late.scope, ctx, streamOf(ciphertext), sender.directory, quarantine().sink));
    const messageContext = { ...ctx, kind: 'message' }, payload = { text: '', attachments: [{ key: upload.key }] };
    const envelope = await sender.api.encrypt(sender.scope, messageContext, payload, [sender.directory, ready.directory, late.directory]);
    assert.ok(!envelope.includes(upload.key.data));
    const received = await late.api.decrypt(late.scope, messageContext, envelope, sender.directory);
    const sink = quarantine();
    assert.deepEqual(await late.api.decryptStream(late.scope, ctx, streamOf(ciphertext), sender.directory, sink.sink, received.attachments[0].key), bytes);
    assert.equal(sink.calls.commit, 1);
    assert.equal(createHash('sha256').update(ciphertext).digest('hex'), originalHash);
    for (const badContext of [{ ...ctx, messageId: crypto.randomUUID() }, { ...ctx, kind: 'message' }]) {
        const fail = quarantine();
        await assert.rejects(late.api.decryptStream(late.scope, badContext, streamOf(ciphertext), sender.directory, fail.sink, upload.key));
        assert.equal(fail.calls.commit, 0); assert.equal(fail.calls.abort, 1);
    }
    const corrupted = ciphertext.slice(); corrupted[corrupted.length - 12] ^= 1;
    const fail = quarantine();
    await assert.rejects(late.api.decryptStream(late.scope, ctx, streamOf(corrupted), sender.directory, fail.sink, upload.key));
    assert.equal(fail.calls.commit, 0); assert.equal(fail.calls.abort, 1);
    const wrongKey = { ...upload.key, data: '00'.repeat(32) };
    await assert.rejects(ready.api.decryptStream(ready.scope, ctx, streamOf(ciphertext), sender.directory, quarantine().sink, wrongKey));
});

test('reset is staged until confirmation, retries safely, and fresh keys cannot decrypt old messages', async () => {
    const a = await account(), b = await account(), ctx = context(b);
    const encrypted = await b.api.encrypt(b.scope, ctx, { text: 'Before reset' }, [a.directory, b.directory]);
    const pending = await a.api.prepareReset(a.scope, a.directory);
    assert.deepEqual(await a.api.prepareReset(a.scope, a.directory), pending);
    assert.equal((await a.api.decrypt(a.scope, ctx, encrypted, b.directory)).text, 'Before reset');
    assert.equal(await a.api.confirmReset(a.scope, a.directory), false);
    assert.equal(await a.api.confirmReset(a.scope, pending.directory), true);
    assert.equal(await a.api.confirmReset(a.scope, pending.directory), true, 'cleanup retry must not create another identity');
    assert.equal((await a.api.status(a.scope)).resetHistoryPending, true);
    await assert.rejects(a.api.decrypt(a.scope, ctx, encrypted, b.directory));
    assert.notEqual(pending.directory.rootPublicKey, a.directory.rootPublicKey);
    assert.equal(pending.directory.revision, a.directory.revision + 1);
    assert.notEqual(pending.recoveryKey, a.recoveryKey);
    const fresh = client();
    await assert.rejects(fresh.api.recovery(a.scope, a.recoveryKey, pending.recoveryArchive, pending.directory));
    const restored = await fresh.api.recovery(a.scope, pending.recoveryKey, pending.recoveryArchive, pending.directory);
    assert.equal(restored.directory.rootPublicKey, pending.directory.rootPublicKey);
    await a.api.acknowledgeReset(a.scope);
    assert.equal((await a.api.status(a.scope)).resetHistoryPending, false);
});

test('contacts must verify a replacement identity, retain old message verification and never downgrade the current pin', async () => {
    const a = await account(), b = await account(), ctx = context(a);
    const old = await a.api.encrypt(a.scope, ctx, { text: 'Old message' }, [a.directory, b.directory]);
    await b.api.decrypt(b.scope, ctx, old, a.directory);
    const next = await a.api.prepareReset(a.scope, a.directory);
    await a.api.confirmReset(a.scope, next.directory);
    const inspected = await b.api.inspectDirectory(b.scope, next.directory);
    assert.equal(inspected.changed, true); assert.equal(inspected.verified, false);
    await assert.rejects(b.api.acceptDirectory(b.scope, next.directory), /key changed/);
    await assert.rejects(b.api.verifyDirectory(b.scope, next.directory, '0'.repeat(40)), /does not match/);
    await b.api.verifyDirectory(b.scope, next.directory, inspected.fingerprint);
    assert.equal((await b.api.decrypt(b.scope, ctx, old, a.directory)).text, 'Old message');
    assert.equal(stored(b).pins[a.scope.credentialId].rootFingerprint, inspected.fingerprint);
    await assert.rejects(b.api.acceptDirectory(b.scope, a.directory), /key changed/);
    const current = context(a), envelope = await a.api.encrypt(a.scope, current, { text: 'New message' }, [next.directory, b.directory]);
    assert.equal((await b.api.decrypt(b.scope, current, envelope, next.directory)).text, 'New message');
    await assert.rejects(b.api.prepareReset(b.scope, next.directory), /another account/);
});

test('a former device stays locked after reset and can rejoin with the new recovery key or explicit approval', async () => {
    for (const method of ['recovery', 'approval']) {
        const old = await account(), fresh = client();
        await fresh.api.proposeDevice(old.scope);
        const next = await fresh.api.prepareReset(old.scope, old.directory);
        await fresh.api.confirmReset(old.scope, next.directory);
        assert.equal(await old.api.observeOwnDirectory(old.scope, next.directory), true);
        assert.equal((await old.api.status(old.scope)).approved, false);
        assert.equal(stored(old).rootPublicKey, old.directory.rootPublicKey, 'observation must retain old keys');
        if (method === 'recovery') {
            await assert.rejects(old.api.recovery(old.scope, old.recoveryKey, next.recoveryArchive, next.directory));
            const result = await old.api.recovery(old.scope, next.recoveryKey, next.recoveryArchive, next.directory);
            await old.api.acceptDirectory(old.scope, result.directory);
        } else {
            const proposal = await old.api.proposeDevice(old.scope);
            assert.notEqual(proposal.deviceId, old.directory.devices[0].deviceId);
            assert.deepEqual(await old.api.proposeDevice(old.scope), proposal);
            const result = await fresh.api.approveDevice(old.scope, proposal, next.directory);
            await old.api.importApproval(old.scope, result.approval, result.directory);
        }
        assert.equal((await old.api.status(old.scope)).approved, true);
        assert.equal((await old.api.status(old.scope)).resetHistoryPending, true);
        assert.equal(stored(old).rootPublicKey, next.directory.rootPublicKey);
    }
});

// Signing in on a device with no local keys used to mint a brand-new account root, which orphaned
// every message already encrypted to the old one. Creating a root is now a decision taken against
// the server's checked answer, never a side effect of finding local state missing.
test('an account that already has an identity cannot be re-enrolled, and an unknown one cannot either', async () => {
    const a = await account(), fresh = client();
    const published = { kind: 'account-identity', checked: true, directory: a.directory, recoveryArchive: a.recoveryArchive };
    await assert.rejects(fresh.api.initialize(a.scope, published), /already has encrypted messages/);
    // A published archive without a roster is still an identity: enrolling over it orphans history.
    await assert.rejects(fresh.api.initialize(a.scope, { kind: 'account-identity', checked: true, directory: null, recoveryArchive: a.recoveryArchive }), /already has encrypted messages/);
    // Offline, a 5xx, or a caller that simply forgot to ask: none of these mean "no identity".
    await assert.rejects(fresh.api.initialize(a.scope, { kind: 'account-identity', checked: false, directory: null, recoveryArchive: null }), /could not check/);
    await assert.rejects(fresh.api.initialize(a.scope), /could not check/);
    await assert.rejects(fresh.api.initialize(a.scope, { ...emptyAccount, directory: { ...a.directory, credentialId: crypto.randomUUID() } }), /another account/);
    assert.equal(fresh.data.size, 0, 'nothing at all was created for the account');
    assert.deepEqual(await fresh.api.status(a.scope), { enrolled: false, approved: false, deviceId: null, rootFingerprint: null,
        directoryRevision: 0, resetHistoryPending: false, canApproveDevices: false, verifiedContacts: [] });
    // A genuinely empty account still enrolls, and repeated calls still return that same identity.
    const empty = { ...a.scope, credentialId: crypto.randomUUID() };
    const created = await fresh.api.initialize(empty, emptyAccount);
    await fresh.api.acceptDirectory(empty, created.directory);
    const again = await fresh.api.initialize(empty, { kind: 'account-identity', checked: true, directory: created.directory, recoveryArchive: created.recoveryArchive });
    assert.deepEqual([again.directory, again.recoveryKey], [created.directory, created.recoveryKey]);
    // An identity this account never confirmed is never offered as a replacement for a published one.
    const other = { ...a.scope, credentialId: crypto.randomUUID() }, unpublished = await fresh.api.initialize(other, emptyAccount);
    await assert.rejects(fresh.api.initialize(other, { kind: 'account-identity', checked: true, directory: { ...a.directory, tenantId: other.tenantId, credentialId: other.credentialId }, recoveryArchive: null }), /already has encrypted messages/);
    const kept = await fresh.api.initialize(other, { kind: 'account-identity', checked: true, directory: unpublished.directory, recoveryArchive: null });
    assert.deepEqual([kept.directory, kept.recoveryKey], [unpublished.directory, unpublished.recoveryKey]);
});

// The start-fresh flow is the deliberate opposite of the guard above: it replaces the identity on
// purpose, from a device that has nothing but its own pending request, and it must keep working.
test('the deliberate start-fresh flow still replaces the identity from a device that cannot recover', async () => {
    const a = await account(), fresh = client();
    await fresh.api.proposeDevice(a.scope);
    await assert.rejects(fresh.api.initialize(a.scope, { kind: 'account-identity', checked: true, directory: a.directory, recoveryArchive: a.recoveryArchive }), /already has encrypted messages/);
    const pending = await fresh.api.prepareReset(a.scope, a.directory);
    assert.notEqual(pending.directory.rootPublicKey, a.directory.rootPublicKey);
    assert.equal(pending.directory.revision, a.directory.revision + 1);
    assert.equal(await fresh.api.confirmReset(a.scope, pending.directory), true);
    const status = await fresh.api.status(a.scope);
    assert.equal(status.approved, true);
    assert.equal(status.resetHistoryPending, true, 'the person is told their old messages stayed behind');
});
