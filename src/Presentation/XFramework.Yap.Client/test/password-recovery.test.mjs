import { test } from 'node:test';
import assert from 'node:assert/strict';
import * as opaque from '../wwwroot/vendor/opaque/opaque.mjs';
import { passwordRecovery, wrapRecovery, unwrapRecovery } from '../wwwroot/password-recovery.mjs';
import { createEncryption } from '../wwwroot/encryption.mjs';
await opaque.ready;
const password = 'test password only, never a real user password';
const scope = `${crypto.randomUUID()}:${crypto.randomUUID()}`;
function device() {
    const store = new Map();
    return createEncryption({ get: async key => structuredClone(store.get(key)), put: async (key, value) => store.set(key, structuredClone(value)) });
}
function server({ loseRegistration = false, loseRestore = false } = {}) {
    const serverSetup = opaque.server.createSetup(), serverIdentity = 'XFramework.IdentityServer.OPAQUE.v1';
    let record, wrappedRecovery, directory, archive, revision = 0;
    const exchanges = new Map(), requests = [];
    const fetcher = async (path, options = {}) => {
        const body = options.body ? JSON.parse(options.body) : {};
        requests.push({ path, body });
        const result = value => new Response(JSON.stringify(value), { status: 200 });
        if (path === '/api/session') return result({ antiforgeryToken: 'test', user: { tenantId: scope.split(':')[0], credentialId: scope.split(':')[1] } });
        if (path.startsWith('/api/chat/')) assert.equal(options.headers['X-Yap-Account'], scope.replaceAll('-', ''));
        if (path.endsWith('/directory')) {
            if (options.method === 'POST') { assert.equal(body.expectedRevision, directory.revision); directory = { ...body, revision: directory.revision + 1 }; if (loseRestore) { loseRestore = false; throw new TypeError('Lost committed response'); } }
            return result(directory);
        }
        if (path.endsWith('/recovery')) {
            if (options.method === 'POST') { assert.equal(body.expectedRevision, revision); archive = body.archive; revision++; }
            return result({ revision, archive });
        }
        assert.equal(path, '/api/auth/opaque');
        if (body.stage === 'options') return result({ mode: 'opaque' });
        if (body.stage === 'status') return result({ mode: 'opaque', client: scope, userName: 'test-user' });
        if (body.stage === 'register-start') {
            const response = opaque.server.createRegistrationResponse({ serverSetup, userIdentifier: scope, registrationRequest: body.message });
            const id = crypto.randomUUID(); exchanges.set(id, {});
            return result({ client: scope, server: serverIdentity, exchangeId: id, message: response.registrationResponse });
        }
        if (body.stage === 'enroll-verify' || body.stage === 'login-start') {
            if (body.stage === 'enroll-verify') assert.ok(exchanges.delete(body.exchangeId));
            const login = opaque.server.startLogin({ serverSetup, userIdentifier: scope,
                registrationRecord: body.record ?? record, startLoginRequest: body.message, identifiers: { client: scope, server: serverIdentity } });
            const id = crypto.randomUUID(); exchanges.set(id, { ...login, body });
            return result({ client: scope, server: serverIdentity, exchangeId: id, message: login.loginResponse });
        }
        const exchange = exchanges.get(body.exchangeId); assert.ok(exchange); exchanges.delete(body.exchangeId);
        opaque.server.finishLogin({ serverLoginState: exchange.serverLoginState, finishLoginRequest: body.message });
        if (body.stage === 'enroll-finish') {
            record = exchange.body.record; wrappedRecovery = exchange.body.wrappedRecovery;
            archive = exchange.body.recoveryArchive; revision = 1; directory = { ...exchange.body.directory, revision: 1 };
            if (loseRegistration) { loseRegistration = false; throw new TypeError('Lost committed response'); }
        }
        return result({ client: scope, wrappedRecovery, exchangeId: crypto.randomUUID() });
    };
    return { fetcher, requests, get directory() { return directory; }, get archive() { return archive; } };
}

test('backup wrapping is account bound and rejects tampering and other export keys', async () => {
    const key = Buffer.from(crypto.getRandomValues(new Uint8Array(64))).toString('base64url'), secret = 'ab'.repeat(32);
    const wrapped = await wrapRecovery(key, scope, secret);
    assert.equal(await unwrapRecovery(key, scope, wrapped), secret);
    await assert.rejects(unwrapRecovery(key, scope + 'x', wrapped));
    await assert.rejects(unwrapRecovery(Buffer.alloc(64).toString('base64url'), scope, wrapped));
    const corrupt = JSON.parse(wrapped); corrupt.ciphertext = Buffer.alloc(80).toString('base64');
    await assert.rejects(unwrapRecovery(key, scope, JSON.stringify(corrupt)));
});

test('registration and fresh-device restore survive lost responses, preserve history and active devices', async () => {
    const api = server({ loseRegistration: true, loseRestore: true }), first = device(), login = passwordRecovery(first, api.fetcher);
    const registered = await login.passwordRegister('test-user', password, 'Test user');
    assert.equal(registered.mode, 'opaque');
    await first.acceptDirectory(scope, api.directory);
    const original = structuredClone(api.directory);
    const context = { tenantId: scope.split(':')[0], senderId: scope.split(':')[1], threadId: crypto.randomUUID(), messageId: crypto.randomUUID(), kind: 'message', parentId: null, isThreadReply: false };
    const ciphertext = await first.encrypt(scope, context, { text: 'old message' }, [original]);
    login.passwordClear();
    const second = device(), recovery = passwordRecovery(second, api.fetcher);
    await assert.rejects(recovery.passwordSignIn('test-user', 'incorrect'));
    await recovery.passwordSignIn('test-user', password);
    assert.equal(await recovery.passwordRestore(scope.replaceAll('-', '')), true);
    assert.equal((await second.decrypt(scope, context, ciphertext, api.directory)).text, 'old message');
    assert.equal(api.directory.devices.filter(d => !d.revocation).length, 2);
    await first.acceptDirectory(scope, api.directory);
    assert.equal((await first.status(scope)).approved, true);
    assert.equal(JSON.stringify(api.requests).includes(password), false);
    assert.equal(api.requests.some(r => JSON.stringify(r.body).includes('exportKey')), false);
    // A backup from an older device must merge, rather than erase, the new device's private key.
    const secondBackup = await second.exportRecovery(scope);
    await first.mergeRecovery(scope, secondBackup.recoveryArchive, api.directory);
    const merged = await first.exportRecovery(scope);
    const third = device();
    const restored = await third.recovery(scope, merged.recoveryKey, merged.recoveryArchive, api.directory, true);
    const newContext = { ...context, messageId: crypto.randomUUID() };
    const afterRestore = await second.encrypt(scope, newContext, { text: 'new-device history' }, [api.directory]);
    assert.equal((await third.decrypt(scope, newContext, afterRestore, api.directory)).text, 'new-device history');
    assert.equal(restored.directory.devices.filter(d => !d.revocation).length, 3);
    recovery.passwordClear();
    assert.equal(await recovery.passwordRestore(scope), false);
});
