import * as pgp from './vendor/openpgp/openpgp.min.mjs';

// OpenPGP provides encryption/signatures. This module binds those primitives to
// Yap identities and contexts; it is not a ratchet and provides no forward secrecy.
const config = { aeadProtect: true, preferredAEADAlgorithm: pgp.enums.aead.gcm, aeadChunkSizeByte: 12 };
const encoder = new TextEncoder();
const decoder = new TextDecoder('utf-8', { fatal: true });
const MAX_BYTES = 64 * 1024 * 1024;
const MAX_STREAM_BYTES = 4 * 1024 * 1024 * 1024;
const MAX_DIRECTORY_DEVICES = 16;
// Minting an account root is the one operation that can orphan readable history, so it never
// happens as a side effect of signing in. `initialize` takes the server's checked answer about
// the account instead of guessing from missing local state, and says so in the words the person
// reads: a blocked device that still has its history beats a working one that silently lost it.
const identityExists = 'This account already has encrypted messages set up. Unlock with your password, use your recovery key, or approve this device from a device you already use.';
const identityUnknown = 'Yap could not check whether this account already has encryption set up, so it created nothing. Reconnect and open Yap again.';
const canonical = value => JSON.stringify(normalize(value));
function normalize(value) {
    if (Array.isArray(value)) return value.map(normalize);
    if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map(k => [k, normalize(value[k])]));
    if (typeof value === 'number' && !Number.isSafeInteger(value)) throw new Error('Invalid integer.');
    if (value === undefined) throw new Error('Undefined context value.');
    return value;
}
function check(condition, message) { if (!condition) throw new Error(message); }
function guid(value) {
    const compact = String(value).toLowerCase().replaceAll('-', '');
    check(/^[0-9a-f]{32}$/.test(compact) && compact !== '0'.repeat(32), 'Invalid identity.');
    return `${compact.slice(0, 8)}-${compact.slice(8, 12)}-${compact.slice(12, 16)}-${compact.slice(16, 20)}-${compact.slice(20)}`;
}
function scopeOf(scope) {
    if (typeof scope === 'string') {
        const parts = scope.split(':');
        check(parts.length === 2, 'Invalid account scope.');
        return { tenantId: guid(parts[0]), credentialId: guid(parts[1]) };
    }
    return { tenantId: guid(scope.tenantId), credentialId: guid(scope.credentialId) };
}
const scopeKey = scope => `${scope.tenantId}:${scope.credentialId}`;
const hash = async value => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', encoder.encode(value))), v => v.toString(16).padStart(2, '0')).join('');
const readPublic = armoredKey => pgp.readKey({ armoredKey });
const readPrivate = armoredKey => pgp.readPrivateKey({ armoredKey });
const fingerprint = async armor => (await readPublic(armor)).getFingerprint();
async function signed(value, privateArmor) {
    return pgp.sign({ message: await pgp.createMessage({ binary: encoder.encode(canonical(value)) }), signingKeys: await readPrivate(privateArmor), format: 'armored', config });
}
async function verified(armor, publicArmor) {
    check(typeof armor === 'string' && armor.length <= 1024 * 1024, 'Invalid signed record.');
    const result = await pgp.verify({ message: await pgp.readMessage({ armoredMessage: armor }), verificationKeys: await readPublic(publicArmor), expectSigned: true, format: 'binary', config });
    await requireSignature(result);
    return JSON.parse(decoder.decode(result.data));
}
async function requireSignature(result) {
    // Observe every verification promise even when rejecting a multi-signature
    // packet, avoiding an unhandled rejection from attacker-controlled input.
    const verified = await Promise.allSettled(result.signatures.map(s => s.verified));
    check(result.signatures.length === 1, 'Exactly one expected signature is required.');
    check(verified[0].status === 'fulfilled', 'The expected sender signature is invalid.');
}
function directoryShape(directory) {
    check(Array.isArray(directory.devices) && directory.devices.length > 0 && directory.devices.length <= MAX_DIRECTORY_DEVICES, 'Invalid device roster.');
    check(Number.isSafeInteger(directory.revision) && directory.revision > 0, 'Invalid directory revision.');
    return {
        tenantId: guid(directory.tenantId), credentialId: guid(directory.credentialId), revision: directory.revision,
        rootPublicKey: directory.rootPublicKey, roster: directory.roster,
        devices: directory.devices.map(d => ({ deviceId: guid(d.deviceId), signingPublicKey: d.signingPublicKey, encryptionPublicKey: d.encryptionPublicKey, approval: d.approval, revocation: d.revocation ?? null })).sort((a, b) => a.deviceId.localeCompare(b.deviceId))
    };
}
async function validateDirectory(scope, directory, pins) {
    const d = directoryShape(directory);
    check(d.tenantId === scope.tenantId, 'Directory belongs to another tenant.');
    check(new Set(d.devices.map(x => x.deviceId)).size === d.devices.length, 'Duplicate device identity.');
    const rootFingerprint = await fingerprint(d.rootPublicKey);
    const payload = { v: 1, kind: 'device-roster', tenantId: d.tenantId, credentialId: d.credentialId, revision: d.revision, rootFingerprint, devices: d.devices };
    check(canonical(await verified(d.roster, d.rootPublicKey)) === canonical(payload), 'The signed device roster does not match.');
    const revisions = new Map();
    const deviceFingerprints = new Set([rootFingerprint]);
    for (const device of d.devices) {
        check(device.signingPublicKey !== device.encryptionPublicKey, 'Device signing and encryption keys must differ.');
        const approval = await verified(device.approval, d.rootPublicKey);
        const fields = { v: 1, kind: 'device-approval', tenantId: d.tenantId, credentialId: d.credentialId, deviceId: device.deviceId, signingFingerprint: await fingerprint(device.signingPublicKey), encryptionFingerprint: await fingerprint(device.encryptionPublicKey), approvedRevision: approval.approvedRevision };
        for (const value of [fields.signingFingerprint, fields.encryptionFingerprint]) { check(!deviceFingerprints.has(value), 'Device keys must be unique and separate from the account root.'); deviceFingerprints.add(value); }
        check(Number.isSafeInteger(approval.approvedRevision) && approval.approvedRevision > 0 && approval.approvedRevision <= d.revision && canonical(approval) === canonical(fields), 'Invalid device approval.');
        let revokedRevision = null;
        if (device.revocation) {
            const revocation = await verified(device.revocation, d.rootPublicKey);
            revokedRevision = revocation.revokedRevision;
            check(Number.isSafeInteger(revokedRevision) && revokedRevision >= approval.approvedRevision && revokedRevision <= d.revision && canonical(revocation) === canonical({ v: 1, kind: 'device-revocation', tenantId: d.tenantId, credentialId: d.credentialId, deviceId: device.deviceId, revokedRevision }), 'Invalid device revocation.');
        }
        revisions.set(device.deviceId, { approvedRevision: approval.approvedRevision, revokedRevision });
    }
    const digest = await hash(canonical(payload));
    const previous = pins[d.credentialId];
    if (previous) {
        check(previous.rootFingerprint === rootFingerprint, 'The account security key changed. Verify it with this person.');
        check(d.revision >= previous.revision, 'An older device roster was returned.');
        check(d.revision !== previous.revision || digest === previous.digest, 'Conflicting device rosters were returned.');
    }
    pins[d.credentialId] = { rootFingerprint, revision: d.revision, digest, verified: previous?.verified ?? false };
    return { directory: d, rootFingerprint, digest, revisions };
}
async function keyPair(label, signingOnly = false) {
    return pgp.generateKey({ type: 'ecc', curve: 'ed25519', userIDs: [{ name: label }], ...(signingOnly ? { subkeys: [] } : {}), format: 'armored', config });
}
async function newDevice() {
    const deviceId = crypto.randomUUID();
    const signing = await keyPair(`Yap device ${deviceId}`, true);
    const encryption = await keyPair(`Yap encryption ${deviceId}`);
    return { deviceId, signingPrivateKey: signing.privateKey, signingPublicKey: signing.publicKey, encryptionPrivateKey: encryption.privateKey, encryptionPublicKey: encryption.publicKey };
}
async function approveRecord(scope, device, revision, rootPrivateKey) {
    const approval = await signed({ v: 1, kind: 'device-approval', ...scope, deviceId: device.deviceId, signingFingerprint: await fingerprint(device.signingPublicKey), encryptionFingerprint: await fingerprint(device.encryptionPublicKey), approvedRevision: revision }, rootPrivateKey);
    return { deviceId: device.deviceId, signingPublicKey: device.signingPublicKey, encryptionPublicKey: device.encryptionPublicKey, approval, revocation: null };
}
async function signDirectory(directory, rootPrivateKey) {
    const d = directoryShape({ ...directory, roster: '' });
    d.roster = await signed({ v: 1, kind: 'device-roster', tenantId: d.tenantId, credentialId: d.credentialId, revision: d.revision, rootFingerprint: await fingerprint(d.rootPublicKey), devices: d.devices }, rootPrivateKey);
    return d;
}
function publicStatus(state) {
    return { enrolled: !!state?.device, approved: !!state?.approved, deviceId: state?.device?.deviceId ?? null, rootFingerprint: state?.rootFingerprint ?? null, directoryRevision: state?.directory?.revision ?? 0, resetHistoryPending: !!state?.resetHistoryPending, canApproveDevices: !!state?.rootPrivateKey && !!state?.approved, verifiedContacts: Object.entries(state?.pins ?? {}).filter(([, p]) => p.verified).map(([credentialId]) => credentialId) };
}

// Keys are isolated by signed-in tenant/account. IndexedDB is origin-local; an
// unlocked malicious script on this origin remains outside E2EE's threat model.
export function indexedDbStore() {
    const open = () => new Promise((resolve, reject) => {
        const request = indexedDB.open('yap-encryption-v1', 1);
        request.onupgradeneeded = () => request.result.createObjectStore('accounts');
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
    return {
        async get(key) {
            const db = await open();
            try { return await new Promise((resolve, reject) => { const request = db.transaction('accounts').objectStore('accounts').get(key); request.onsuccess = () => resolve(request.result); request.onerror = () => reject(request.error); }); }
            finally { db.close(); }
        },
        async put(key, value) {
            const db = await open();
            try { await new Promise((resolve, reject) => { const transaction = db.transaction('accounts', 'readwrite'); transaction.objectStore('accounts').put(value, key); transaction.oncomplete = resolve; transaction.onerror = () => reject(transaction.error); transaction.onabort = () => reject(transaction.error); }); }
            finally { db.close(); }
        }
    };
}

export function createEncryption(store = indexedDbStore()) {
    const queues = new Map();
    const locked = (scope, work) => {
        const s = scopeOf(scope), key = scopeKey(s);
        const run = () => work(s, key);
        const action = () => globalThis.navigator?.locks ? navigator.locks.request(`yap-encryption:${key}`, run) : run();
        const result = (queues.get(key) ?? Promise.resolve()).then(action, action);
        queues.set(key, result.catch(() => {}));
        return result;
    };
    const requireState = async key => { const state = await store.get(key); check(state?.device, 'Set up encryption on this device first.'); return state; };
    async function accept(s, state, directory) {
        const result = await validateDirectory(s, directory, state.pins);
        if (result.directory.credentialId === s.credentialId) {
            check(state.rootFingerprint === result.rootFingerprint, 'The account security key changed.');
            const rotation = state.pendingRotation;
            if (rotation && result.directory.revision >= rotation.result.directory.revision) {
                const confirmed = result.directory.devices.find(d => d.deviceId === rotation.device.deviceId && !d.revocation);
                if (confirmed && confirmed.signingPublicKey === rotation.device.signingPublicKey && confirmed.encryptionPublicKey === rotation.device.encryptionPublicKey) {
                    state.device = rotation.device; state.historyKeys = rotation.historyKeys;
                    state.lastApproval = { proposal: rotation.proposal, approval: rotation.result.approval };
                    state.pendingRotation = null;
                }
            }
            const own = result.directory.devices.find(d => d.deviceId === state.device.deviceId);
            check(own && own.signingPublicKey === state.device.signingPublicKey && own.encryptionPublicKey === state.device.encryptionPublicKey, 'This device is not in the approved roster.');
            state.directory = result.directory;
            state.approved = !own.revocation;
            if (!state.pendingRotation) state.pendingDirectory = null;
        }
        return result;
    }
    async function archive(state, s) {
        const recoveryKey = state.recoveryKey ??= Array.from(crypto.getRandomValues(new Uint8Array(32)), x => x.toString(16).padStart(2, '0')).join('');
        check(state.rootPrivateKey, 'Only an account owner device can export recovery.');
        const payload = { v: 1, kind: 'account-recovery', ...s, rootFingerprint: state.rootFingerprint, rootPrivateKey: state.rootPrivateKey, rootPublicKey: state.rootPublicKey, decryptionKeys: [...new Set([state.device.encryptionPrivateKey, ...(state.historyKeys ?? [])])], pins: state.pins, historicalPins: state.historicalPins ?? {} };
        const recoveryArchive = await pgp.encrypt({ message: await pgp.createMessage({ binary: encoder.encode(canonical(payload)) }), passwords: [recoveryKey], signingKeys: await readPrivate(state.rootPrivateKey), format: 'armored', config });
        return { recoveryArchive, recoveryKey };
    }
    function contextOf(s, context) {
        check(context && typeof context === 'object', 'Missing encryption context.');
        const c = { ...context, tenantId: guid(context.tenantId), threadId: guid(context.threadId), messageId: guid(context.messageId), senderId: guid(context.senderId) };
        check(c.tenantId === s.tenantId && typeof c.kind === 'string' && c.kind.length > 0 && c.kind.length < 40, 'Invalid encryption context.');
        check(!('senderDirectoryRevision' in c) && !('recipients' in c), 'Reserved encryption context field.');
        return c;
    }
    function pack(header, bytes) {
        check(bytes instanceof Uint8Array && bytes.length <= MAX_BYTES, 'Attachment exceeds the 64 MiB encryption limit.');
        const h = encoder.encode(canonical(header));
        check(h.length <= 65536, 'Encryption context is too large.');
        const output = new Uint8Array(4 + h.length + bytes.length);
        new DataView(output.buffer).setUint32(0, h.length);
        output.set(h, 4); output.set(bytes, 4 + h.length);
        return output;
    }
    function unpack(bytes) {
        check(bytes instanceof Uint8Array && bytes.length >= 4 && bytes.length <= MAX_BYTES + 65540, 'Invalid encrypted payload size.');
        const length = new DataView(bytes.buffer, bytes.byteOffset, 4).getUint32(0);
        check(length <= 65536 && length + 4 <= bytes.length, 'Invalid encrypted context.');
        return { header: JSON.parse(decoder.decode(bytes.subarray(4, 4 + length))), bytes: bytes.subarray(4 + length) };
    }
    async function prepareEncryption(s, key, context, directories, recipientDeviceIds = null) {
        const state = await requireState(key), c = contextOf(s, context);
        check(state.approved && c.senderId === s.credentialId, 'The sending device is not approved.');
        check(Array.isArray(directories) && directories.length > 0 && directories.length <= 101, 'Missing recipient directories.');
        const recipients = [], encryptionKeys = [], accounts = new Set();
        const selected = recipientDeviceIds === null ? null : new Set(recipientDeviceIds.map(guid));
        check(selected === null || selected.size > 0 && selected.size === recipientDeviceIds.length, 'Invalid selected recipient devices.');
        const included = new Set();
        for (const input of directories) {
            const { directory: d, digest } = await validateDirectory(s, input, state.pins);
            check(!accounts.has(d.credentialId), 'Duplicate recipient account.'); accounts.add(d.credentialId);
            const active = d.devices.filter(device => !device.revocation);
            check(active.length > 0, 'A recipient has no approved devices.');
            if (d.credentialId === s.credentialId) {
                check(d.revision === state.directory.revision && active.some(device => device.deviceId === state.device.deviceId), 'The sending device roster changed.');
            }
            const targets = selected === null ? active : active.filter(device => selected.has(device.deviceId));
            for (const device of targets) { encryptionKeys.push(await readPublic(device.encryptionPublicKey)); included.add(device.deviceId); }
            recipients.push({ credentialId: d.credentialId, revision: d.revision, digest, deviceIds: targets.map(d => d.deviceId).sort() });
        }
        check(accounts.has(s.credentialId), 'Include the sender account in the encrypted recipients.');
        check(selected === null || selected.size === included.size, 'A selected recipient device is missing or revoked.');
        if (c.expectedSenderDeviceId !== undefined) check(guid(c.expectedSenderDeviceId) === state.device.deviceId, 'The expected sending device does not match.');
        if (c.senderDeviceId !== undefined) check(guid(c.senderDeviceId) === state.device.deviceId, 'The sending device does not match.');
        if (c.expectedSenderDirectoryRevision !== undefined) check(c.expectedSenderDirectoryRevision === state.directory.revision, 'The sending device roster revision does not match.');
        recipients.sort((a, b) => a.credentialId.localeCompare(b.credentialId));
        const header = { v: 1, context: c, senderDeviceId: state.device.deviceId, senderDirectoryRevision: state.directory.revision, recipients };
        await store.put(key, state);
        return { header, encryptionKeys, signingKeys: await readPrivate(state.device.signingPrivateKey) };
    }
    async function encryptData(s, key, context, bytes, directories, format, recipientDeviceIds = null) {
        const { header, encryptionKeys, signingKeys } = await prepareEncryption(s, key, context, directories, recipientDeviceIds);
        const output = await pgp.encrypt({ message: await pgp.createMessage({ binary: pack(header, bytes) }), encryptionKeys, signingKeys, format, config });
        if (format === 'armored') check(output.length <= 256 * 1024, 'Encrypted message is too large.');
        return output;
    }
    async function prepareDecryption(s, key, context, senderDirectory) {
        const state = await requireState(key), c = contextOf(s, context);
        const root = await fingerprint(senderDirectory.rootPublicKey), senderId = guid(senderDirectory.credentialId);
        const previous = state.historicalPins?.[senderId]?.[root];
        // Retired roots verify history only. They never become the active recipient identity again.
        const { directory: d, revisions } = await validateDirectory(s, senderDirectory, previous ? { [senderId]: previous } : state.pins);
        check(d.credentialId === c.senderId, 'Unexpected sender directory.');
        await store.put(key, state);
        return { s, c, d, revisions, decryptionKeys: await Promise.all([...new Set([state.device.encryptionPrivateKey, ...(state.historyKeys ?? [])])].map(readPrivate)), verificationKeys: await Promise.all(d.devices.map(device => readPublic(device.signingPublicKey))) };
    }
    async function decryptData(s, key, context, encrypted, senderDirectory, format) {
        const prepared = await prepareDecryption(s, key, context, senderDirectory);
        if (format === 'armored') check(typeof encrypted === 'string' && encrypted.length <= 256 * 1024, 'Invalid encrypted message size.');
        else check(encrypted instanceof Uint8Array && encrypted.length <= MAX_BYTES + 1024 * 1024, 'Invalid encrypted attachment size.');
        const result = await pgp.decrypt({ message: await pgp.readMessage(format === 'armored' ? { armoredMessage: encrypted } : { binaryMessage: encrypted }), decryptionKeys: prepared.decryptionKeys, verificationKeys: prepared.verificationKeys, expectSigned: true, format: 'binary', config });
        await requireSignature(result);
        const { header, bytes } = unpack(result.data);
        await verifyHeader(prepared, result, header);
        return bytes;
    }
    async function verifyHeader({ s, c, d, revisions, sessionKeys }, result, header) {
        check(header.v === 1 && canonical(header.context) === canonical(c), 'This encrypted content belongs to another message or conversation.');
        const sender = d.devices.find(device => device.deviceId === header.senderDeviceId);
        check(sender, 'Unknown sending device.');
        if (c.expectedSenderDeviceId !== undefined) check(guid(c.expectedSenderDeviceId) === sender.deviceId, 'Unexpected sending device.');
        if (c.senderDeviceId !== undefined) check(guid(c.senderDeviceId) === sender.deviceId, 'Unexpected sending device.');
        const publicKey = await readPublic(sender.signingPublicKey);
        check(result.signatures[0].keyID.toHex() === publicKey.getKeyID().toHex(), 'The signature does not match the sending device.');
        const revision = revisions.get(sender.deviceId);
        if (c.expectedSenderDirectoryRevision !== undefined) check(c.expectedSenderDirectoryRevision === header.senderDirectoryRevision, 'The accepted sender roster revision does not match.');
        check(Number.isSafeInteger(header.senderDirectoryRevision) && header.senderDirectoryRevision >= revision.approvedRevision && header.senderDirectoryRevision <= d.revision && (!revision.revokedRevision || header.senderDirectoryRevision < revision.revokedRevision), 'The sending device was not approved for this message.');
        check(sessionKeys || Array.isArray(header.recipients) && header.recipients.some(r => r.credentialId === s.credentialId && Array.isArray(r.deviceIds) && r.deviceIds.length > 0), 'This account is not an encrypted recipient.');
    }
    function boundedStream(source, maximum, prefix = null) {
        check(source instanceof ReadableStream, 'A readable byte stream is required.');
        const reader = source.getReader(); let total = 0, prefixed = !prefix;
        return new ReadableStream({
            async pull(controller) {
                try {
                    if (!prefixed) { prefixed = true; controller.enqueue(prefix); return; }
                    const { done, value } = await reader.read();
                    if (done) { reader.releaseLock(); controller.close(); return; }
                    check(value instanceof Uint8Array, 'Invalid byte stream chunk.');
                    total += value.length; check(total <= maximum, 'Encrypted attachment exceeds the 4 GiB limit.');
                    controller.enqueue(value);
                } catch (error) { await reader.cancel(error).catch(() => {}); controller.error(error); }
            },
            cancel: reason => reader.cancel(reason)
        });
    }
    async function decryptToSink(prepared, source, sink) {
        check(sink && typeof sink.write === 'function' && typeof sink.commit === 'function' && typeof sink.abort === 'function', 'A quarantined output sink is required.');
        let reader, result;
        try {
            result = await pgp.decrypt({ message: await pgp.readMessage({ binaryMessage: boundedStream(source, MAX_STREAM_BYTES + 16 * 1024 * 1024) }), decryptionKeys: prepared.decryptionKeys, sessionKeys: prepared.sessionKeys, verificationKeys: prepared.verificationKeys, expectSigned: true, format: 'binary', config });
            // Signature completion depends on consuming the stream. Its bytes
            // remain quarantined until the final signature/context barrier below.
            for (const signature of result.signatures) signature.verified.catch(() => {});
            reader = result.data.getReader();
            const prefix = new Uint8Array(65540); let prefixLength = 0, headerLength = null, header = null, total = 0;
            while (true) {
                const { done, value } = await reader.read(); if (done) break;
                let offset = 0;
                while (!header && offset < value.length) {
                    const target = headerLength === null ? 4 : headerLength + 4;
                    const count = Math.min(target - prefixLength, value.length - offset);
                    prefix.set(value.subarray(offset, offset + count), prefixLength); prefixLength += count; offset += count;
                    if (headerLength === null && prefixLength === 4) { headerLength = new DataView(prefix.buffer).getUint32(0); check(headerLength > 0 && headerLength <= 65536, 'Invalid encrypted context size.'); }
                    if (headerLength !== null && prefixLength === headerLength + 4) header = JSON.parse(decoder.decode(prefix.subarray(4, prefixLength)));
                }
                if (offset < value.length) { const chunk = value.subarray(offset); total += chunk.length; check(total <= MAX_STREAM_BYTES, 'Decrypted attachment exceeds the 4 GiB limit.'); await sink.write(chunk); }
            }
            check(header, 'Missing encrypted context.');
            await requireSignature(result);
            await verifyHeader(prepared, result, header);
            return await sink.commit();
        } catch (error) {
            if (reader) await reader.cancel(error).catch(() => {});
            await Promise.resolve().then(() => sink.abort()).catch(() => {});
            throw error;
        } finally { reader?.releaseLock(); }
    }
    const api = {
        status: scope => locked(scope, async (_s, key) => publicStatus(await store.get(key))),
        observeOwnDirectory: (scope, directory) => locked(scope, async (s, key) => {
            const state = await store.get(key);
            if (!state?.rootFingerprint) return false;
            if (directory.rootPublicKey === state.rootPublicKey) return false;
            const result = await validateDirectory(s, directory, {});
            check(result.directory.credentialId === s.credentialId, 'Wrong account directory.');
            if (result.rootFingerprint === state.rootFingerprint) return false;
            check(result.directory.revision > (state.directory?.revision ?? 0), 'An older account identity was returned.');
            // Keep old keys until the owner explicitly recovers or approves this device.
            state.identityChanged = true; state.approved = false; await store.put(key, state); return true;
        }),
        prepareReset: (scope, directory) => locked(scope, async (s, key) => {
            const old = directoryShape(directory);
            check(old.tenantId === s.tenantId && old.credentialId === s.credentialId, 'Reset belongs to another account.');
            const state = await requireState(key);
            if (state.pendingReset?.expectedRevision === old.revision) return state.pendingReset.result;
            const revision = old.revision + 1, root = await keyPair(`Yap account ${s.credentialId}`, true), device = await newDevice();
            const next = await signDirectory({ ...s, revision, rootPublicKey: root.publicKey, devices: [await approveRecord(s, device, revision, root.privateKey)] }, root.privateKey);
            const pins = { ...state.pins }; delete pins[s.credentialId];
            const fresh = { device, rootPrivateKey: root.privateKey, rootPublicKey: root.publicKey, rootFingerprint: await fingerprint(root.publicKey),
                pins, historicalPins: state.historicalPins ?? {}, historyKeys: [], approved: false, directory: null, pendingDirectory: next, resetHistoryPending: true };
            const result = { directory: next, ...await archive(fresh, s) };
            state.pendingReset = { expectedRevision: old.revision, fresh, result };
            await store.put(key, state); return result;
        }),
        confirmReset: (scope, directory) => locked(scope, async (s, key) => {
            const state = await store.get(key), pending = state?.pendingReset;
            if (state?.resetHistoryPending && state.rootPublicKey === directory.rootPublicKey) {
                await accept(s, state, directory); await store.put(key, state); return true;
            }
            if (!pending || pending.result.directory.rootPublicKey !== directory.rootPublicKey || pending.result.directory.roster !== directory.roster) return false;
            await accept(s, pending.fresh, directory);
            await store.put(key, pending.fresh); return true;
        }),
        acknowledgeReset: scope => locked(scope, async (_s, key) => {
            const state = await requireState(key); state.resetHistoryPending = false; await store.put(key, state);
        }),
        inspectDirectory: (scope, directory) => locked(scope, async (s, key) => {
            const state = await requireState(key), result = await validateDirectory(s, directory, {});
            const pin = state.pins[result.directory.credentialId];
            return { fingerprint: result.rootFingerprint, verified: !!pin?.verified && pin.rootFingerprint === result.rootFingerprint,
                changed: !!pin && pin.rootFingerprint !== result.rootFingerprint };
        }),
        verifyDirectory: (scope, directory, expectedFingerprint) => locked(scope, async (s, key) => {
            const state = await requireState(key), pins = {}, result = await validateDirectory(s, directory, pins);
            const id = result.directory.credentialId;
            check(id !== s.credentialId && result.rootFingerprint === String(expectedFingerprint).toLowerCase().replaceAll(' ', ''), 'The fingerprint does not match.');
            const previous = state.pins[id];
            if (previous && previous.rootFingerprint !== result.rootFingerprint) {
                check(result.directory.revision > previous.revision, 'An older identity was returned.');
                check(!state.historicalPins?.[id]?.[result.rootFingerprint], 'A retired identity was returned.');
                state.historicalPins ??= {}; state.historicalPins[id] ??= {};
                state.historicalPins[id][previous.rootFingerprint] = previous;
            } else await validateDirectory(s, directory, state.pins);
            state.pins[id] = { ...pins[id], verified: true }; await store.put(key, state); return true;
        }),
        // `account` is what the server answered about this account before anything was created:
        // { kind:'account-identity', checked, directory, recoveryArchive }. `checked` is false
        // whenever the lookup did not complete, so an unreachable server can never be mistaken
        // for an account that has no identity. Only a checked, empty account enrolls.
        initialize: (scope, account) => locked(scope, async (s, key) => {
            const known = account?.kind === 'account-identity' && account.checked === true;
            const published = (known && account.directory) || null;
            check(!published || guid(published.tenantId) === s.tenantId && guid(published.credentialId) === s.credentialId, 'Directory belongs to another account.');
            let state = await store.get(key);
            if (!state?.device) {
                check(known, identityUnknown);
                check(!published && !account.recoveryArchive, identityExists);
                const root = await keyPair(`Yap account ${s.credentialId}`, true), device = await newDevice();
                const directory = await signDirectory({ ...s, revision: 1, rootPublicKey: root.publicKey, devices: [await approveRecord(s, device, 1, root.privateKey)] }, root.privateKey);
                state = { device, rootPrivateKey: root.privateKey, rootPublicKey: root.publicKey, rootFingerprint: await fingerprint(root.publicKey), pins: {}, historyKeys: [], approved: false, directory: null, pendingDirectory: directory };
                await store.put(key, state);
            }
            // A local identity this account never confirmed must not be offered as a replacement
            // for the one it published. Nothing is deleted here; the keys stay for recovery.
            check(!published || !!state.directory || state.rootPublicKey === published.rootPublicKey, identityExists);
            check(state.rootPrivateKey, 'Approve this device from an existing device.');
            const backup = await archive(state, s);
            await store.put(key, state);
            return { directory: state.pendingDirectory ?? state.directory, ...backup };
        }),
        acceptDirectory: (scope, directory) => locked(scope, async (s, key) => {
            const state = await requireState(key), result = await accept(s, state, directory);
            await store.put(key, state);
            return { ...publicStatus(state), credentialId: result.directory.credentialId, fingerprint: result.rootFingerprint, verified: state.pins[result.directory.credentialId].verified };
        }),
        verifyFingerprint: (scope, credentialId, expectedFingerprint) => locked(scope, async (_s, key) => {
            const state = await requireState(key), pin = state.pins[guid(credentialId)];
            check(pin && pin.rootFingerprint === String(expectedFingerprint).toLowerCase().replaceAll(' ', ''), 'The verification fingerprint does not match.');
            pin.verified = true; await store.put(key, state); return true;
        }),
        encrypt: (scope, context, payload, directories) => locked(scope, (s, key) => encryptData(s, key, context, encoder.encode(canonical(payload)), directories, 'armored')),
        encryptToDevices: (scope, context, payload, directories, recipientDeviceIds) => locked(scope, (s, key) => encryptData(s, key, context, encoder.encode(canonical(payload)), directories, 'armored', recipientDeviceIds)),
        decrypt: (scope, context, envelope, directory) => locked(scope, async (s, key) => JSON.parse(decoder.decode(await decryptData(s, key, context, envelope, directory, 'armored')))),
        encryptBytes: (scope, context, bytes, directories) => locked(scope, (s, key) => encryptData(s, key, context, bytes, directories, 'binary')),
        decryptBytes: (scope, context, bytes, directory) => locked(scope, (s, key) => decryptData(s, key, context, bytes, directory, 'binary')),
        encryptStream: (scope, context, source, directories) => locked(scope, async (s, key) => {
            const { header, encryptionKeys, signingKeys } = await prepareEncryption(s, key, context, directories);
            return pgp.encrypt({ message: await pgp.createMessage({ binary: boundedStream(source, MAX_STREAM_BYTES, pack(header, new Uint8Array())) }), encryptionKeys, signingKeys, format: 'binary', config });
        }),
        encryptAttachment: (scope, context, source, directories) => locked(scope, async (s, key) => {
            check(context.kind === 'attachment', 'Attachment context is required.');
            const { header, encryptionKeys, signingKeys } = await prepareEncryption(s, key, context, directories);
            const sessionKey = { algorithm: 'aes256', data: crypto.getRandomValues(new Uint8Array(32)) };
            const stream = await pgp.encrypt({ message: await pgp.createMessage({ binary: boundedStream(source, MAX_STREAM_BYTES, pack(header, new Uint8Array())) }),
                encryptionKeys, signingKeys, sessionKey, format: 'binary', config });
            return { stream, key: { algorithm: sessionKey.algorithm, data: Array.from(sessionKey.data, v => v.toString(16).padStart(2, '0')).join('') } };
        }),
        decryptStream: async (scope, context, source, directory, sink, attachmentKey = null) => {
            let prepared;
            try {
                prepared = await locked(scope, (s, key) => prepareDecryption(s, key, context, directory));
                if (attachmentKey) {
                    // The caller obtained this key from the authenticated encrypted message.
                    // The original file signature, device approval, context and AEAD still must verify.
                    check(prepared.c.kind === 'attachment' && attachmentKey.algorithm === 'aes256'
                        && typeof attachmentKey.data === 'string' && /^[0-9a-f]{64}$/.test(attachmentKey.data), 'Invalid attachment key.');
                    prepared.sessionKeys = [{ algorithm: 'aes256', data: Uint8Array.from(attachmentKey.data.match(/../g), x => parseInt(x, 16)) }];
                }
            }
            catch (error) { await Promise.resolve().then(() => sink?.abort?.()).catch(() => {}); throw error; }
            return decryptToSink(prepared, source, sink);
        },
        mergeRecovery: (scope, recoveryArchive, directory) => locked(scope, async (s, key) => {
            const state = await requireState(key);
            if (!recoveryArchive || !state.recoveryKey || !state.rootPrivateKey) return;
            check(directory.rootPublicKey === state.rootPublicKey, 'Encryption identity changed.');
            const result = await pgp.decrypt({ message: await pgp.readMessage({ armoredMessage: recoveryArchive }),
                passwords: [state.recoveryKey], verificationKeys: await readPublic(state.rootPublicKey), expectSigned: true, format: 'binary', config });
            await requireSignature(result);
            const data = JSON.parse(decoder.decode(result.data));
            check(data.v === 1 && data.kind === 'account-recovery' && data.tenantId === s.tenantId &&
                data.credentialId === s.credentialId && data.rootPublicKey === state.rootPublicKey &&
                Array.isArray(data.decryptionKeys) && data.decryptionKeys.length <= MAX_DIRECTORY_DEVICES, 'Invalid recovery archive.');
            for (const privateKey of data.decryptionKeys) await readPrivate(privateKey);
            state.historyKeys = [...new Set([...(state.historyKeys ?? []), ...data.decryptionKeys])].filter(x => x !== state.device.encryptionPrivateKey);
            check(state.historyKeys.length < MAX_DIRECTORY_DEVICES, 'Recovery device limit reached.');
            await store.put(key, state);
        }),
        exportRecovery: scope => locked(scope, async (s, key) => { const state = await requireState(key); const backup = await archive(state, s); await store.put(key, state); return backup; }),
        proposeDevice: scope => locked(scope, async (s, key) => {
            let state = await store.get(key);
            if (!state?.device) { state = { device: await newDevice(), rootPrivateKey: null, rootPublicKey: null, rootFingerprint: null, pins: {}, historyKeys: [], approved: false, directory: null }; await store.put(key, state); }
            if (state.identityChanged) {
                state.pendingRejoinDevice ??= await newDevice(); await store.put(key, state);
                const device = state.pendingRejoinDevice;
                return { v: 1, kind: 'device-proposal', ...s, deviceId: device.deviceId, signingPublicKey: device.signingPublicKey, encryptionPublicKey: device.encryptionPublicKey };
            }
            check(!state.approved && !state.rootPrivateKey, 'This device already owns an account identity.');
            return { v: 1, kind: 'device-proposal', ...s, deviceId: state.device.deviceId, signingPublicKey: state.device.signingPublicKey, encryptionPublicKey: state.device.encryptionPublicKey };
        }),
        approveDevice: (scope, proposal, directory) => locked(scope, async (s, key) => {
            const state = await requireState(key);
            check(state.rootPrivateKey && state.approved, 'This device cannot approve other devices.');
            const { directory: validated } = await accept(s, state, directory);
            check(state.approved, 'This device was revoked.');
            const d = structuredClone(validated);
            check(d.credentialId === s.credentialId && proposal.v === 1 && proposal.kind === 'device-proposal' && guid(proposal.tenantId) === s.tenantId && guid(proposal.credentialId) === s.credentialId, 'The proposed device belongs to another account.');
            const device = { ...proposal, deviceId: guid(proposal.deviceId) };
            const proposalText = canonical(device);
            if (state.lastApproval?.proposal === proposalText && d.devices.some(x => x.deviceId === device.deviceId && !x.revocation)) {
                await store.put(key, state);
                return { alreadyPublished: true, expectedRevision: d.revision, directory: d, approval: state.lastApproval.approval };
            }
            if (state.pendingRotation) {
                check(state.pendingRotation.proposal === proposalText, 'Finish the pending device approval first.');
                if (d.revision === state.pendingRotation.result.expectedRevision) { await store.put(key, state); return state.pendingRotation.result; }
                // A different confirmed mutation won CAS; these unpublished keys
                // were never shared/used and can safely be replaced against it.
                state.pendingRotation = null; state.pendingDirectory = null;
            }
            check(d.devices.length + 2 <= MAX_DIRECTORY_DEVICES && !d.devices.some(x => x.deviceId === device.deviceId), 'The device limit has been reached.');
            const replacement = await newDevice();
            const previous = d.devices.find(x => x.deviceId === state.device.deviceId);
            previous.revocation = await signed({ v: 1, kind: 'device-revocation', ...s, deviceId: previous.deviceId, revokedRevision: d.revision + 1 }, state.rootPrivateKey);
            const updated = await signDirectory({ ...d, revision: d.revision + 1, devices: [...d.devices,
                await approveRecord(s, replacement, d.revision + 1, state.rootPrivateKey),
                await approveRecord(s, device, d.revision + 1, state.rootPrivateKey)] }, state.rootPrivateKey);
            const retiredFingerprints = new Set(d.devices.filter(x => x.revocation).map(x => x.encryptionPublicKey));
            const retiredKeys = [];
            for (const armor of [...new Set([state.device.encryptionPrivateKey, ...(state.historyKeys ?? [])])]) {
                const publicArmor = (await readPrivate(armor)).toPublic().armor();
                if (retiredFingerprints.has(publicArmor)) retiredKeys.push(armor);
            }
            const transfer = { v: 1, kind: 'device-transfer', ...s, deviceId: device.deviceId, rootFingerprint: state.rootFingerprint, directoryRevision: updated.revision, decryptionKeys: retiredKeys };
            const encryptedTransfer = await pgp.encrypt({ message: await pgp.createMessage({ binary: encoder.encode(canonical(transfer)) }), encryptionKeys: await readPublic(device.encryptionPublicKey), signingKeys: await readPrivate(state.rootPrivateKey), format: 'armored', config });
            const result = { alreadyPublished: false, expectedRevision: d.revision, directory: updated, approval: { rootFingerprint: state.rootFingerprint, deviceId: device.deviceId, encryptedTransfer } };
            state.pendingDirectory = updated;
            state.pendingRotation = { proposal: proposalText, device: replacement, historyKeys: retiredKeys, result };
            await store.put(key, state);
            return result;
        }),
        importApproval: (scope, approval, directory) => locked(scope, async (s, key) => {
            let state = await requireState(key);
            check(!state.approved, 'This device is already approved.');
            if (state.identityChanged) {
                check(state.pendingRejoinDevice && directory.revision > (state.directory?.revision ?? 0), 'Create a fresh device request first.');
                const pins = { ...state.pins }; delete pins[s.credentialId];
                state = { device: state.pendingRejoinDevice, pins, historicalPins: state.historicalPins ?? {}, approved: false, resetHistoryPending: true };
            }
            const expectedRootFingerprint = approval.rootFingerprint;
            check(guid(approval.deviceId) === state.device.deviceId, 'This approval belongs to another device.');
            check(await fingerprint(directory.rootPublicKey) === expectedRootFingerprint, 'The account fingerprint does not match the approving device.');
            state.rootPublicKey = directory.rootPublicKey; state.rootFingerprint = expectedRootFingerprint;
            await accept(s, state, directory);
            check(typeof approval.encryptedTransfer === 'string' && approval.encryptedTransfer.length < 4 * 1024 * 1024, 'Missing encrypted device history.');
            const result = await pgp.decrypt({ message: await pgp.readMessage({ armoredMessage: approval.encryptedTransfer }), decryptionKeys: await readPrivate(state.device.encryptionPrivateKey), verificationKeys: await readPublic(state.rootPublicKey), expectSigned: true, format: 'binary', config });
            await requireSignature(result);
            const transfer = JSON.parse(decoder.decode(result.data));
            check(transfer.v === 1 && transfer.kind === 'device-transfer' && transfer.tenantId === s.tenantId && transfer.credentialId === s.credentialId && transfer.deviceId === state.device.deviceId && transfer.rootFingerprint === state.rootFingerprint && transfer.directoryRevision <= state.directory.revision, 'Device transfer context mismatch.');
            check(Array.isArray(transfer.decryptionKeys) && transfer.decryptionKeys.length <= MAX_DIRECTORY_DEVICES, 'Invalid device history.');
            const retiredFingerprints = new Set(await Promise.all(state.directory.devices.filter(d => d.revocation).map(d => fingerprint(d.encryptionPublicKey))));
            for (const privateKey of transfer.decryptionKeys) check(retiredFingerprints.has((await readPrivate(privateKey)).getFingerprint()), 'Cannot import another active device private key.');
            state.historyKeys = transfer.decryptionKeys;
            await store.put(key, state); return publicStatus(state);
        }),
        revokeDevice: (scope, deviceId, directory) => locked(scope, async (s, key) => {
            const state = await requireState(key); check(state.rootPrivateKey && state.approved, 'This device cannot revoke devices.');
            const { directory: validated } = await accept(s, state, directory);
            const d = structuredClone(validated);
            check(d.credentialId === s.credentialId, 'Wrong account directory.');
            const target = d.devices.find(device => device.deviceId === guid(deviceId)); check(target && !target.revocation, 'Device is already revoked or unknown.');
            check(d.devices.filter(device => !device.revocation).length > 1, 'Cannot revoke the last active device.');
            target.revocation = await signed({ v: 1, kind: 'device-revocation', ...s, deviceId: target.deviceId, revokedRevision: d.revision + 1 }, state.rootPrivateKey);
            const updated = await signDirectory({ ...d, revision: d.revision + 1 }, state.rootPrivateKey);
            // Keep the confirmed local roster unchanged until the server accepts CAS.
            await store.put(key, state);
            return { expectedRevision: d.revision, directory: updated };
        }),
        recovery: (scope, recoveryKey, recoveryArchive, directory, preserveDevices = false) => locked(scope, async (s, key) => {
            check(typeof recoveryKey === 'string' && /^[0-9a-f]{64}$/.test(recoveryKey), 'Invalid recovery key.');
            check(typeof recoveryArchive === 'string' && recoveryArchive.length <= 4 * 1024 * 1024, 'Invalid recovery archive.');
            const existing = await store.get(key);
            check(!existing?.approved, 'This device is already enrolled.');
            const d = directoryShape(directory);
            check(d.tenantId === s.tenantId && d.credentialId === s.credentialId, 'Recovery belongs to another account.');
            const result = await pgp.decrypt({ message: await pgp.readMessage({ armoredMessage: recoveryArchive }), passwords: [recoveryKey], verificationKeys: await readPublic(d.rootPublicKey), expectSigned: true, format: 'binary', config });
            await requireSignature(result);
            const data = JSON.parse(decoder.decode(result.data));
            check(data.v === 1 && data.kind === 'account-recovery' && data.tenantId === s.tenantId && data.credentialId === s.credentialId && data.rootPublicKey === d.rootPublicKey && data.rootFingerprint === await fingerprint(d.rootPublicKey), 'Recovery belongs to another account or security key.');
            check((await readPrivate(data.rootPrivateKey)).getFingerprint() === data.rootFingerprint, 'Invalid recovered account key.');
            const pins = { ...(data.pins ?? {}), ...(existing?.pins ?? {}) };
            if (existing?.identityChanged) {
                check(d.revision > (existing.directory?.revision ?? 0), 'An older account identity was returned.');
                delete pins[s.credentialId];
            }
            await validateDirectory(s, d, pins);
            check(d.devices.length < MAX_DIRECTORY_DEVICES && Array.isArray(data.decryptionKeys) && data.decryptionKeys.length <= MAX_DIRECTORY_DEVICES, 'Recovery device limit reached.');
            for (const privateKey of data.decryptionKeys) await readPrivate(privateKey);
            // Restored keys become history-only on this device. Its active identity
            // is fresh; password recovery may leave other devices' identities active.
            for (const old of preserveDevices ? [] : d.devices) {
                if (!old.revocation) old.revocation = await signed({ v: 1, kind: 'device-revocation', ...s, deviceId: old.deviceId, revokedRevision: d.revision + 1 }, data.rootPrivateKey);
            }
            const device = await newDevice();
            const updated = await signDirectory({ ...d, revision: d.revision + 1, devices: [...d.devices, await approveRecord(s, device, d.revision + 1, data.rootPrivateKey)] }, data.rootPrivateKey);
            const state = { device, rootPrivateKey: data.rootPrivateKey, rootPublicKey: d.rootPublicKey, rootFingerprint: data.rootFingerprint, recoveryKey, pins, historicalPins: data.historicalPins ?? {}, historyKeys: data.decryptionKeys, approved: false, directory: d, pendingDirectory: updated, resetHistoryPending: !!existing?.identityChanged };
            await store.put(key, state);
            return { expectedRevision: d.revision, directory: updated, ...await archive(state, s) };
        })
    };
    return api;
}

export const encryption = createEncryption();
