import * as opaque from './vendor/opaque/opaque.mjs';

const enc = new TextEncoder(), dec = new TextDecoder();
const b64 = bytes => btoa(String.fromCharCode(...new Uint8Array(bytes)));
const unb64 = value => Uint8Array.from(atob(value.replaceAll('-', '+').replaceAll('_', '/')), c => c.charCodeAt(0));
const serverIdentity = 'XFramework.IdentityServer.OPAQUE.v1';
const canonicalScope = value => value.split(':').map(x => { const n = x.replaceAll('-', '').toLowerCase(); if (!/^[a-f0-9]{32}$/.test(n)) throw new Error('Invalid account.'); return `${n.slice(0,8)}-${n.slice(8,12)}-${n.slice(12,16)}-${n.slice(16,20)}-${n.slice(20)}`; }).join(':');
const keyStretching = 'memory-constrained';
// Said once, in the words the person reads. A failure here is never a connection problem.
const noBackup = 'This account has no password-protected backup yet. Unlock with your recovery key or a trusted device, then turn password recovery back on.';
const unwrapFailed = 'Your password no longer opens this account’s saved backup. Unlock with your recovery key or a trusted device, then turn password recovery back on.';
const notSignedIn = 'Sign in again to unlock older messages with your password.';

async function wrappingKey(exportKey, scope) {
    const material = unb64(exportKey);
    try {
        const key = await crypto.subtle.importKey('raw', material, 'HKDF', false, ['deriveKey']);
        return await crypto.subtle.deriveKey({ name: 'HKDF', hash: 'SHA-256', salt: enc.encode(scope),
            info: enc.encode('Yap password recovery v1') }, key, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
    } finally { material.fill(0); }
}
export async function wrapRecovery(exportKey, scope, secret) {
    if (!/^[a-f0-9]{64}$/.test(secret)) throw new Error('Invalid recovery material.');
    const key = await wrappingKey(exportKey, scope), iv = crypto.getRandomValues(new Uint8Array(12));
    const plaintext = enc.encode(secret);
    try {
        const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: enc.encode(scope) }, key, plaintext);
        return JSON.stringify({ v: 1, iv: b64(iv), ciphertext: b64(ciphertext) });
    } finally { plaintext.fill(0); }
}
export async function unwrapRecovery(exportKey, scope, envelope) {
    const value = JSON.parse(envelope);
    if (value.v !== 1) throw new Error('Unsupported recovery version.');
    const key = await wrappingKey(exportKey, scope);
    const plaintext = new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: unb64(value.iv),
        additionalData: enc.encode(scope) }, key, unb64(value.ciphertext)));
    try {
        const secret = dec.decode(plaintext);
        if (!/^[a-f0-9]{64}$/.test(secret)) throw new Error('Invalid recovery material.');
        return secret;
    } finally { plaintext.fill(0); }
}

export function passwordRecovery(encryption, fetcher = (...args) => fetch(...args)) {
    // Secrets live only in this worker. Neither export keys nor recovery keys go to the server.
    const unlocked = new Map();
    let generation = 0;
    const session = async () => {
        const response = await fetcher('/api/session', { credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(30000) });
        if (!response.ok) throw new Error('Cannot reach the sign-in service.');
        return response.json();
    };
    const post = async (path, body, token, scope) => {
        const response = await fetcher(path, { method: 'POST', credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(30000),
            headers: { 'Content-Type': 'application/json', RequestVerificationToken: token, ...(scope ? { 'X-Yap-Account': scope.replaceAll('-', '') } : {}) }, body: JSON.stringify(body) });
        if (!response.ok) {
            let error;
            try { error = (await response.json()).error; } catch { }
            throw new Error(error || (response.status === 429 ? 'Too many attempts. Try again shortly.' : 'Authentication could not finish. Please try again.'));
        }
        return response.status === 204 ? null : response.json();
    };
    const get = async (path, scope) => {
        const response = await fetcher(path, { credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(30000), headers: { 'X-Yap-Account': scope.replaceAll('-', '') } });
        if (!response.ok) throw new Error('Encrypted backup is unavailable. Please try again.');
        return response.json();
    };
    const scopedSession = async scope => {
        const current = await session();
        if (!current.user || `${current.user.tenantId}:${current.user.credentialId}` !== scope) throw new Error('The signed-in account changed.');
        return current;
    };
    const exchange = (body, token) => post('/api/auth/opaque', body, token);
    const identifiers = result => {
        if (result.server !== serverIdentity || !/^[a-f0-9-]{36}:[a-f0-9-]{36}$/.test(result.client)) throw new Error('Invalid authentication identity.');
        return { client: result.client, server: serverIdentity };
    };
    async function signIn(username, password) {
        await opaque.ready;
        const version = ++generation; unlocked.clear();
        const current = await session(), token = current.antiforgeryToken;
        const options = await exchange({ stage: 'options', userName: username }, token);
        if (options.mode === 'legacy') return { mode: 'legacy' };
        const login = opaque.client.startLogin({ password });
        const started = await exchange({ stage: 'login-start', userName: username, message: login.startLoginRequest }, token);
        const finished = opaque.client.finishLogin({ ...login, password, identifiers: identifiers(started), keyStretching, loginResponse: started.message });
        if (!finished) throw new Error('The username or password is incorrect.');
        const confirmed = await exchange({ stage: 'login-finish', userName: username, exchangeId: started.exchangeId, message: finished.finishLoginRequest }, token);
        if (version !== generation) throw new Error('The signed-in account changed.');
        let secret = null, problem = null;
        // Authentication succeeded; keep reauthentication available for an explicit lost-key reset.
        // Keep the reason too: swallowing it turned every cause - no envelope, a reset that dropped
        // it, a wrong account - into one unexplained "restore unavailable" for the person.
        try { secret = await unwrapRecovery(finished.exportKey, started.client, confirmed.wrappedRecovery); }
        catch { problem = confirmed.wrappedRecovery ? unwrapFailed : noBackup; }
        unlocked.set(started.client, { secret, problem, exportKey: finished.exportKey, grant: confirmed.exchangeId, username });
        return { mode: 'opaque', scope: started.client, recoveryAvailable: !!secret, problem };
    }
    async function enroll(scope, username, password, newPassword) {
        scope = canonicalScope(scope);
        await opaque.ready;
        const version = generation;
        const current = await scopedSession(scope), token = current.antiforgeryToken;
        const status = await encryption.status(scope);
        if (!status.canApproveDevices) return false;
        const directory = await get('/api/chat/encryption/directory', scope);
        await encryption.acceptDirectory(scope, directory);
        const previous = await get('/api/chat/encryption/recovery', scope);
        await encryption.mergeRecovery(scope, previous.archive, directory);
        const archive = await encryption.exportRecovery(scope);
        await post('/api/chat/encryption/recovery', { expectedRevision: previous.revision,
            archive: archive.recoveryArchive, rootPublicKey: directory.rootPublicKey }, token, scope);
        const nextPassword = newPassword ?? password;
        const start = opaque.client.startRegistration({ password: nextPassword });
        const started = await exchange({ stage: newPassword ? 'change-start' : 'enroll-start', userName: username,
            message: start.registrationRequest, legacyPassword: newPassword ? null : password,
            exchangeId: newPassword ? unlocked.get(scope)?.grant : undefined }, token);
        if (started.client !== scope || generation !== version) throw new Error('The signed-in account changed.');
        const registered = opaque.client.finishRegistration({ ...start, password: nextPassword, identifiers: identifiers(started), keyStretching, registrationResponse: started.message });
        const wrappedRecovery = await wrapRecovery(registered.exportKey, scope, archive.recoveryKey);
        if (await unwrapRecovery(registered.exportKey, scope, wrappedRecovery) !== archive.recoveryKey) throw new Error('Backup verification failed.');
        await confirmRegistration(username, nextPassword, started, registered, { wrappedRecovery }, token);
        if (generation !== version) throw new Error('The signed-in account changed.');
        unlocked.set(scope, { secret: archive.recoveryKey, problem: null, exportKey: registered.exportKey, username });
        return true;
    }
    async function confirmRegistration(username, password, started, registered, backup, token) {
        const login = opaque.client.startLogin({ password });
        const challenge = await exchange({ stage: 'enroll-verify', userName: username, exchangeId: started.exchangeId,
            message: login.startLoginRequest, record: registered.registrationRecord, ...backup }, token);
        const proof = opaque.client.finishLogin({ ...login, password, identifiers: identifiers(started), keyStretching, loginResponse: challenge.message });
        if (!proof || proof.exportKey !== registered.exportKey) throw new Error('Credential verification failed.');
        try {
            return await exchange({ stage: 'enroll-finish', userName: username, exchangeId: challenge.exchangeId, message: proof.finishLoginRequest, recoveryArchive: backup.recoveryArchive }, token);
        } catch (error) {
            // A lost response may follow a committed registration/password change. Verify the
            // actual credential and envelope before treating it as successful; never retry a proof.
            const attempt = opaque.client.startLogin({ password });
            const latestToken = (await session()).antiforgeryToken;
            try {
                const active = await exchange({ stage: 'login-start', userName: username, message: attempt.startLoginRequest }, latestToken);
                const verified = opaque.client.finishLogin({ ...attempt, password, identifiers: identifiers(active), keyStretching, loginResponse: active.message });
                if (!verified || active.client !== started.client || verified.exportKey !== registered.exportKey) throw error;
                const accepted = await exchange({ stage: 'login-finish', userName: username, exchangeId: active.exchangeId, message: verified.finishLoginRequest }, latestToken);
                if (accepted.wrappedRecovery !== backup.wrappedRecovery) throw error;
                return accepted;
            } catch { throw error; }
        }
    }
    return {
        passwordSignIn: signIn,
        passwordEnroll: enroll,
        async passwordRegister(username, password, displayName) {
            if (password.length < 8) throw new Error('Use a password with at least 8 characters.');
            await opaque.ready;
            const token = (await session()).antiforgeryToken;
            const start = opaque.client.startRegistration({ password });
            const started = await exchange({ stage: 'register-start', userName: username, displayName, message: start.registrationRequest }, token);
            const registered = opaque.client.finishRegistration({ ...start, password, identifiers: identifiers(started), keyStretching, registrationResponse: started.message });
            const scope = started.client;
            const initialized = await encryption.initialize(scope);
            const archive = await encryption.exportRecovery(scope);
            const directory = initialized.directory;
            await confirmRegistration(username, password, started, registered, {
                wrappedRecovery: await wrapRecovery(registered.exportKey, scope, archive.recoveryKey),
                recoveryArchive: archive.recoveryArchive, directory: { ...directory, expectedRevision: 0 }
            }, token);
            return signIn(username, password);
        },
        // True once this worker still holds sign-in material, so an unlock that was interrupted
        // can be retried without asking for the password a second time.
        passwordUnlockState(scope) {
            const material = unlocked.get(canonicalScope(scope));
            return { signedIn: !!material, available: !!material?.secret, problem: material ? material.problem : notSignedIn };
        },
        async passwordRestore(scope) {
            scope = canonicalScope(scope);
            const material = unlocked.get(scope);
            // Only "no sign-in in this browser session" is an ordinary false. Every other reason
            // is reported, because the caller can do nothing useful with an unexplained failure.
            if (!material) return false;
            if (!material.secret) throw new Error(material.problem ?? noBackup);
            const version = generation;
            const current = await scopedSession(scope);
            const directory = await get('/api/chat/encryption/directory', scope);
            await encryption.observeOwnDirectory(scope, directory);
            const status = await encryption.status(scope);
            if (status.approved && status.rootFingerprint) {
                await encryption.acceptDirectory(scope, directory);
                if ((await encryption.status(scope)).approved) return true;
            }
            const archive = await get('/api/chat/encryption/recovery', scope);
            const restored = await encryption.recovery(scope, material.secret, archive.archive, directory, true);
            if (generation !== version) throw new Error('The signed-in account changed.');
            let confirmed;
            try {
                confirmed = await post('/api/chat/encryption/directory', { ...restored.directory,
                    expectedRevision: restored.directory.revision - 1 }, current.antiforgeryToken, scope);
            } catch (error) {
                confirmed = await get('/api/chat/encryption/directory', scope);
                if (confirmed.rootPublicKey !== restored.directory.rootPublicKey || confirmed.roster !== restored.directory.roster) throw error;
            }
            if (generation !== version) throw new Error('The signed-in account changed.');
            await encryption.acceptDirectory(scope, confirmed);
            return true;
        },
        async passwordChange(scope, username, password, nextPassword) {
            scope = canonicalScope(scope);
            if (nextPassword.length < 8) throw new Error('Use a password with at least 8 characters.');
            const login = await signIn(username, password);
            if (login.mode !== 'opaque' || login.scope !== scope) throw new Error('Sign in to this account before changing its password.');
            if (!await enroll(scope, username, password, nextPassword)) throw new Error('Restore encrypted messages before changing your password.');
            await signIn(username, nextPassword); // Password change revokes all previous sessions.
            return true;
        },
        async passwordStatus(scope) {
            scope = canonicalScope(scope);
            const current = await scopedSession(scope);
            const result = await exchange({ stage: 'status' }, current.antiforgeryToken);
            if (result.client !== scope) throw new Error('The signed-in account changed.');
            return { enabled: result.mode === 'opaque', username: result.userName };
        },
        async passwordReset(scope, password) {
            scope = canonicalScope(scope);
            const current = await scopedSession(scope);
            const info = await exchange({ stage: 'status' }, current.antiforgeryToken);
            if (info.mode !== 'opaque') return false;
            if (info.client !== scope) throw new Error('The signed-in account changed.');
            const login = await signIn(info.userName, password);
            if (login.scope !== scope) throw new Error('The signed-in account changed.');
            const material = unlocked.get(scope), token = (await scopedSession(scope)).antiforgeryToken;
            let directory = await get('/api/chat/encryption/directory', scope);
            if (await encryption.confirmReset(scope, directory)) return true;
            const pending = await encryption.prepareReset(scope, directory);
            try {
                directory = await post('/api/chat/encryption/reset', { opaqueProof: material.grant,
                    directory: { ...pending.directory, expectedRevision: pending.directory.revision - 1 },
                    recoveryArchive: pending.recoveryArchive,
                    wrappedRecovery: await wrapRecovery(material.exportKey, scope, pending.recoveryKey) }, token, scope);
            } catch (error) {
                directory = await get('/api/chat/encryption/directory', scope);
                if (directory.rootPublicKey !== pending.directory.rootPublicKey) throw error;
            }
            if (!await encryption.confirmReset(scope, directory)) throw new Error('Reset could not be confirmed.');
            unlocked.set(scope, { ...material, secret: pending.recoveryKey, problem: null, grant: null });
            return true;
        },
        passwordClear() { generation++; unlocked.clear(); }
    };
}
