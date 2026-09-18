// Automatic unlock at sign-in: the export key the login exchange produces has to open the
// wrapped recovery secret on a device that has never seen a recovery key. These cover the
// migrated (enroll) account, which the registration test does not, and the failure reporting
// that used to collapse every cause into one unexplained "restore unavailable".
import { test } from 'node:test';
import assert from 'node:assert/strict';
import * as opaque from '../wwwroot/vendor/opaque/opaque.mjs';
import { passwordRecovery, wrapRecovery } from '../wwwroot/password-recovery.mjs';
import { createEncryption } from '../wwwroot/encryption.mjs';
await opaque.ready;

const password = 'test password only, never a real user password';
function device() {
    const store = new Map();
    return createEncryption({ get: async key => structuredClone(store.get(key)), put: async (key, value) => store.set(key, structuredClone(value)) });
}

// Mirrors the deployed server: OPAQUE stages over a stored record, and directory/recovery rows
// behind compare-and-swap with the same roster ceiling the identity service enforces.
function server(scope, { corruptEnvelope = false } = {}) {
    const serverSetup = opaque.server.createSetup(), serverIdentity = 'XFramework.IdentityServer.OPAQUE.v1';
    let record = null, wrappedRecovery = null, directory = null, archive = null, revision = 0;
    const exchanges = new Map(), requests = [];
    const fetcher = async (path, options = {}) => {
        const body = options.body ? JSON.parse(options.body) : {};
        requests.push({ path, body });
        const result = value => new Response(JSON.stringify(value), { status: 200 });
        if (path === '/api/session') return result({ antiforgeryToken: 'test', user: { tenantId: scope.split(':')[0], credentialId: scope.split(':')[1] } });
        if (path.startsWith('/api/chat/')) assert.equal(options.headers['X-Yap-Account'], scope.replaceAll('-', ''));
        if (path.endsWith('/directory')) {
            if (options.method === 'POST') {
                assert.equal(body.expectedRevision, directory.revision);
                assert.equal(body.rootPublicKey, directory.rootPublicKey, 'the account root is pinned');
                assert.ok(body.devices.length <= 16, 'the server caps the roster at sixteen devices');
                directory = { tenantId: directory.tenantId, credentialId: directory.credentialId, revision: directory.revision + 1,
                    rootPublicKey: body.rootPublicKey, roster: body.roster, devices: body.devices };
            }
            return result(directory);
        }
        if (path.endsWith('/recovery')) {
            if (options.method === 'POST') { assert.equal(body.expectedRevision, revision); archive = body.archive; revision++; }
            return result({ revision, archive });
        }
        assert.equal(path, '/api/auth/opaque');
        if (body.stage === 'options') return result({ mode: record ? 'opaque' : 'legacy', client: scope });
        if (body.stage === 'status') return result({ mode: record ? 'opaque' : 'legacy', client: scope, userName: 'test-user' });
        if (body.stage === 'enroll-start' || body.stage === 'change-start') {
            const response = opaque.server.createRegistrationResponse({ serverSetup, userIdentifier: scope, registrationRequest: body.message });
            const id = crypto.randomUUID(); exchanges.set(id, {});
            return result({ client: scope, server: serverIdentity, exchangeId: id, message: response.registrationResponse });
        }
        if (body.stage === 'enroll-verify' || body.stage === 'login-start') {
            const login = opaque.server.startLogin({ serverSetup, userIdentifier: scope,
                registrationRecord: body.record ?? record, startLoginRequest: body.message, identifiers: { client: scope, server: serverIdentity } });
            const id = crypto.randomUUID(); exchanges.set(id, { ...login, body });
            return result({ client: scope, server: serverIdentity, exchangeId: id, message: login.loginResponse });
        }
        const exchange = exchanges.get(body.exchangeId); assert.ok(exchange); exchanges.delete(body.exchangeId);
        opaque.server.finishLogin({ serverLoginState: exchange.serverLoginState, finishLoginRequest: body.message });
        if (body.stage === 'enroll-finish') {
            record = exchange.body.record;
            // A stored envelope no export key can open: what a reset that dropped it leaves behind.
            wrappedRecovery = corruptEnvelope
                ? await wrapRecovery(Buffer.alloc(64).toString('base64url'), scope, 'ab'.repeat(32))
                : exchange.body.wrappedRecovery;
        }
        return result({ client: scope, wrappedRecovery, exchangeId: crypto.randomUUID() });
    };
    return { fetcher, requests, get directory() { return directory; }, set directory(value) { directory = value; },
        get archive() { return archive; }, set archive(value) { archive = value; },
        get revision() { return revision; }, set revision(value) { revision = value; } };
}

// A pre-OPAQUE account: encryption was already set up, then password recovery was turned on.
async function migrated(options) {
    const scope = `${crypto.randomUUID()}:${crypto.randomUUID()}`;
    const api = server(scope, options), owner = device();
    const initialized = await owner.initialize(scope, { kind: 'account-identity', checked: true, directory: null, recoveryArchive: null });
    api.directory = initialized.directory; api.archive = initialized.recoveryArchive; api.revision = 1;
    await owner.acceptDirectory(scope, api.directory);
    const context = { tenantId: scope.split(':')[0], senderId: scope.split(':')[1], threadId: crypto.randomUUID(),
        messageId: crypto.randomUUID(), kind: 'message', parentId: null, isThreadReply: false };
    const ciphertext = await owner.encrypt(scope, context, { text: 'message from before the migration' }, [api.directory]);
    const enrolment = passwordRecovery(owner, api.fetcher);
    assert.equal(await enrolment.passwordEnroll(scope, 'test-user', password), true);
    enrolment.passwordClear();
    return { scope, api, owner, context, ciphertext };
}

test('a migrated account unlocks its history at first sign-in on a new device', async () => {
    const { scope, api, owner, context, ciphertext } = await migrated();
    const fresh = device(), login = passwordRecovery(fresh, api.fetcher);
    // Migration proves the retired password to the server once, deliberately. Nothing after it may.
    const afterMigration = api.requests.length;
    await fresh.proposeDevice(scope); // what startup does before anyone signs in
    const signedIn = await login.passwordSignIn('test-user', password);
    assert.deepEqual({ mode: signedIn.mode, recoveryAvailable: signedIn.recoveryAvailable, problem: signedIn.problem },
        { mode: 'opaque', recoveryAvailable: true, problem: null });
    assert.deepEqual(login.passwordUnlockState(scope.replaceAll('-', '')), { signedIn: true, available: true, problem: null });
    assert.equal(await login.passwordRestore(scope.replaceAll('-', '')), true);
    assert.equal((await fresh.decrypt(scope, context, ciphertext, api.directory)).text, 'message from before the migration');
    assert.equal((await fresh.status(scope)).approved, true);
    // Neither the password nor anything derived from it reaches the server while unlocking.
    const sent = JSON.stringify(api.requests.slice(afterMigration));
    assert.equal(sent.includes(password), false);
    assert.equal(sent.includes('exportKey'), false);
    assert.equal(sent.includes('recoveryKey'), false);
    assert.equal(sent.includes((await owner.exportRecovery(scope)).recoveryKey), false, 'the recovery secret stays in the browser');
});

test('a wrong password neither authenticates nor unlocks anything', async () => {
    const { scope, api } = await migrated();
    const fresh = device(), login = passwordRecovery(fresh, api.fetcher);
    await assert.rejects(login.passwordSignIn('test-user', 'not the password'), /username or password is incorrect/);
    assert.deepEqual(login.passwordUnlockState(scope), { signedIn: false, available: false, problem: 'Sign in again to unlock older messages with your password.' });
    assert.equal(await login.passwordRestore(scope), false);
    assert.equal((await fresh.status(scope)).approved, false);
});

test('a backup the export key cannot open is reported instead of failing silently', async () => {
    const { scope, api } = await migrated({ corruptEnvelope: true });
    const fresh = device(), login = passwordRecovery(fresh, api.fetcher);
    const signedIn = await login.passwordSignIn('test-user', password);
    // Authentication still succeeded: the account stays usable and only history stays locked.
    assert.equal(signedIn.mode, 'opaque');
    assert.equal(signedIn.recoveryAvailable, false);
    assert.match(signedIn.problem, /no longer opens/);
    assert.deepEqual(login.passwordUnlockState(scope), { signedIn: true, available: false, problem: signedIn.problem });
    // The regression: this used to return a bare false that the UI turned into a sentence about
    // codes, fingerprints and the connection, none of which was the reason.
    await assert.rejects(login.passwordRestore(scope), error => error.message === signedIn.problem);
});

test('the recovery key still restores a device the password path cannot', async () => {
    const { scope, api, owner, context, ciphertext } = await migrated({ corruptEnvelope: true });
    const backup = await owner.exportRecovery(scope);
    const fresh = device();
    const restored = await fresh.recovery(scope, backup.recoveryKey, api.archive, api.directory);
    await api.fetcher('/api/chat/encryption/directory', { method: 'POST', headers: { 'X-Yap-Account': scope.replaceAll('-', '') },
        body: JSON.stringify({ expectedRevision: restored.expectedRevision, rootPublicKey: restored.directory.rootPublicKey,
            roster: restored.directory.roster, devices: restored.directory.devices }) });
    await fresh.acceptDirectory(scope, api.directory);
    assert.equal((await fresh.status(scope)).approved, true);
    assert.equal((await fresh.decrypt(scope, context, ciphertext, api.directory)).text, 'message from before the migration');
});

test('a full device roster says so rather than reporting a connection problem', async () => {
    const { scope, api } = await migrated();
    let unlocked = 0, failure = null;
    // Every password unlock appends a device and revokes none, so the signed roster fills up.
    for (let attempt = 0; attempt < 20; attempt++) {
        const fresh = device(), login = passwordRecovery(fresh, api.fetcher);
        await fresh.proposeDevice(scope);
        await login.passwordSignIn('test-user', password);
        try { await login.passwordRestore(scope); unlocked++; }
        catch (error) { failure = error; break; }
    }
    assert.equal(unlocked, 15, 'fifteen unlocks fit beside the original device');
    assert.equal(api.directory.devices.length, 16);
    assert.match(failure.message, /device limit/i);
});

// The account state a sign-in device must never guess at: what the server already holds for it.
const asked = api => ({ kind: 'account-identity', checked: true, directory: api.directory, recoveryArchive: api.archive });
async function peerOf(scope, api, owner) {
    const peerScope = `${scope.split(':')[0]}:${crypto.randomUUID()}`, peer = device();
    const enrolled = await peer.initialize(peerScope, { kind: 'account-identity', checked: true, directory: null, recoveryArchive: null });
    await peer.acceptDirectory(peerScope, enrolled.directory);
    const context = { tenantId: scope.split(':')[0], senderId: scope.split(':')[1], threadId: crypto.randomUUID(),
        messageId: crypto.randomUUID(), kind: 'message', parentId: null, isThreadReply: false };
    const ciphertext = await owner.encrypt(scope, context, { text: 'sent before the sign-out' }, [api.directory, enrolled.directory]);
    // Decrypting pins the sender's root. A replacement root is what turns this into a warning.
    assert.equal((await peer.decrypt(peerScope, context, ciphertext, api.directory)).text, 'sent before the sign-out');
    return { peer, peerScope, context, ciphertext };
}

test('signing out and in again recovers the same account root instead of publishing a new one', async () => {
    const { scope, api, owner, context, ciphertext } = await migrated();
    const { peer, peerScope, context: shared, ciphertext: message } = await peerOf(scope, api, owner);
    const root = api.directory.rootPublicKey, fingerprint = (await owner.status(scope)).rootFingerprint;
    // Signing out leaves this device with no keys at all; signing in must not answer that with a
    // brand-new account identity, which is what made the messages above unreadable.
    const fresh = device(), login = passwordRecovery(fresh, api.fetcher);
    await assert.rejects(fresh.initialize(scope, asked(api)), /already has encrypted messages/);
    await login.passwordSignIn('test-user', password);
    assert.equal(await login.passwordRestore(scope), true);
    assert.equal(api.directory.rootPublicKey, root, 'the published account root is untouched');
    assert.equal((await fresh.status(scope)).rootFingerprint, fingerprint);
    assert.equal((await fresh.decrypt(scope, context, ciphertext, api.directory)).text, 'message from before the migration');
    // The reported symptom: every peer that had pinned this account saw its key change.
    await peer.acceptDirectory(peerScope, api.directory);
    assert.equal((await peer.decrypt(peerScope, shared, message, api.directory)).text, 'sent before the sign-out');
    assert.deepEqual(await fresh.inspectDirectory(scope, api.directory), { fingerprint, verified: false, changed: false });
});

test('a sign-in that cannot recover reports the lock and leaves the account identity alone', async () => {
    const { scope, api, owner } = await migrated({ corruptEnvelope: true });
    const { peer, peerScope, context, ciphertext } = await peerOf(scope, api, owner);
    const root = api.directory.rootPublicKey, revision = api.directory.revision;
    const fresh = device(), login = passwordRecovery(fresh, api.fetcher);
    const signedIn = await login.passwordSignIn('test-user', password);
    await assert.rejects(login.passwordRestore(scope), error => error.message === signedIn.problem);
    // Nothing is created for an account this device cannot open yet: a blocked screen keeps the
    // history that a replacement identity would have orphaned.
    await assert.rejects(fresh.initialize(scope, asked(api)), /already has encrypted messages/);
    assert.equal((await fresh.status(scope)).enrolled, false);
    assert.deepEqual([api.directory.rootPublicKey, api.directory.revision], [root, revision]);
    // A device request is still local-only, so approval from a trusted device stays available.
    await fresh.proposeDevice(scope);
    await assert.rejects(fresh.initialize(scope, asked(api)), /already has encrypted messages/);
    assert.deepEqual([api.directory.rootPublicKey, api.directory.revision], [root, revision]);
    await peer.acceptDirectory(peerScope, api.directory);
    assert.equal((await peer.decrypt(peerScope, context, ciphertext, api.directory)).text, 'sent before the sign-out');
});
