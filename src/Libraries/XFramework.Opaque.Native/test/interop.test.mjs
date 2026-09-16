import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import * as opaque from '../../../Presentation/XFramework.Yap.Client/wwwroot/vendor/opaque/opaque.mjs';

await opaque.ready;
test('native server interoperates with browser OPAQUE and rejects altered proofs and identities', async () => {
    const bridge = spawn(process.platform === 'win32' ? 'python' : 'python3', [fileURLToPath(new URL('./bridge.py', import.meta.url))]);
    const lines = createInterface({ input: bridge.stdout })[Symbol.asyncIterator]();
    const call = async data => { bridge.stdin.write(JSON.stringify(data) + '\n'); return JSON.parse((await lines.next()).value); };
    try {
        const { setup } = await call({ operation: 'setup' });
        const client = 'test-tenant:test-account', server = 'XFramework.IdentityServer.OPAQUE.v1';
        const identifiers = { client, server }, password = 'interop test password, not a real credential';
        const registration = opaque.client.startRegistration({ password });
        const response = await call({ operation: 'register', setup, client, request: registration.registrationRequest });
        const registered = opaque.client.finishRegistration({ ...registration, password, keyStretching: 'memory-constrained', identifiers, registrationResponse: response.response });
        const record = registered.registrationRecord;
        assert.equal((await call({ operation: 'validate', record })).valid, true);
        const begin = async () => {
            const login = opaque.client.startLogin({ password });
            const started = await call({ operation: 'start', setup, record, client, server, request: login.startLoginRequest });
            return { login, started };
        };
        const { login, started } = await begin();
        const finished = opaque.client.finishLogin({ ...login, password, keyStretching: 'memory-constrained', identifiers, loginResponse: started.response });
        assert.equal(finished.exportKey, registered.exportKey);
        assert.equal((await call({ operation: 'finish', state: started.state, client, server, request: finished.finishLoginRequest })).valid, true);
        const corrupted = Buffer.from(finished.finishLoginRequest, 'base64url'); corrupted[0] ^= 1;
        assert.ok((await call({ operation: 'finish', state: started.state, client, server, request: corrupted.toString('base64url') })).error);
        const other = await begin();
        assert.equal(opaque.client.finishLogin({ ...other.login, password, keyStretching: 'memory-constrained', identifiers: { client: 'other-account', server }, loginResponse: other.started.response }), undefined);
        const wrong = await begin();
        assert.equal(opaque.client.finishLogin({ ...wrong.login, password: 'wrong password', keyStretching: 'memory-constrained', identifiers, loginResponse: wrong.started.response }), undefined);
        const fakeLogin = opaque.client.startLogin({ password });
        const fake = await call({ operation: 'start', setup, record: null, client, server, request: fakeLogin.startLoginRequest });
        assert.ok(fake.response);
        assert.equal(opaque.client.finishLogin({ ...fakeLogin, password, keyStretching: 'memory-constrained', identifiers, loginResponse: fake.response }), undefined);
        for (const operation of ['register', 'start', 'finish', 'validate']) {
            assert.ok((await call({ operation, setup, client, server, request: 'malformed', state: 'malformed', record: 'malformed' })).error);
        }
    } finally { bridge.stdin.end(); bridge.kill(); }
});
